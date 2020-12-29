using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

namespace Mirror.WebRTC
{
	public class RTCConnection
	{
		public enum DataChannelReliability { Reliable, Unreliable }

		RTCPeerConnection peerConnection;
		RTCDataChannel[] dataChannels;

		public event Action DataChannels_OnOpen;
		public event Action DataChannels_OnClose;
		public event Action<int, byte[]> DataChannel_OnMessage;

		bool _channelsOpen;
		public bool ChannelsOpen
		{
			get => _channelsOpen;
			private set
			{
				if (_channelsOpen == value)
					return;

				_channelsOpen = value;

				if (_channelsOpen)
					DataChannels_OnOpen?.Invoke();
				else
					DataChannels_OnClose?.Invoke();
			}
		}

		void RefreshChannelsOpen() => ChannelsOpen = dataChannels.All(x => x?.ReadyState == RTCDataChannelState.Open);

		public bool IsConnected => peerConnection?.ConnectionState == RTCPeerConnectionState.Connected && ChannelsOpen;

		public RTCSessionDescription LocalDescription => peerConnection.LocalDescription;

		public static async Task<RTCConnection> CreateOffer(RTCTransport rtcTransport, DelegateOnIceCandidate onIceCandidate)
		{
			var connection = new RTCConnection();

			// PeerConnection
			var configuration = default(RTCConfiguration);
			configuration.iceServers = new RTCIceServer[]
			{
				new RTCIceServer { urls = rtcTransport.stunUrls }
			};

			connection.peerConnection = new RTCPeerConnection(ref configuration);
			connection.peerConnection.OnIceCandidate = async (iceCandidate) =>
			{
				await Task.Run(() => { while (connection.peerConnection.SignalingState == RTCSignalingState.HaveLocalOffer) { } });
				onIceCandidate.Invoke(iceCandidate);
			};

			// DataChannels
			connection.dataChannels = new RTCDataChannel[rtcTransport.dataChannels.Length];

			for (int i = 0; i < connection.dataChannels.Length; i++)
			{
				var dataChannelInit = new RTCDataChannelInit(rtcTransport.dataChannels[i] == DataChannelReliability.Reliable);
				connection.dataChannels[i] = connection.peerConnection.CreateDataChannel(i.ToString(), ref dataChannelInit);
				connection.dataChannels[i].OnOpen = () => connection.RefreshChannelsOpen();
				connection.dataChannels[i].OnClose = () => connection.RefreshChannelsOpen();
				connection.dataChannels[i].OnMessage = (bytes) => connection.DataChannel_OnMessage?.Invoke(i, bytes);
			}

			// Offer
			var offerOptions = new RTCOfferOptions()
			{
				iceRestart = false,
				offerToReceiveAudio = false,
				offerToReceiveVideo = false
			};
			var offerOperation = connection.peerConnection.CreateOffer(ref offerOptions);

			await Task.Run(() => { while (offerOperation.keepWaiting) { } });

			if (offerOperation.IsError)
			{
				throw new InvalidOperationException($"{offerOperation.Error.errorType} - {offerOperation.Error.message}");
			}

			// LocalDescription
			var offerDesc = offerOperation.Desc;
			var localDescriptionOperation = connection.peerConnection.SetLocalDescription(ref offerDesc);

			await Task.Run(() => { while (localDescriptionOperation.keepWaiting) { } });

			if (localDescriptionOperation.IsError)
			{
				throw new InvalidOperationException($"{localDescriptionOperation.Error.errorType} - {localDescriptionOperation.Error.message}");
			}

			// Success
			return connection;
		}

		public static async Task<RTCConnection> CreateAnswer(RTCTransport rtcTransport, DelegateOnIceCandidate onIceCandidate, RTCSessionDescription remoteDescription)
		{
			var connection = new RTCConnection();

			// PeerConnection
			var configuration = default(RTCConfiguration);
			configuration.iceServers = new RTCIceServer[]
			{
				new RTCIceServer { urls = rtcTransport.stunUrls }
			};

			connection.peerConnection = new RTCPeerConnection(ref configuration);
			connection.peerConnection.OnIceCandidate = onIceCandidate;

			// DataChannels
			connection.dataChannels = new RTCDataChannel[rtcTransport.dataChannels.Length];

			connection.peerConnection.OnDataChannel = (channel) =>
			{
				Debug.Log($"{connection.GetType().Name}: OnDataChannel\n{channel.Id} - '{channel.Label}'");

				if (int.TryParse(channel.Label, out int i))
				{
					connection.dataChannels[i] = channel;
					connection.dataChannels[i].OnOpen = () => connection.RefreshChannelsOpen();
					connection.dataChannels[i].OnClose = () => connection.RefreshChannelsOpen();
					connection.dataChannels[i].OnMessage = (bytes) => connection.DataChannel_OnMessage?.Invoke(i, bytes);
				}
				else
				{
					throw new FormatException($"Invalid DataChannel.Label format.");
				}

				connection.RefreshChannelsOpen();
			};

			// RemoteDescription
			var remoteDescriptionOperation = connection.peerConnection.SetRemoteDescription(ref remoteDescription);

			await Task.Run(() => { while (remoteDescriptionOperation.keepWaiting) { } });

			if (remoteDescriptionOperation.IsError)
			{
				throw new InvalidOperationException($"{remoteDescriptionOperation.Error.errorType} - {remoteDescriptionOperation.Error.message}");
			}

			// Answer
			var answerOptions = new RTCAnswerOptions();
			var answerOperation = connection.peerConnection.CreateAnswer(ref answerOptions);

			await Task.Run(() => { while (answerOperation.keepWaiting) { } });

			if (answerOperation.IsError)
			{
				throw new InvalidOperationException($"{answerOperation.Error.errorType} - {answerOperation.Error.message}");
			}

			// LocalDescription
			var answerDesc = answerOperation.Desc;
			var localDescriptionOperation = connection.peerConnection.SetLocalDescription(ref answerDesc);

			await Task.Run(() => { while (localDescriptionOperation.keepWaiting) { } });

			if (localDescriptionOperation.IsError)
			{
				throw new InvalidOperationException($"{localDescriptionOperation.Error.errorType} - {localDescriptionOperation.Error.message}");
			}

			// Success
			return connection;
		}

		public async Task ApplyAnswer(RTCSessionDescription answer)
		{
			var remoteDescriptionOperation = peerConnection.SetRemoteDescription(ref answer);

			await Task.Run(() => { while (remoteDescriptionOperation.keepWaiting) { } });

			if (remoteDescriptionOperation.IsError)
			{
				throw new InvalidOperationException($"{remoteDescriptionOperation.Error.errorType} - {remoteDescriptionOperation.Error.message}");
			}
		}

		public void AddIceCandidate(RTCIceCandidate iceCandidate)
		{
			peerConnection.AddIceCandidate(ref iceCandidate);
		}

		public void Send(int channel, byte[] bytes)
		{
			dataChannels[channel].Send(bytes);
		}

		public void Close()
		{
			for (int i = 0; i < dataChannels.Length; i++)
			{
				dataChannels[i]?.Close();
			}

			peerConnection?.Close();
			peerConnection?.Dispose();
		}

		public override string ToString()
		{
			var toString = "RTCConnection";

			for (int i = 0; i < dataChannels.Length; i++)
			{
				toString += $"\n- {i}: {dataChannels[i]?.ReadyState}";
			}

			return toString;
		}
	}
}
