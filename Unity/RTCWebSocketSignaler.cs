using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using Unity.WebRTC;

namespace Mirror.WebRTC
{
	[RequireComponent(typeof(RTCTransport))]
	public class RTCWebSocketSignaler : MonoBehaviour
	{
		[Serializable]
		class SignalMessage
		{
			public enum Type { Offer, Answer, Decline, IceCandidate }
			public Type type;
			public string from;
			public string to;
			public string data;
		}

		/// <summary>
		/// "user-agent" request header
		/// </summary>
		public string headerUserAgent = "mirror-webrtc";

		/// <summary>
		/// URL to the signaling server (e.g. "wss://mysignalserver.glitch.me")
		/// </summary>
		public string serverURL;

		public WebSocket WebSocket { get; private set; }
		RTCTransport rtcTransport;
		Dictionary<string, int> serverFromToId;

		public WebSocketState WebSocketState => WebSocket == null ? WebSocketState.Closed : WebSocket.State;
		public string LoginID { get; private set; }

		private void Awake()
		{
			rtcTransport = GetComponent<RTCTransport>();
			rtcTransport.OnServerStart += () => serverFromToId = new Dictionary<string, int>();
			rtcTransport.OnServerStop += () => serverFromToId = null;

			rtcTransport.OnClientConnect += (address) => Offer(address);
		}

		void Update()
		{
#if !UNITY_WEBGL || UNITY_EDITOR
			if (WebSocket != null)
				WebSocket.DispatchMessageQueue();
#endif
		}

		private void OnDestroy()
		{
			Close();
		}

		/// <summary>
		/// Close the WebSocket connection.
		/// </summary>
		public async void Close()
		{
			if (WebSocket == null)
				return;

			Debug.Log($"{GetType().Name}: Close\n");

			await WebSocket.Close();
			WebSocket = null;
			LoginID = null;
		}

		/// <summary>
		/// Connect to the WebSocket Signaling Server with a unique LoginID.
		/// If LoginID is already in use on the server, the connection will be closed.
		/// </summary>
		/// <param name="id">A unique LoginID for signaling identification.</param>
		public async void Connect(string id)
		{
			if (WebSocket != null)
				return;

			Debug.Log($"{GetType().Name}: Connect\n");

			Dictionary<string, string> requestHeaders = new Dictionary<string, string>
			{
				{ "user-agent", headerUserAgent },
				{ "login-id", id }
			};

			WebSocket = new WebSocket(serverURL, requestHeaders);

			WebSocket.OnOpen += () =>
			{
				Debug.Log($"{GetType().Name}: OnOpen\n'{id}'");
				LoginID = id;
			};

			WebSocket.OnError += (error) =>
			{
				Debug.LogError($"{GetType().Name}: OnError\n{error}");
				Close();
			};

			WebSocket.OnClose += (closeCode) =>
			{
				Debug.Log($"{GetType().Name}: OnClose\n'{closeCode}'");
				Close();
			};

			WebSocket.OnMessage += (bytes) =>
			{
				try
				{
					string json = Encoding.UTF8.GetString(bytes);
					SignalMessage message = JsonUtility.FromJson<SignalMessage>(json);

					Debug.Log($"{GetType().Name}: Received {message.type}\nFrom: {message.from}");

					if (message.type == SignalMessage.Type.Offer)
						ReceiveOffer(message);
					else if (message.type == SignalMessage.Type.Answer)
						ReceiveAnswer(message);
					else if (message.type == SignalMessage.Type.Decline)
						ReceiveDecline(message);
					else if (message.type == SignalMessage.Type.IceCandidate)
						ReceiveIceCandidate(message);
				}
				catch (Exception exception)
				{
					Debug.LogError($"{GetType().Name}: Error parsing received message\n{exception}");
				}
			};

			await WebSocket.Connect();
		}

