using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using UnityEngine;

namespace Mirror.WebRTC
{
	[RequireComponent(typeof(RTCWebSocketSignaler))]
	public class RTCWebSocketSignalerHUD : MonoBehaviour
	{
		public bool showGUI = true;

		RTCWebSocketSignaler webSocketSignaler;
		RTCTransport rtcTransport;

		string loginID = "Host";

		private void Awake()
		{
			webSocketSignaler = GetComponent<RTCWebSocketSignaler>();
			rtcTransport = GetComponent<RTCTransport>();
		}

		private void OnGUI()
		{
			if (!showGUI)
				return;

			GUILayout.BeginArea(new Rect(Screen.width - 150, 0, 150, Screen.height));

			GUILayout.Label($"WebSocket: {webSocketSignaler.WebSocketState}");

			GUI.enabled = webSocketSignaler.WebSocketState == NativeWebSocket.WebSocketState.Closed;
			loginID = GUILayout.TextField(loginID);

			GUILayout.BeginHorizontal();

			if (GUILayout.Button("Connect"))
				webSocketSignaler.Connect(loginID);

			GUI.enabled = webSocketSignaler.WebSocketState == NativeWebSocket.WebSocketState.Open;
			if (GUILayout.Button("Close"))
				webSocketSignaler.Close();

			GUILayout.EndHorizontal();

			GUI.enabled = webSocketSignaler.WebSocketState == NativeWebSocket.WebSocketState.Open && !NetworkServer.active && !NetworkClient.active;

			if (rtcTransport.clientConnection != null)
			{
				GUILayout.Label($"Client");
				GUILayout.Label($"{rtcTransport.clientConnection}");
			}
			else if (rtcTransport.serverConnections != null)
			{
				GUILayout.Label($"Server");

				foreach (var kvp in rtcTransport.serverConnections)
				{
					GUILayout.Label($"({kvp.Key}) {kvp.Value}");
				}
			}

			GUILayout.EndArea();
		}
	}
}
