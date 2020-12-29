using Mirror;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.WebRTC;
using UnityEngine;

namespace Mirror.WebRTC
{
    [RequireComponent(typeof(RTCTransport))]
    public class RTCFacepunchSignaler : RTCSignaler
    {
		[Serializable]
		class SignalMessage
		{
			public enum Type { Offer, Answer, Decline, IceCandidate }
			public Type type;
			public string data;
		}

        public uint steamAppID = 480;
        public bool allowP2PPacketRelay = true;

		RTCTransport rtcTransport;
		Dictionary<SteamId, int> serverSteamIDToId;

		public bool IsConnected => SteamClient.IsValid && SteamClient.IsLoggedOn;

		void Start()
		{
			rtcTransport = GetComponent<RTCTransport>();
			rtcTransport.OnServerStart += () => serverSteamIDToId = new Dictionary<SteamId, int>();
			rtcTransport.OnServerStop += () => serverSteamIDToId = null;

			try
            {
                SteamClient.Init(steamAppID, true);
                SteamNetworking.AllowP2PPacketRelay(allowP2PPacketRelay);

                SteamNetworking.OnP2PSessionRequest += SteamNetworking_OnP2PSessionRequest;
                SteamNetworking.OnP2PConnectionFailed += SteamNetworking_OnP2PConnectionFailed;
            }
            catch (Exception exception)
            {
                Debug.LogError($"{GetType().Name}: Error initializing Steam\n{exception}");
            }

            if (IsConnected)
                InvokeRepeating(nameof(PollP2PPackets), 0, 0.5f);
        }

		void OnApplicationQuit()
		{
            SteamNetworking.OnP2PSessionRequest -= SteamNetworking_OnP2PSessionRequest;
            SteamNetworking.OnP2PConnectionFailed -= SteamNetworking_OnP2PConnectionFailed;

            SteamClient.Shutdown();
		}

		void SteamNetworking_OnP2PSessionRequest(SteamId steamID) => SteamNetworking.AcceptP2PSessionWithUser(steamID);
		void SteamNetworking_OnP2PConnectionFailed(SteamId steamID, P2PSessionError error) => SteamNetworking.CloseP2PSessionWithUser(steamID);

		void PollP2PPackets()
		{
			while (SteamNetworking.IsP2PPacketAvailable())
			{
                var packet = SteamNetworking.ReadP2PPacket();

				if (!packet.HasValue)
					continue;

				try
				{
					string json = Encoding.UTF8.GetString(packet.Value.Data);
					SignalMessage message = JsonUtility.FromJson<SignalMessage>(json);

					Debug.Log($"{GetType().Name}: Received {message.type}\nFrom: {packet.Value.SteamId.Value}");

					if (message.type == SignalMessage.Type.Offer)
						ReceiveOffer(packet.Value.SteamId, message);
					else if (message.type == SignalMessage.Type.Answer)
						ReceiveAnswer(packet.Value.SteamId, message);
					else if (message.type == SignalMessage.Type.Decline)
						ReceiveDecline(packet.Value.SteamId, message);
					else if (message.type == SignalMessage.Type.IceCandidate)
						ReceiveIceCandidate(packet.Value.SteamId, message);
				}
				catch (Exception exception)
				{
					Debug.LogError($"{GetType().Name}: Error parsing received message\n{exception}");
				}
			}
		}

		/// <summary>
		/// Send a fully decorated SignalMessage to a specific SteamID.
		/// </summary>
		/// <param name="message">The SignalMessage to send.</param>
		void Send(SteamId steamID, SignalMessage message)
		{
			try
			{
				Debug.Log($"{GetType().Name}: Sending {message.type}\nTo: {steamID}");

				string json = JsonUtility.ToJson(message);
				byte[] bytes = Encoding.UTF8.GetBytes(json);

				SteamNetworking.SendP2PPacket(steamID, bytes, bytes.Length);
			}
			catch (Exception exception)
			{
				Debug.LogError($"{GetType().Name}: Error sending message\n{exception}");
			}
		}

