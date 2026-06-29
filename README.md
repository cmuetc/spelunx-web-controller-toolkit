<p align="right">
  <a href="README.zh-CN.md">
    <img src="https://img.shields.io/badge/简体中文-点击进入-blue?style=for-the-badge" alt="简体中文" />
  </a>
</p>

# Spelunx Web Controller Toolkit

A Unity toolkit that turns mobile browsers into game controllers, enabling a local multiplayer experience with a **host display + multiple phone remotes**. Developed by the Entertainment Technology Center (ETC) at Carnegie Mellon University, licensed under MIT.

## Overview

This project addresses a common need in party games, exhibition interactives, and classroom demos: **Unity runs on a host machine (PC / large screen), and players join via their phones through a web page to control the game**—no app install required.

The system uses a three-layer architecture:

```
┌─────────────────┐     WebSocket      ┌──────────────────┐     WebSocket      ┌─────────────────┐
│  Unity Host     │ ◄────────────────► │  Node.js Relay   │ ◄────────────────► │  Mobile Browser │
│  (HostClient)   │   role=host        │  (server.js)     │   role=client      │  (join.html…)   │
└─────────────────┘                    └──────────────────┘                    └─────────────────┘
```

1. **Unity host** — Runs game logic, connects to the relay via `HostClient`, creates rooms, and receives player input.
2. **Node.js relay** — Express + WebSocket server for room management, message forwarding, and serving the mobile controller pages (default port `3010`).
3. **Mobile browser** — Players scan a QR code or enter a room code to join a waiting queue; when the host starts the game, controller slots are assigned in join order.

Messages use **pipe-delimited plain text** (e.g. `slider|id|slot|73.5`) instead of JSON to reduce latency and parsing overhead.

## Controller Slots

Up to 4 players are supported. Slots are assigned automatically in join order, each with a distinct mobile UI:

| Slot | Controller Type | Input |
|------|-----------------|-------|
| P1 | Slider | Continuous value 0–100 |
| P2 | Messenger | Text messages (forwarded to P4 for display) |
| P3 | Action Button | Single press / release |
| P4 | Display | Read-only; shows text sent by P2 |

A legacy **D-pad + jump** input interface is also available for custom scenarios.

## Typical Flow

1. Enter Play mode in the Unity Editor — `NodeAutoRunner` starts the Node relay automatically; in builds, `NodeRuntimeStarter` handles startup.
2. `HostClient` connects to the relay and receives a 4-character room code (e.g. `AB3K`).
3. `LanAddressDisplay` shows the LAN address and QR code on screen; players scan to open `join.html`.
4. Players enter a nickname and wait in the lobby; the host can see the queue count in Unity or on the web.
5. The host calls `HostClient.AssignAndStart()` to assign slots and begin the game.
6. Mobile input is forwarded to Unity in real time; game logic responds by subclassing `PlayerInputRouter`.

## Project Structure

```
spelunx-web-controller-toolkit/
└── Spelunx Web Multiplayer Toolkit/     # Unity 6 project
    ├── Assets/
    │   ├── Scripts/
    │   │   ├── Web/
    │   │   │   ├── HostClient.cs          # WebSocket client; room & input state
    │   │   │   ├── PlayerInputRouter.cs   # Input routing base class
    │   │   │   ├── PlayerListUI.cs        # Player list UI
    │   │   │   ├── LanAddressDisplay.cs   # LAN address & QR code display
    │   │   │   └── NodeRuntimeStarter.cs  # Auto-starts Node in builds
    │   │   └── Sample Scene/
    │   │       └── SampleGameplay.cs      # Example: slider force, button impulse
    │   ├── Editor/
    │   │   └── NodeAutoRunner.cs          # Auto-starts Node in Play mode
    │   └── StreamingAssets/
    │       └── server/
    │           ├── server.js              # Relay server
    │           ├── package.json
    │           └── public/                # Mobile controller pages
    │               ├── join.html          # Join room
    │               ├── waiting.html       # Waiting lobby
    │               ├── controller_p1.html # P1 slider UI
    │               ├── controller_p2.html # P2 messenger UI
    │               ├── controller_p3.html # P3 button UI
    │               └── controller_p4.html # P4 display UI
    └── Packages/
        └── manifest.json                  # Dependencies (NativeWebSocket, etc.)
```

## Quick Start

### Requirements

- [Unity 6](https://unity.com/) (project version: `6000.1.0f1`)
- [Node.js](https://nodejs.org/) (to run the relay server)

### Run the Sample

1. Open the `Spelunx Web Multiplayer Toolkit` folder in Unity.
2. In `Assets/Editor/NodeAutoRunnerConfig.asset`, assign `server.js` to the `serverJs` field (the editor creates this asset automatically on first open).
3. Ensure Node.js is installed and `node` is available in your terminal.
4. Enter Play mode — the relay starts automatically; the console shows `Relay on http://localhost:3010`.
5. Open the sample scene; the screen displays the LAN address and room code. On a phone on the same network, visit that address to join.

### Integrate Into Your Game

1. Add a `HostClient` component to your scene.
2. Create a script that extends `PlayerInputRouter` and override `OnSliderInput`, `OnActionButton`, `OnTextMessage`, etc.
3. Assign the subclass to `HostClient.router`.
4. Call `hostClient.AssignAndStart()` when you are ready to begin.

See `SampleGameplay.cs` for an example: P1's slider controls force; P3's button applies an upward impulse to a sphere.

## Remote Deployment

`HostClient` exposes `isRemoted` and `relayHost`. Set `isRemoted` to `true` and point `relayHost` at a remote relay server; Unity will skip local Node startup and connect directly to the remote WebSocket endpoint. Useful when the relay is hosted in the cloud.

## License

[MIT License](LICENSE) — Copyright (c) 2026 Entertainment Technology Center at Carnegie Mellon University
