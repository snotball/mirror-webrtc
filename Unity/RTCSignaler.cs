using System;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

namespace Mirror.WebRTC
{
	public abstract class RTCSignaler : MonoBehaviour
	{
		public abstract void Offer(string address);
	}
}
