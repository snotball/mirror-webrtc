using System;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

namespace Mirror.WebRTC
{
	public class RTCTransport : Transport
	{
		public RTCSignaler activeSignaler;

		static bool initialized;

		public string[] stunUrls = new string[]
		{
			"stun:stun.stunprotocol.org"
		};

		public RTCConnection.DataChannelReliability[] dataChannels = new RTCConnection.DataChannelReliability[]
		{
			RTCConnection.DataChannelReliability.Reliable,
			RTCConnection.DataChannelReliability.Unreliable
		};

		private void Awake()
		{
			// Initialize WebRTC with software encoding to avoid failure on AMD graphics adapters. We only use data-channels, so this is not important anyway
			if (!initialized)
			{
				Unity.WebRTC.WebRTC.Initialize(EncoderType.Software);
				initialized = true;
			}
		}

		private void OnValidate()
		{
			// Make sure there's at least 1 data channel
			if (dataChannels.Length < 1)
				dataChannels = new RTCConnection.DataChannelReliability[1];
		}

		public override void OnApplicationQuit()
		{
			base.OnApplicationQuit();

			// Clean up after WebRTC
			if (initialized)
			{
				Unity.WebRTC.WebRTC.Dispose();
				initialized = false;
			}
		}

		public override bool Available() => true;

		#region Client
		internal RTCConnection clientConnection;

		public override bool ClientConnected() => clientConnection != null && clientConnection.IsConnected;

		public override void ClientConnect(string address)
		{
			if (serverConnections != null)
			{
				Debug.LogError($"{GetType().Name}: ClientConnect\nServer already started");
				return;
			}

			if (clientConnection != null)
			{
				Debug.LogError($"{GetType().Name}: ClientConnect\nTransport already running as Client");
				return;
			}

			Debug.Log($"{GetType().Name}: ClientConnect\n");

			activeSignaler.Offer(address);
		}

		public override void ClientSend(int channelId, ArraySegment<byte> segment)
		{
			byte[] data = new byte[segment.Count];
			Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
			clientConnection.Send(channelId, data);
		}

		public override void ClientDisconnect()
		{
			if (clientConnection == null)
			{
				Debug.LogError($"{GetType().Name}: ClientDisconnect\nClient not connected");
				return;
			}

			Debug.Log($"{GetType().Name}: ClientDisconnect\nDisconnecting");

			clientConnection.Close();
			clientConnection = null;
		}
		#endregion

		#region Server
		internal int serverNextConnectionID;
		internal Dictionary<int, RTCConnection> serverConnections;

		public event Action OnServerStart;
		public event Action OnServerStop;

		public override Uri ServerUri()
		{
			throw new NotSupportedException();
		}

		public override bool ServerActive() => serverConnections != null;

		public override void ServerStart()
		{
			if (serverConnections != null)
			{
				Debug.LogError($"{GetType().Name}: ServerStart\nServer already started");
				return;
			}

			if (clientConnection != null)
			{
				Debug.LogError($"{GetType().Name}: ServerStart\nTransport already running as Client");
				return;
			}

			Debug.Log($"{GetType().Name}: ServerStart\nStarting Server");

			serverNextConnectionID = 1;
			serverConnections = new Dictionary<int, RTCConnection>();

			OnServerStart?.Invoke();
		}

		public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
		{
			if (serverConnections.TryGetValue(connectionId, out RTCConnection connection))
			{
				byte[] data = new byte[segment.Count];
				Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
				connection.Send(channelId, data);
			}
			else
			{
				Debug.LogError($"{GetType().Name}: ServerSend\nUnknown Connection: {connectionId}");
			}
		}

		public override bool ServerDisconnect(int connectionId)
		{
			if (serverConnections.TryGetValue(connectionId, out RTCConnection connection))
			{
				connection.Close();
				return true;
			}
			else
			{
				Debug.LogError($"{GetType().Name}: ServerDisconnect\nUnknown Connection: {connectionId}");
				return false;
			}
		}

		public override string ServerGetClientAddress(int connectionId)
		{
			if (serverConnections.TryGetValue(connectionId, out RTCConnection connection))
			{
				return "WebRTC";
			}
			else
			{
				Debug.LogError($"{GetType().Name}: ServerGetClientAddress\nUnknown Connection: {connectionId}");
				return null;
			}
		}

		public override void ServerStop()
		{
			if (serverConnections == null)
			{
				Debug.LogError($"{GetType().Name}: ServerStop\nServer not started");
				return;
			}

			Debug.Log($"{GetType().Name}: ServerStop\nStopping server");

			foreach (var connection in serverConnections)
			{
				connection.Value.Close();
			}

			serverConnections = null;
			OnServerStop?.Invoke();
		}
		#endregion

		public override int GetMaxPacketSize(int channelId = Channels.DefaultReliable)
		{
			return 1200;
		}

		public override void Shutdown()
		{
			Debug.Log($"{GetType().Name}: Shutdown\n");

			if (serverConnections != null)
			{
				ServerStop();
			}

			if (clientConnection != null)
			{
				ClientDisconnect();
			}
		}

		public override string ToString()
		{
			return "WebRTC";
		}
	}
}
