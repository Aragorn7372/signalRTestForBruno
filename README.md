# signalRTestForBruno

[English](#english) | [Español](#español)

---

## English

### What is SignalR?

SignalR is a library for ASP.NET Core that simplifies adding **real-time web functionality** to applications. Real-time means the server can push content to connected clients instantly, without the client having to ask for it (polling).

**How does it work?** SignalR automatically chooses the best transport method available:
- **WebSockets** (preferred) — full-duplex, low-latency communication
- **Server-Sent Events (SSE)** — one-way server-to-client streaming
- **Long Polling** — fallback for older browsers

SignalR handles all the transport negotiation and falls back automatically, so you don't have to worry about it.

---

### Project Overview

This is a **SignalR test server** built with ASP.NET Core (.NET 10). It exposes a single hub at `/hub` and provides:

- **JWT authentication** with a built-in `/token` endpoint to generate tokens for testing
- **Public broadcast** — send a message to every connected client
- **Named groups** — join/leave rooms and send messages to specific groups
- **Private messaging** — send a message directly to a specific connection
- **Connected user tracking** — see who is online
- **Connection lifecycle** — receive notifications when you connect/disconnect

This server was designed to be used with the [Bruno](https://www.usebruno.com/) API client (or any SignalR-capable client like a browser, Postman, or a .NET console app).

---

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A SignalR client (browser, Bruno, Postman, etc.)

---

### How to Run

```bash
cd signalRTestForBruno/signalRTestForBruno
dotnet run
```

The server starts on `http://localhost:5246` (or `https://localhost:7238`).

---

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `/` | GET | Health check → returns `"SignalR server OK"` |
| `/token` | GET | Generates a JWT with random user info (name, role, favorite color) |
| `/hub` | WebSocket | SignalR Hub endpoint |

---

### Authentication Flow

1. Call `GET /token` to get a JWT
2. When connecting to SignalR at `/hub`, pass the token as `?access_token=<jwt>` in the query string
3. The server validates the token and populates `Context.User` with the claims

> **Why the query string?** WebSocket connections cannot set custom HTTP headers (like `Authorization`), so SignalR uses the `access_token` query parameter as a convention for passing authentication tokens.

---

### Hub Methods (Client → Server)

These are methods the **client calls** on the server:

| Method | Parameters | Description | SignalR Concept |
|--------|-----------|-------------|-----------------|
| `Broadcast` | `message` (string) | Sends a message to **all** connected clients | `Clients.All` |
| `JoinGroup` | `groupName` (string) | The caller joins a named group | `Groups.AddToGroupAsync` |
| `LeaveGroup` | `groupName` (string) | The caller leaves a named group | `Groups.RemoveFromGroupAsync` |
| `GroupChat` | `groupName, message` | Sends a message to **all members** of a group | `Clients.Group` |
| `IndividualChat` | `targetConnectionId, message` | Sends a private message to a specific user | `Clients.Client` |
| `GetConnectedUsers` | _(none)_ | Gets the list of all currently connected users | `Caller` response |

---

### Server Events (Server → Client)

These are events the **server sends** to clients. Your client code must listen for them:

| Event Name | Payload | Triggered By |
|-----------|---------|-------------|
| `ReceiveMessage` | `message, connectionId, userName` | `Broadcast` |
| `ReceiveGroupJoined` | `groupName` | `JoinGroup` |
| `ReceiveGroupLeft` | `groupName` | `LeaveGroup` |
| `ReceiveGroupMessage` | `groupName, message, connectionId, userName` | `GroupChat` |
| `ReceivePrivateMessage` | `message, fromConnectionId, userName` | `IndividualChat` (on target) |
| `ReceivePrivateMessageSent` | `message, toConnectionId, userName` | `IndividualChat` (on caller) |
| `ReceiveError` | `errorMessage` | `IndividualChat` when target not found |
| `ReceiveConnectedUsers` | `[ConnectedUserInfo]` | `GetConnectedUsers` |
| `OnConnected` | `connectionId, tokenInfo` | Automatic on successful connection |

---

### Step-by-Step Testing Guide

#### 1. Get a token

```bash
curl http://localhost:5246/token
```

This returns something like:
```json
{
  "token": "eyJhbGciOiJIUzI1NiJ9...",
  "expiresAt": "2026-07-07T...",
  "claims": {
    "sub": "a1b2c3d4-...",
    "name": "Ana Martínez",
    "role": "admin",
    "favoriteColor": "#3357FF"
  }
}
```

#### 2. Connect to the hub

Using a SignalR client, connect to `http://localhost:5246/hub?access_token=<your_token>`.

#### 3. Try the features

- Call `Broadcast("Hello everyone!")` → all clients receive `ReceiveMessage`
- Call `JoinGroup("room1")` → you receive `ReceiveGroupJoined("room1")`
- Call `GroupChat("room1", "Hi group!")` → all group members receive `ReceiveGroupMessage`
- Call `IndividualChat("<other_connection_id>", "Hey!")` → that client receives `ReceivePrivateMessage`
- Call `GetConnectedUsers()` → you receive `ReceiveConnectedUsers([...])`

---

### Full JavaScript Client Example

Create an HTML file and open it in a browser to test the server interactively:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>SignalR Test Client</title>
    <style>
        body { font-family: Arial, sans-serif; max-width: 800px; margin: 20px auto; padding: 0 20px; }
        #log { background: #f5f5f5; border: 1px solid #ccc; height: 300px; overflow-y: auto; padding: 10px; font-size: 13px; }
        input, button { padding: 6px 10px; margin: 3px; }
        .msg { margin: 4px 0; }
        .error { color: red; }
        .info { color: green; }
    </style>
</head>
<body>
    <h1>SignalR Test Client</h1>

    <div>
        <button id="btnConnect">1. Get Token & Connect</button>
        <input id="txtMessage" placeholder="Message" size="30">
        <button id="btnBroadcast">2. Broadcast</button>
    </div>

    <div>
        <input id="txtGroup" placeholder="Group name" size="15">
        <button id="btnJoinGroup">Join Group</button>
        <button id="btnLeaveGroup">Leave Group</button>
        <input id="txtGroupMsg" placeholder="Group message" size="20">
        <button id="btnGroupChat">Group Chat</button>
    </div>

    <div>
        <input id="txtTargetId" placeholder="Target connection ID" size="25">
        <input id="txtPrivateMsg" placeholder="Private message" size="20">
        <button id="btnPrivateChat">Private Chat</button>
    </div>

    <div>
        <button id="btnGetUsers">Get Connected Users</button>
        <span id="connectionStatus" style="margin-left:15px;color:gray;">Not connected</span>
        <span id="myId" style="margin-left:15px;color:gray;"></span>
    </div>

    <h3>Log</h3>
    <div id="log"></div>

    <!-- SignalR JavaScript client library -->
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>

    <script>
        let connection = null;
        let myConnectionId = null;
        let token = null;

        function log(msg, type) {
            const el = document.getElementById('log');
            const div = document.createElement('div');
            div.className = 'msg' + (type ? ' ' + type : '');
            div.textContent = `[${new Date().toLocaleTimeString()}] ${msg}`;
            el.appendChild(div);
            el.scrollTop = el.scrollHeight;
        }

        // Step 1: Get token and connect
        document.getElementById('btnConnect').onclick = async () => {
            try {
                // 1. Get JWT token from the server
                const resp = await fetch('http://localhost:5246/token');
                const data = await resp.json();
                token = data.token;
                log(`Token obtained for ${data.claims.name} (${data.claims.role})`, 'info');

                // 2. Create the SignalR connection
                //    The token is passed as "access_token" in the query string.
                //    This is the standard SignalR way to pass auth tokens
                //    because WebSockets don't support custom HTTP headers.
                connection = new signalR.HubConnectionBuilder()
                    .withUrl(`http://localhost:5246/hub?access_token=${token}`)
                    .configureLogging(signalR.LogLevel.Information)
                    .build();

                // 3. Register event handlers (server → client)

                // Called when broadcast messages arrive
                connection.on('ReceiveMessage', (message, connectionId, userName) => {
                    log(`[Broadcast from ${userName || connectionId}] ${message}`);
                });

                // Called after successfully joining a group
                connection.on('ReceiveGroupJoined', (groupName) => {
                    log(`Joined group: ${groupName}`, 'info');
                });

                // Called after leaving a group
                connection.on('ReceiveGroupLeft', (groupName) => {
                    log(`Left group: ${groupName}`, 'info');
                });

                // Called when a group message is received
                connection.on('ReceiveGroupMessage', (groupName, message, connectionId, userName) => {
                    log(`[Group:${groupName} from ${userName || connectionId}] ${message}`);
                });

                // Called when someone sends us a private message
                connection.on('ReceivePrivateMessage', (message, fromConnectionId, userName) => {
                    log(`[Private from ${userName || fromConnectionId}] ${message}`);
                });

                // Confirmation that our private message was delivered
                connection.on('ReceivePrivateMessageSent', (message, toConnectionId, userName) => {
                    log(`[Private sent to ${userName || toConnectionId}] ${message}`, 'info');
                });

                // Called when an error occurs (e.g., target user not found)
                connection.on('ReceiveError', (errorMessage) => {
                    log(`ERROR: ${errorMessage}`, 'error');
                });

                // Called with the list of connected users
                connection.on('ReceiveConnectedUsers', (users) => {
                    log(`Connected users (${users.length}):`, 'info');
                    users.forEach(u => log(`  • ${u.userName || 'anon'} — ${u.connectionId}`, 'info'));
                });

                // Automatically called when we connect — gives us our connection ID
                connection.on('OnConnected', (connectionId, tokenInfo) => {
                    myConnectionId = connectionId;
                    document.getElementById('myId').textContent = `My ID: ${connectionId}`;
                    log(`Connected! ID: ${connectionId} | ${tokenInfo}`, 'info');
                });

                // 4. Start the connection
                await connection.start();
                document.getElementById('connectionStatus').textContent = 'Connected!';
                document.getElementById('connectionStatus').style.color = 'green';
                log('Connection established', 'info');

            } catch (err) {
                log(`Connection failed: ${err}`, 'error');
            }
        };

        // Broadcast a message to all connected clients
        document.getElementById('btnBroadcast').onclick = async () => {
            if (!connection) return log('Connect first!', 'error');
            const msg = document.getElementById('txtMessage').value || 'Hello!';
            await connection.invoke('Broadcast', msg);
            log(`Called Broadcast("${msg}")`);
        };

        // Join a named group
        document.getElementById('btnJoinGroup').onclick = async () => {
            if (!connection) return log('Connect first!', 'error');
            const group = document.getElementById('txtGroup').value || 'default';
            await connection.invoke('JoinGroup', group);
            log(`Called JoinGroup("${group}")`);
        };

        // Leave a named group
        document.getElementById('btnLeaveGroup').onclick = async () => {
            if (!connection) return log('Connect first!', 'error');
            const group = document.getElementById('txtGroup').value || 'default';
            await connection.invoke('LeaveGroup', group);
            log(`Called LeaveGroup("${group}")`);
        };

        // Send a message to a group
        document.getElementById('btnGroupChat').onclick = async () => {
            if (!connection) return log('Connect first!', 'error');
            const group = document.getElementById('txtGroup').value || 'default';
            const msg = document.getElementById('txtGroupMsg').value || 'Hi group!';
            await connection.invoke('GroupChat', group, msg);
            log(`Called GroupChat("${group}", "${msg}")`);
        };

        // Send a private message to a specific connection
        document.getElementById('btnPrivateChat').onclick = async () => {
            if (!connection) return log('Connect first!', 'error');
            const targetId = document.getElementById('txtTargetId').value;
            const msg = document.getElementById('txtPrivateMsg').value || 'Hey!';
            if (!targetId) return log('Enter a target connection ID!', 'error');
            await connection.invoke('IndividualChat', targetId, msg);
            log(`Called IndividualChat("${targetId}", "${msg}")`);
        };

        // Get the list of connected users
        document.getElementById('btnGetUsers').onclick = async () => {
            if (!connection) return log('Connect first!', 'error');
            await connection.invoke('GetConnectedUsers');
            log('Called GetConnectedUsers()');
        };
    </script>
</body>
</html>
```

**How to use the example:**
1. Start the server (`dotnet run`)
2. Save the HTML above and open it in your browser
3. Click **"Get Token & Connect"**
4. Try broadcast, groups, and private messages

---

### Key SignalR Concepts Explained

#### Hub
A **Hub** is a central class on the server that handles real-time communication. It defines methods clients can call and events clients can listen to. `TestHub` extends `Microsoft.AspNetCore.SignalR.Hub`.

#### Clients.All
Sends a message to **every connected client**. Used in `Broadcast`:
```csharp
await Clients.All.SendAsync("ReceiveMessage", message, connectionId, userName);
```

#### Clients.Caller
Sends a message **only to the client that made the call**. Used in `JoinGroup`, `LeaveGroup`, and `GetConnectedUsers`.

#### Clients.Client(connectionId)
Sends a message to a **specific client** by their connection ID. Used in `IndividualChat` for private messaging.

#### Clients.Group(groupName)
Sends a message to **all clients in a named group**. Used in `GroupChat`.

#### Groups
Named collections of connections. Clients can join or leave groups dynamically. Groups are ideal for chat rooms, notifications per channel, etc.

```csharp
await Groups.AddToGroupAsync(Context.ConnectionId, "room1");
await Groups.RemoveFromGroupAsync(Context.ConnectionId, "room1");
```

#### Connection Lifecycle
`OnConnectedAsync()` is called when a client connects, and `OnDisconnectedAsync()` when they disconnect. These are the perfect places to set up or clean up per-connection state.

---

### Configuration

All JWT settings are in `appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "EstaEsUnaClaveSuperSeguraParaTest12345!EstoEsMasLongitud",
    "Issuer": "SignalRTestForBruno",
    "Audience": "BrunoSignalRTest",
    "ExpirationMinutes": 60
  }
}
```

---

## Español

### ¿Qué es SignalR?

SignalR es una biblioteca para ASP.NET Core que simplifica la implementación de **funcionalidades web en tiempo real**. Tiempo real significa que el servidor puede enviar contenido a los clientes conectados al instante, sin que el cliente tenga que solicitarlo (polling).

**¿Cómo funciona?** SignalR elige automáticamente el mejor método de transporte disponible:
- **WebSockets** (preferido) — comunicación full-duplex de baja latencia
- **Server-Sent Events (SSE)** — streaming unidireccional servidor → cliente
- **Long Polling** — alternativa para navegadores antiguos

SignalR maneja toda la negociación del transporte y retrocede automáticamente al método disponible, por lo que no tienes que preocuparte por ello.

---

### Resumen del Proyecto

Este es un **servidor de pruebas SignalR** construido con ASP.NET Core (.NET 10). Expone un único hub en `/hub` y proporciona:

- **Autenticación JWT** con un endpoint `/token` incorporado para generar tokens de prueba
- **Broadcast público** — envía un mensaje a todos los clientes conectados
- **Grupos con nombre** — únete/sal de salas y envía mensajes a grupos específicos
- **Mensajería privada** — envía un mensaje directamente a una conexión específica
- **Seguimiento de usuarios conectados** — mira quién está en línea
- **Ciclo de vida de conexión** — recibe notificaciones cuando te conectas/desconectas

Este servidor fue diseñado para usarse con el cliente API [Bruno](https://www.usebruno.com/) (o cualquier cliente compatible con SignalR como un navegador, Postman o una app de consola .NET).

---

### Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Un cliente SignalR (navegador, Bruno, Postman, etc.)

---

### Cómo Ejecutar

```bash
cd signalRTestForBruno/signalRTestForBruno
dotnet run
```

El servidor inicia en `http://localhost:5246` (o `https://localhost:7238`).