		/// <summary>
		/// As a Client, offer to join a Host via Facepunch signaling.
		/// </summary>
		/// <param name="address">String variant of SteamID of Host we wish to join.</param>
		public override void Offer(string address)
		{
			try
			{
				SteamId steamID = ulong.Parse(address);
				Offer(steamID);
			}
			catch (Exception exception)
			{
				Debug.LogError($"{GetType().Name}: Invalid address: {address}\n{exception}");
				return;
			}
		}

		/// <summary>
		/// As a Client, offer to join a Host via Facepunch signaling.
		/// </summary>
		/// <param name="recipientID">SteamID of Host we wish to join.</param>
		public async void Offer(SteamId recipientID)
		{
			if (!IsConnected)
			{
				Debug.LogError($"{GetType().Name}: Not logged in to Steam\n");
				return;
			}

			if (!recipientID.IsValid)
			{
				Debug.LogError($"{GetType().Name}: Invalid RecipientID\n{recipientID}");
				return;
			}

			try
			{
				Debug.Log($"{GetType().Name}: Creating Offer\n{recipientID}");

				RTCConnection connection = await RTCConnection.CreateOffer(rtcTransport, (iceCandidate) =>
				{
					Send(recipientID, new SignalMessage
					{
						type = SignalMessage.Type.IceCandidate,
						data = JsonUtility.ToJson(iceCandidate)
					});
				});

				rtcTransport.clientConnection = connection;

				connection.DataChannels_OnOpen += () =>
				{
					Debug.Log($"{connection.GetType().Name}: DataChannels_OnOpen\nInvoking OnClientConnected");
					rtcTransport.OnClientConnected.Invoke();
				};

				connection.DataChannels_OnClose += () =>
				{
					Debug.Log($"{connection.GetType().Name}: DataChannels_OnClose\nInvoking OnClientDisconnected");
					rtcTransport.OnClientDisconnected.Invoke();
				};

				connection.DataChannel_OnMessage += (channel, bytes) =>
				{
					rtcTransport.OnClientDataReceived.Invoke(new ArraySegment<byte>(bytes), channel);
				};

				Send(recipientID, new SignalMessage
				{
					type = SignalMessage.Type.Offer,
					data = JsonUtility.ToJson(connection.LocalDescription)
				});
			}
			catch (InvalidOperationException exception)
			{
				Debug.LogError($"{GetType().Name}: Error in RTCConnection operation\n{exception.Message}");
				return;
			}
			catch (Exception exception)
			{
				Debug.LogError($"{GetType().Name}: Error creating Offer\n{exception}");
				return;
			}
		}

		/// <summary>
		/// As a Host, receive an Offer SignalMessage from a Client via Facepunch signaling.
		/// </summary>
		/// <param name="message">The received SignalMessage.</param>
		async void ReceiveOffer(SteamId steamID, SignalMessage message)
		{
			if (!rtcTransport.ServerActive())
			{
				Debug.LogError($"{GetType().Name}: Server not active\n");

				Send(steamID, new SignalMessage
				{
					type = SignalMessage.Type.Decline,
					data = "Server not active"
				});

				return;
			}

			try
			{
				Debug.Log($"{GetType().Name}: Creating Answer\n");

				RTCSessionDescription remoteDescription = JsonUtility.FromJson<RTCSessionDescription>(message.data);
				RTCConnection connection = await RTCConnection.CreateAnswer(rtcTransport, (iceCandidate) =>
				{
					Send(steamID, new SignalMessage
					{
						type = SignalMessage.Type.IceCandidate,
						data = JsonUtility.ToJson(iceCandidate)
					});
				}, remoteDescription);

				int connectionID = rtcTransport.serverNextConnectionID++;
				serverSteamIDToId.Add(steamID, connectionID);
				rtcTransport.serverConnections.Add(connectionID, connection);

				connection.DataChannels_OnOpen += () =>
				{
					Debug.Log($"{connection.GetType().Name}: DataChannels_OnOpen\nInvoking OnServerConnected");
					rtcTransport.OnServerConnected.Invoke(connectionID);
				};

				connection.DataChannels_OnClose += () =>
				{
					if (!rtcTransport.ServerActive())
					{
						Debug.LogError($"{connection.GetType().Name}: DataChannels_OnClose\nServer not active");
						return;
					}

					Debug.Log($"{connection.GetType().Name}: DataChannels_OnClose\nInvoking OnServerDisconnected");
					serverSteamIDToId.Remove(steamID);
					rtcTransport.serverConnections.Remove(connectionID);
					rtcTransport.OnServerDisconnected.Invoke(connectionID);
				};

				connection.DataChannel_OnMessage += (channel, bytes) =>
				{
					rtcTransport.OnServerDataReceived.Invoke(connectionID, new ArraySegment<byte>(bytes), channel);
				};

				Send(steamID, new SignalMessage
				{
					type = SignalMessage.Type.Answer,
					data = JsonUtility.ToJson(connection.LocalDescription)
				});
			}
			catch (ArgumentException exception)
			{
				Debug.LogError($"{GetType().Name}: Error parsing RemoteDescription\n{exception}");
				return;
			}
			catch (InvalidOperationException exception)
			{
				Debug.LogError($"{GetType().Name}: Error in RTCConnection operation\n{exception.Message}");
				return;
			}
			catch (Exception exception)
			{
				Debug.LogError($"{GetType().Name}: Error creating Answer\n{exception}");
				return;
			}
		}

