using Steamworks;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using UnityEngine;

namespace Mirror.WebRTC
{
	[RequireComponent(typeof(RTCFacepunchSignaler))]
	public class RTCFacepunchSignalerHUD : MonoBehaviour
	{
		public bool showGUI = true;
		public Vector2Int offset = new Vector2Int(-350, 0);

		bool toggle = true;

		RTCFacepunchSignaler facepunchSignaler;

		private void Awake()
		{
			facepunchSignaler = GetComponent<RTCFacepunchSignaler>();
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
					200,
					Screen.height
				)
			);

			toggle = GUILayout.Toggle(toggle, $" {facepunchSignaler.GetType().Name}");

			if (toggle)
			{
				GUILayout.Label($"Steam: {(facepunchSignaler.IsConnected ? "Connected" : "Disconnected")}");
				GUILayout.Label($"SteamID: {(SteamClient.IsValid && SteamClient.IsLoggedOn ? SteamClient.SteamId : default)}");

				GUI.enabled = SteamClient.IsValid && SteamClient.IsLoggedOn;

				if (GUILayout.Button("Copy SteamID", GUILayout.ExpandWidth(false)))
				{
					GUIUtility.systemCopyBuffer = SteamClient.SteamId.ToString();
					Debug.Log($"{GetType().Name}: SteamID copied to clipboard\n{GUIUtility.systemCopyBuffer}");
				}
			}

			GUILayout.EndArea();
		}
	}
}
