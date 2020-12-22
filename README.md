# mirror-webrtc

A WebRTC based Transport for Mirror Networking that utilizes data channels for reliable and unreliable network communication.

![WebRTC](https://upload.wikimedia.org/wikipedia/commons/thumb/6/68/WebRTC_Logo.svg/150px-WebRTC_Logo.svg.png "WebRTC")

The purpose of this Transport is to achieve a method of connectivity that does not require port forwarding by the game host, nor any kind of expensive bandwidth-thirsty hosting by the developer.

With an abundance of freely available STUN servers around the internet and sufficiently featured free NodeJS hosting for Signaling, we can in fact connect players directly quite easily. Simply put, the STUN server figures out _how_ a peer can be contacted behind an intricate web of routers while the Signal Server _exchanges_ that information between clients, so they can hook up. All subsequent traffic is via direct UDP messaging.

Just like classic NAT punchthrough, this method _can_ fail on complex networks such as those of big companies or universities. And while WebRTC _does_ support fallback to TURN servers where all traffic is directed through online relays, that goes against the very purpose of this project and is therefore not implemented.

#### Dependencies
- [Mirror Networking v26.2.2](https://github.com/vis2k/Mirror/releases/tag/v26.2.2)
- [com.unity.webrtc@2.2](https://docs.unity3d.com/Packages/com.unity.webrtc@2.2/manual/index.html)
- [NativeWebSocket](https://github.com/endel/NativeWebSocket)

## Getting started

### Signal Server
- Host the [Signal Server](./SignalServer/server.js).

It is written in pure JavaScript for use in a NodeJS environment. It's built on the 'ws' WebSocket library, and it _should_ function on every host that supports WebSockets. [Glitch.com](https://glitch.com/) was used during development - another free alternative could have been [Heroku](https://heroku.com).

### Unity
- Install all dependencies into your Unity project via Github or Package Manager where applicable.
- Import all classes found in the Unity folder to your Unity project.
- Add [RTCTransport](./Unity/RTCTransport.cs) to the same GameObject as your NetworkManager, and assign it as the active Transport. You can adjust STUN URLs or Data Channels if need be.
- Also, add [RTCWebSocketSignaler](./Unity/RTCWebSocketSignaler.cs) to the same GameObject and set the Server URL to point to your hosted Signal Server.

### Testing it out
For testing purposes you can add both a NetworkManagerHUD and a [RTCWebSocketSignalerHUD](./Unity/RTCWebSocketSignalerHUD.cs) to the NetworkManager GameObject.

Once you press PLAY, you'll see a menu in the upper right corner with which you can connect to the Signal Server via WebSocket. Enter your desired unique ID, then click Connect. If the ID is already in use, you'll be disconnected immediately, as each client _must_ have a unique ID for signaling to work. Note: Glitch puts services to sleep if unused for longer periods of time, so it might take a couple of seconds for the service to wake up.

Once connected, you can either Start as a Host or Join as a Client as per usual using the NetworkManagerHUD. As a Client you enter the unique ID of the Host you wish to join instead of an IP address.

## Thoughts
Signaling can be done in any way you desire. This example employs a custom WebSocket solution, but it could also be implemented via Steam P2P or the Epic equivalent. Ideally, multiple Signalers could be used simultaneously to support cross-platform play and broaden the player base.

## Platform support
At the time of writing, this project has only been tested between Windows standalone clients, but it should theoretically work within the intersection of its 3 dependencies.

## Disclaimer
This is a rudimentary implementation at best and should only serve as inspiration or reference, as it does _not_ have any security layers whatsoever at this point.