		/// <summary>
		/// As a Client, receive an Answer SignalMessage from a Host via Facepunch signaling.
		/// </summary>
		/// <param name="message">The received SignalMessage.</param>
		async void ReceiveAnswer(SteamId steamID, SignalMessage message)
		{
			if (rtcTransport.clientConnection == null)
			{
				Debug.LogError($"{GetType().Name}: ClientConnection not started\n");
				return;
			}

			try
			{
				Debug.Log($"{GetType().Name}: Applying Answer\n");

				RTCSessionDescription answer = JsonUtility.FromJson<RTCSessionDescription>(message.data);

				await rtcTransport.clientConnection.ApplyAnswer(answer);
			}
			catch (ArgumentException exception)
			{
				Debug.LogError($"{GetType().Name}: Error parsing RemoteDescription\n{exception}");
				return;
			}
			catch (InvalidOperationException exception)
			{
				Debug.LogError($"{GetType().Name}: Error in RTCConnection operation\n{exception.Message}");
				return;
			}
			catch (Exception exception)
			{
				Debug.LogError($"{GetType().Name}: Error creating Answer\n{exception}");
				return;
			}
		}

		/// <summary>
		/// As a Client, receive a Decline SignalMessage from a Host via Facepunch signaling.
		/// </summary>
		/// <param name="message">The received SignalMessage.</param>
		void ReceiveDecline(SteamId steamID, SignalMessage message)
		{
			NetworkManager.singleton.StopClient();
		}

		/// <summary>
		/// As either Client or Host, receive an ICE Candidate SignalMessage relating to an active RTCConnection.
		/// </summary>
		/// <param name="message">The received SignalMessage.</param>
		void ReceiveIceCandidate(SteamId steamID, SignalMessage message)
		{
			if (rtcTransport.serverConnections != null)
			{
				if (serverSteamIDToId.TryGetValue(steamID, out int connectionKey) && rtcTransport.serverConnections.TryGetValue(connectionKey, out RTCConnection serverConnection))
				{
					try
					{
						RTCIceCandidate candidate = JsonUtility.FromJson<RTCIceCandidate>(message.data);
						serverConnection.AddIceCandidate(candidate);
					}
					catch (Exception exception)
					{
						Debug.LogError($"{GetType().Name}: Server - Error adding ICE Candidate\n{exception}");
					}
				}
				else
				{
					Debug.LogError($"{GetType().Name}: Server - ICE Candidate connection not found\n{steamID}");
				}
			}
			else if (rtcTransport.clientConnection != null)
			{
				try
				{
					RTCIceCandidate candidate = JsonUtility.FromJson<RTCIceCandidate>(message.data);
					rtcTransport.clientConnection.AddIceCandidate(candidate);
				}
				catch (Exception exception)
				{
					Debug.LogError($"{GetType().Name}: Client - Error adding ICE Candidate\n{exception}");
				}
			}
		}
	}
}
