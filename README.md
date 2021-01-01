# mirror-webrtc

A transport for Mirror Networking that utilizes WebRTC data channels for reliable and unreliable network communication.

![WebRTC](https://upload.wikimedia.org/wikipedia/commons/thumb/6/68/WebRTC_Logo.svg/150px-WebRTC_Logo.svg.png "WebRTC")

The purpose of this transport is to provide an alternate method of online connectivity that does not require port forwarding by the game host.

With an abundance of freely available STUN servers around the internet and total freedom in how to implement signaling, we can connect players quite easily without port forwarding using WebRTC. In simple terms, a STUN server figures out _how_ peers can be contacted on the internet while the signaler _exchanges_ this information between them, so they can connect to each other. All subsequent traffic is via direct UDP messaging between the clients.

Just like classic NAT hole punching, the STUN process _can_ fail on complex networks such as those of big companies or universities. While WebRTC _does_ in fact support fallback to TURN servers where all subsequent traffic is directed through online relays, that typically requires paid hosting and is beyond the scope of this project for now.

## Transport

[RTCTransport](./Unity/RTCTransport.cs) should be added as a neighbor to the [NetworkManager](https://mirror-networking.com/docs/Articles/Components/NetworkManager.html) Component and be assigned as its active transport. It bears a reference to an active signaler which will be utilized when connecting to Hosts via NetworkManager.StartClient() and in turn the NetworkManager.networkAddress field. Furthermore, it exposes a string array of URLs for which STUN servers to use. Lastly, an array of data channels and their reliability type.

##### Dependencies

- [Mirror Networking v26.2.2](https://github.com/vis2k/Mirror/releases/tag/v26.2.2)
- [com.unity.webrtc@2.2](https://docs.unity3d.com/Packages/com.unity.webrtc@2.2/manual/index.html)

##### HUD

[RTCTransportHUD](./Unity/RTCTransportHUD.cs) is an optional developer tool like [NetworkManagerHUD](https://mirror-networking.com/docs/Articles/Components/NetworkManagerHUD.html) that aids in development. When unconnected, it shows an array of radio buttons for setting the active signaler with which to join hosts as a client. When connected, it shows current client/server connections and their data channel status.

## Signalers

With WebRTC, signaling can be done in any way you desire; it can be any arbitrary method of sending a handfuld of messages back and forth between peers, so they can establish [RTCConnections](./Unity/RTCConnection.cs). When joining a host, the client _offers_ his connection information to the host via signaling, which the host decides to _answer_ with his connection information.

[RTCSignaler](./Unity/RTCSignaler.cs) is an abstract base class that all signalers must inherit from to integrate with the transport. Signalers should be added as neighbors to the transport Component.

In this implementation, multiple signalers can function in parallel allowing for better cross-platform support. The following signalers are optional and can be picked and chosen at will. Because established connections are separate from the signaling process itself, signaling services can be started or stopped independently of game sessions.

### WebSocket

[RTCWebSocketSignaler](./Unity/RTCWebSocketSignaler.cs) is a signaler that uses WebSockets to communicate with an online service for exchanging signal messages between peers. Clients connect to this service using unique IDs, which are then used by clients when they _offer_ to join hosts. The component exposes a User Agent field, that is required for authorization by the WebSocket server. Finally, a URL to the location of the WebSocket server.

[SignalServer](./SignalServer/server.js) is a JavaScript example of a rudimentary WebSocket server for hosting in a NodeJS environments. It is built on the 'ws' library and should function on every host that supports WebSockets. During development, [Glitch](https://glitch.com/) was used - another free alternative could have been [Heroku](https://heroku.com). Feel free to test using the provided URL, though the server's uptime cannot be guaranteed. Your game should have its own server, of course.

##### Dependencies

- [NativeWebSocket](https://github.com/endel/NativeWebSocket)

##### HUD

[RTCWebSocketSignalerHUD](./Unity/RTCWebSocketSignalerHUD.cs) is a toggleable user interface for connection to the WebSocket signal server. It shows its current status and has an input field for which ID to connect with. It is this ID that clients must use when _offering_ to join a host via WebSocket signaling.

### Facepunch.Steamworks

[RTCFacepunchSignaler](./Unity/RTCFacepunchSignaler.cs) is a signaler that uses the Facepunch.Steamworks library to exchange signal messages via Steamworks P2P messaging directly between Steam users. It has a Steam App ID field which should be set to that of your game, but for testing purposes the default of 480 (Spacewar) can be used. Lastly, a toggle for allowing P2P packets to be relayed, should Steam NAT fail.

When the _offer_ is sent, the message will typically have a brief delay because of NAT.

##### Dependencies

- [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks)

##### HUD

[RTCFacepunchSignalerHUD](./Unity/RTCFacepunchSignalerHUD.cs) is a toggleable user interface for connection to Steamworks. It shows its current status, the users' SteamID, and a button for copying said ID to clipboard for ease of use. It is this ID that clients must use when _offering_ to join a host via Facepunch.Steamworks signaling.

## Platform support
At the time of writing, this project has only been tested between Windows standalone clients, but it should theoretically work within the intersection of its dependencies. Obviously, some signalers only work on certain platforms, and should be stripped from the project.

## Disclaimer
This is a rudimentary implementation at best and should only serve as inspiration or reference, as it does _not_ have any security layers whatsoever at this point.