---

### Endpoints

| Ruta | Método | Descripción |
|------|--------|-------------|
| `/` | GET | Health check → devuelve `"SignalR server OK"` |
| `/token` | GET | Genera un JWT con información de usuario aleatoria (nombre, rol, color favorito) |
| `/hub` | WebSocket | Endpoint del Hub de SignalR |

---

### Flujo de Autenticación

1. Llama a `GET /token` para obtener un JWT
2. Al conectarte a SignalR en `/hub`, pasa el token como `?access_token=<jwt>` en la query string
3. El servidor valida el token y llena `Context.User` con los claims

> **¿Por qué la query string?** Las conexiones WebSocket no pueden establecer cabeceras HTTP personalizadas (como `Authorization`), por lo que SignalR usa el parámetro `access_token` como convención para pasar tokens de autenticación.

---

### Métodos del Hub (Cliente → Servidor)

Estos son métodos que el **cliente llama** en el servidor:

| Método | Parámetros | Descripción | Concepto SignalR |
|--------|-----------|-------------|------------------|
| `Broadcast` | `message` (string) | Envía un mensaje a **todos** los clientes conectados | `Clients.All` |
| `JoinGroup` | `groupName` (string) | El cliente se une a un grupo con nombre | `Groups.AddToGroupAsync` |
| `LeaveGroup` | `groupName` (string) | El cliente abandona un grupo con nombre | `Groups.RemoveFromGroupAsync` |
| `GroupChat` | `groupName, message` | Envía un mensaje a **todos los miembros** de un grupo | `Clients.Group` |
| `IndividualChat` | `targetConnectionId, message` | Envía un mensaje privado a un usuario específico | `Clients.Client` |
| `GetConnectedUsers` | _(ninguno)_ | Obtiene la lista de todos los usuarios conectados | Respuesta al `Caller` |

