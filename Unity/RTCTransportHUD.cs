using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mirror.WebRTC
{
	[RequireComponent(typeof(RTCTransport))]
	public class RTCTransportHUD : MonoBehaviour
	{
		public bool showGUI = true;
		public Vector2Int offset = new Vector2Int(10, 120);

		RTCSignaler[] signalers;
		string[] toolbarStrings;
		int toolbarSelected = -1;

		RTCTransport rtcTransport;

		private void Awake()
		{
			rtcTransport = GetComponent<RTCTransport>();

			signalers = GetComponents<RTCSignaler>();
			toolbarSelected = Array.IndexOf(signalers, rtcTransport.activeSignaler);
			toolbarStrings = signalers.Select(x => " " + x.GetType().Name).ToArray();
		}

		private void OnGUI()
		{
			if (!showGUI)
				return;

			GUILayout.Space(offset.y);
			GUILayout.BeginHorizontal();
			GUILayout.Space(offset.x);

			GUILayout.BeginVertical();

			if (!NetworkClient.isConnected && !NetworkServer.active)
			{
				GUILayout.Label("Join as Client via:");
				toolbarSelected = GUILayout.SelectionGrid(toolbarSelected, toolbarStrings, 1, "toggle");
				rtcTransport.activeSignaler = signalers[toolbarSelected];
			}
			else
			{
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
			}

			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
		}
	}
}
