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
		public Vector2Int offset = new Vector2Int(-165, 0);

		string loginID = "Host";
		bool toggle = true;

		RTCWebSocketSignaler webSocketSignaler;

		private void Awake()
		{
			webSocketSignaler = GetComponent<RTCWebSocketSignaler>();

#if UNITY_EDITOR
			if (Application.dataPath.ToLower().Contains("symlink"))
				loginID = "Client";
#endif
		}

		private void OnGUI()
		{
			if (!showGUI)
				return;

			GUILayout.BeginArea
			(
				new Rect
				(
					offset.x + (offset.x < 0 ? Screen.width : 0),
					offset.y + (offset.y < 0 ? Screen.height : 0),
					160,
					Screen.height
				)
			);

			toggle = GUILayout.Toggle(toggle, $" {webSocketSignaler.GetType().Name}");

			if (toggle)
			{
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
			}

			GUILayout.EndArea();
		}
	}
}