---

### Eventos del Servidor (Servidor → Cliente)

Estos son eventos que el **servidor envía** a los clientes. Tu código debe escucharlos:

| Nombre del Evento | Payload | Disparado por |
|------------------|---------|--------------|
| `ReceiveMessage` | `message, connectionId, userName` | `Broadcast` |
| `ReceiveGroupJoined` | `groupName` | `JoinGroup` |
| `ReceiveGroupLeft` | `groupName` | `LeaveGroup` |
| `ReceiveGroupMessage` | `groupName, message, connectionId, userName` | `GroupChat` |
| `ReceivePrivateMessage` | `message, fromConnectionId, userName` | `IndividualChat` (en el destino) |
| `ReceivePrivateMessageSent` | `message, toConnectionId, userName` | `IndividualChat` (en el emisor) |
| `ReceiveError` | `errorMessage` | `IndividualChat` cuando no encuentra el destino |
| `ReceiveConnectedUsers` | `[ConnectedUserInfo]` | `GetConnectedUsers` |
| `OnConnected` | `connectionId, tokenInfo` | Automático al conectar exitosamente |

---

### Guía de Pruebas Paso a Paso

#### 1. Obtén un token

```bash
curl http://localhost:5246/token
```

Esto devuelve algo como:
```json
{
  "token": "eyJhbGciOiJIUzI1NiJ9...",
  "expiresAt": "2026-07-07T...",
  "claims": {
    "sub": "a1b2c3d4-...",
    "name": "Ana Martínez",
    "role": "admin",
    "favoriteColor": "#3357FF"
  }
}
```