		/// <summary>
		/// Send a fully decorated SignalMessage to someone on the server.
		/// </summary>
		/// <param name="message">The SignalMessage to send.</param>
		async void Send(SignalMessage message)
		{
			try
			{
				Debug.Log($"{GetType().Name}: Sending {message.type}\nTo: {message.to}");

				string json = JsonUtility.ToJson(message);
				byte[] bytes = Encoding.UTF8.GetBytes(json);

				await WebSocket.Send(bytes);
			}
			catch (Exception exception)
			{
				Debug.LogError($"{GetType().Name}: Error sending message\n{exception}");
			}
		}

		/// <summary>
		/// As a Client, offer to join a Host via signaling server.
		/// </summary>
		/// <param name="recipientID">WebSocket LoginID of Host we wish to join.</param>
		public async void Offer(string recipientID)
		{
			if (string.IsNullOrEmpty(LoginID))
			{
				Debug.LogError($"{GetType().Name}: Not logged in\n");
				return;
			}

			if (string.IsNullOrEmpty(recipientID) || string.IsNullOrWhiteSpace(recipientID))
			{
				Debug.LogError($"{GetType().Name}: Invalid RecipientID\n{recipientID}");
				return;
			}

			try
			{
				Debug.Log($"{GetType().Name}: Creating Offer\n{recipientID}");

				RTCConnection connection = await RTCConnection.CreateOffer(rtcTransport, (iceCandidate) =>
				{
					Send(new SignalMessage
					{
						type = SignalMessage.Type.IceCandidate,
						from = LoginID,
						to = recipientID,
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

				Send(new SignalMessage
				{
					type = SignalMessage.Type.Offer,
					from = LoginID,
					to = recipientID,
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
		/// As a Host, receive an Offer SignalMessage from a Client via the signaling server.
		/// </summary>
		/// <param name="message">The received SignalMessage.</param>
		async void ReceiveOffer(SignalMessage message)
		{
			if (!rtcTransport.ServerActive())
			{
				Debug.LogError($"{GetType().Name}: Server not active\n");

				Send(new SignalMessage
				{
					type = SignalMessage.Type.Decline,
					from = LoginID,
					to = message.from,
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
					Send(new SignalMessage
					{
						type = SignalMessage.Type.IceCandidate,
						from = LoginID,
						to = message.from,
						data = JsonUtility.ToJson(iceCandidate)
					});
				}, remoteDescription);

				int connectionID = rtcTransport.serverNextConnectionID++;
				serverFromToId.Add(message.from, connectionID);
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
					serverFromToId.Remove(message.from);
					rtcTransport.serverConnections.Remove(connectionID);
					rtcTransport.OnServerDisconnected.Invoke(connectionID);
				};

				connection.DataChannel_OnMessage += (channel, bytes) =>
				{
					rtcTransport.OnServerDataReceived.Invoke(connectionID, new ArraySegment<byte>(bytes), channel);
				};

				Send(new SignalMessage
				{
					type = SignalMessage.Type.Answer,
					from = LoginID,
					to = message.from,
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
		/// As a Client, receive an Answer SignalMessage from a Host via the signaling server.
		/// </summary>
		/// <param name="message">The received SignalMessage.</param>
		async void ReceiveAnswer(SignalMessage message)
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
		/// As a Client, receive a Decline SignalMessage from a Host via the signaling server.
		/// </summary>
		/// <param name="message">The received SignalMessage.</param>
		void ReceiveDecline(SignalMessage message)
		{
			NetworkManager.singleton.StopClient();
		}

		/// <summary>
		/// As either Client or Host, receive an ICE Candidate SignalMessage relating to an active RTCConnection.
		/// </summary>
		/// <param name="message">The received SignalMessage.</param>
		void ReceiveIceCandidate(SignalMessage message)
		{
			if (rtcTransport.serverConnections != null)
			{
				if (serverFromToId.TryGetValue(message.from, out int connectionKey) && rtcTransport.serverConnections.TryGetValue(connectionKey, out RTCConnection serverConnection))
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
					Debug.LogError($"{GetType().Name}: Server - ICE Candidate connection not found\n{message.from}");
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
