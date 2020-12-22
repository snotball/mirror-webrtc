var port = 3000;

var WebSocket = require("ws");
var wss = new WebSocket.Server({ port: port });

var MessageType = {
  OFFER: 0,
  ANSWER: 1,
  DECLINE: 2,
  ICECANDIDATE: 3
};

var connections = [];

wss.on("listening", function() {
  console.log("Server listening on port " + port);
});

wss.on("connection", function(ws, req) {
  console.log("Client connecting...");
  
  // Validate request headers
  if (req.headers["user-agent"] !== "mirror-webrtc") {
    ws.close(4000, "Invalid 'user-agent'");
    return console.error("Invalid 'user-agent'");
  }
  
  if (req.headers["login-id"] == null || req.headers["login-id"].length === 0) {
    ws.close(4001, "Invalid 'login-id'");
    return console.error("Invalid 'login-id'");
  }
  
  if (connections.find(x => x.id == req.headers["login-id"]) != undefined) {
    ws.close(4002, "Occupied 'login-id'");
    return console.error("Occupied 'login-id'");
  }
  
  // Assign "login-id" to WebSocket
  ws.id = req.headers["login-id"];
  
  // Store connection in array
  connections.push(ws);
  console.log("Client connected: '" + ws.id + "'");
  console.log("Connections: " + connections.length);
  
  // On Close
  ws.on("close", function(code, reason) {   
    console.log("Client disconnected: '" + ws.id + "'");
    
    var index = connections.indexOf(ws);
    
    if (index > -1)
      connections.splice(index, 1);
    
    console.log("Connections: " + connections.length);
  });
  
  // On Error
  ws.on("error", function(error) {
    console.error("Error", error);
  });
  
  // On Message
  ws.on("message", function(message) {   
    var msg;
    
    // Validation
    try {
      msg = JSON.parse(message);
      
      if (!msg.hasOwnProperty("type"))
        return console.error("Missing 'type' property");
      else if (!msg.hasOwnProperty("from"))
        return console.error("Missing 'from' property");
      else if (!msg.hasOwnProperty("to"))
        return console.error("Missing 'to' property");
      else if (!msg.hasOwnProperty("data"))
        return console.error("Missing 'data' property");
    }
    catch (e) {
      return console.error("JSON parse error");
    }
    
    if (msg.from == msg.to)
      return console.error("Sending to self");
    
    var msgType = (function(t) {
      for (var k in MessageType) {
        if (MessageType[k] == t)
          return k;
      }
    })(msg.type);
    
    console.log(`Message received - From: ${msg.from} - To: ${msg.to} - Type: ${msgType}`);
    
    var receiver = connections.find(x => x.id == msg.to);
      
    if (receiver == undefined)
      return console.error("Couldn't find receiver: " + msg.to);
      
    receiver.send(message);
  });
});