#### 2. Conéctate al hub

Usando un cliente SignalR, conéctate a `http://localhost:5246/hub?access_token=<tu_token>`.

#### 3. Prueba las funcionalidades

- Llama a `Broadcast("¡Hola a todos!")` → todos los clientes reciben `ReceiveMessage`
- Llama a `JoinGroup("sala1")` → recibes `ReceiveGroupJoined("sala1")`
- Llama a `GroupChat("sala1", "¡Hola grupo!")` → todos los miembros reciben `ReceiveGroupMessage`
- Llama a `IndividualChat("<id_conexion>", "¡Oye!")` → ese cliente recibe `ReceivePrivateMessage`
- Llama a `GetConnectedUsers()` → recibes `ReceiveConnectedUsers([...])`

---

### Conceptos Clave de SignalR Explicados

#### Hub
Un **Hub** es una clase central en el servidor que maneja la comunicación en tiempo real. Define métodos que los clientes pueden llamar y eventos que los clientes pueden escuchar. `TestHub` extiende `Microsoft.AspNetCore.SignalR.Hub`.

#### Clients.All
Envía un mensaje a **todos los clientes conectados**. Se usa en `Broadcast`:
```csharp
await Clients.All.SendAsync("ReceiveMessage", message, connectionId, userName);
```

#### Clients.Caller
Envía un mensaje **solo al cliente que hizo la llamada**. Se usa en `JoinGroup`, `LeaveGroup` y `GetConnectedUsers`.

#### Clients.Client(connectionId)
Envía un mensaje a un **cliente específico** por su ID de conexión. Se usa en `IndividualChat` para mensajería privada.

#### Clients.Group(groupName)
Envía un mensaje a **todos los clientes en un grupo con nombre**. Se usa en `GroupChat`.

#### Groups
Colecciones con nombre de conexiones. Los clientes pueden unirse o salir de grupos dinámicamente. Los grupos son ideales para salas de chat, notificaciones por canal, etc.

```csharp
await Groups.AddToGroupAsync(Context.ConnectionId, "sala1");
await Groups.RemoveFromGroupAsync(Context.ConnectionId, "sala1");
```

#### Ciclo de Vida de la Conexión
`OnConnectedAsync()` se llama cuando un cliente se conecta, y `OnDisconnectedAsync()` cuando se desconecta. Estos son los lugares perfectos para inicializar o limpiar el estado de cada conexión.

---

### Configuración

Todas las opciones JWT están en `appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "EstaEsUnaClaveSuperSeguraParaTest12345!EstoEsMasLongitud",
    "Issuer": "SignalRTestForBruno",
    "Audience": "BrunoSignalRTest",
    "ExpirationMinutes": 60
  }
}
```
