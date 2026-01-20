#HALRAD MBXHub

**RestfulBee** - HALRAD MBXHub ecosystem for MusicBee - a suite of components that transform MusicBee into a network-accessible music service.


## Vision

MusicBee is a powerful desktop music player, but it's trapped on a single machine. RestfulBee breaks it free by exposing MusicBee's full capabilities over the network - enabling control from any device, integration with any system, and access from anywhere on your local network.

## Components

| Component   | Folder  | Description                                     |
| ----------- | ------- | ----------------------------------------------- |
| **MBXQ**    | `MBXQ/` | Queue management suite (MBXShuffle + MBXGuard)  |
| **MBXH**    | `MBXH/` | Hub - REST API, MBRC protocol, WebSocket events |
| **MBXR**    | `MBXR/` | Remote - Cross-platform client (planned)        |
| **MBXS**    | `MBXS/` | Sync - Library synchronization (planned)        |
| **MBXCast** | MBXCast | Casting & streaming (planned)                   |

### MBXQ - Queue Management

True shuffle and audio validation for MusicBee.

| Module         | Output         | Description                                                       |
| -------------- | -------------- | ----------------------------------------------------------------- |
| **MBXShuffle** | `mbxqueue.dll` | MusicBee plugin - plays every track exactly once before repeating |
| **MBXGuard**   | `MBXGuard.dll` | Audio file anomaly detection library                              |

### MBXH - Hub (Codename: restfulbee)

Network service plugin exposing MusicBee over multiple protocols.

| Module        | Port                             | Description                          |
| ------------- | -------------------------------- | ------------------------------------ |
| **REST**      | 8080                             | Primary HTTP API (~130 endpoints)    |
| **MBRC**      | 3000                             | Legacy protocol for Android/iOS apps |
| **WebSocket** | 8080 | Real-time event streaming            |
| **Discovery** | UDP 45345                        | MBRC client auto-discovery           |

Inspired by **MBRC** (protocol design) and **BeeKeeper** (REST API approach).

### MBXCast - UPnP Streaming

Stream MusicBee audio to UPnP renderers over the network.

| Module         | Output              | Description                                   |
| -------------- | ------------------- | --------------------------------------------- |
| **PhantomBee** | `mb_PhantomBee.dll` | MusicBee plugin - streams to Devialet Phantom |

**Target:** Replace physical audio cables (Optical/SPDIF) with network streaming to Devialet Phantom speakers.

```
Before:  MusicBee → DAC → Optical/Coax → Phantom
After:   MusicBee → PhantomBee → Network (UPnP) → Phantom
```

Uses shared [SsdpCore](../../fireants/src/SsdpCore) library for device discovery.

## Project Structure

```
restfulbee/
├── README.md               # This file
│
├── MBXQ/                   # Queue Management Suite
│   ├── MBXQ.sln            # Solution file
│   ├── ROADMAP.md          # Master roadmap
│   │
│   ├── MBXShuffle/         # Shuffle plugin
│   │   ├── MBXShuffle.csproj
│   │   ├── ROADMAP.md
│   │   ├── BACKLOG.md
│   │   └── plugin/
│   │       ├── Plugin.cs           # Main plugin entry point
│   │       ├── ShuffleEngine.cs    # Core shuffle logic
│   │       ├── ShuffleState.cs     # State persistence
│   │       ├── ShuffleSettings.cs  # Configuration
│   │       ├── BanList.cs          # Banned tracks management
│   │       └── MusicBeeInterface.cs
│   │
│   └── MBXGuard/           # Audio validation library
│       ├── MBXGuard.csproj
│       ├── ROADMAP.md
│       ├── BACKLOG.md
│       ├── IAudioValidator.cs      # Validator interface
│       ├── AudioValidationResult.cs # Result types
│       └── BasicValidator.cs       # Basic file checks
│
└── MBXH/                   # Hub Service
    ├── MBXH.sln            # Solution file
    ├── ROADMAP.md          # Vision, goals, milestones
    ├── SPEC.md             # Functional specification
    │
    ├── Core/               # Shared types and interfaces
    │   ├── MBXHub.Core.csproj
    │   ├── IModule.cs          # Module interface
    │   ├── IMusicBeeApi.cs     # MusicBee abstraction
    │   └── HubSettings.cs      # Configuration
    │
    ├── REST/               # HTTP API module
    │   ├── MBXHub.REST.csproj
    │   └── RestModule.cs       # REST server (scaffold)
    │
    ├── MBRC/               # Legacy protocol module
    │   ├── MBXHub.MBRC.csproj
    │   └── MbrcModule.cs       # MBRC server (scaffold)
    │
    └── WebSocket/          # Events module
        ├── MBXHub.WebSocket.csproj
        └── WebSocketModule.cs  # WebSocket server (scaffold)
```

## Status

| Component       | Status                                 |
| --------------- | -------------------------------------- |
| MBXQ/MBXShuffle | **Active** - Feature complete for v1.0 |
| MBXQ/MBXGuard   | Scaffold - Interfaces defined          |
| MBXH/Core       | Scaffold - Interfaces defined          |
| MBXH/REST       | Scaffold - Module stub                 |
| MBXH/MBRC       | Scaffold - Module stub                 |
| MBXH/WebSocket  | Scaffold - Module stub                 |
| MBXS            | Planned                                |
| MBXCast         | Planned                                |

## Documentation

- [MBXQ Roadmap](MBXQ/ROADMAP.md) - Queue suite milestones and MBXHub overview
- [MBXShuffle Roadmap](MBXQ/MBXShuffle/ROADMAP.md) - Shuffle plugin features
- [MBXShuffle Backlog](MBXQ/MBXShuffle/BACKLOG.md) - Future ideas
- [MBXGuard Roadmap](MBXQ/MBXGuard/ROADMAP.md) - Audio validation plans
- [MBXHub Roadmap](MBXH/ROADMAP.md) - Hub vision and goals
- [MBXHub Spec](MBXH/SPEC.md) - Functional specification (REST, MBRC, WebSocket)

## MBXRemote Product

**MBXRemote** is the product brand for remote control of MusicBee.

### Evolution

| Generation         | Codebase   | Component   | Description                           |
| ------------------ | ---------- | ----------- | ------------------------------------- |
| **v1 (Prototype)** | jellybee   | tntctl      | Windows client, MBRC protocol         |
| **v2 (Current)**   | restfulbee | MBXH + MBXR | Hub + cross-platform client, REST API |

**tntctl** (jellybee) was the prototype - proving the concept. **MBXRemote** from restfulbee is the natural evolution - modern architecture, REST-first, cross-platform.

```
MBXRemote
├── v1 (Prototype) ─── jellybee/tntctl ─── Windows, MBRC
│
└── v2 (Evolution) ─── restfulbee ─── REST-first, cross-platform
                       ├── MBXH (server)
                       └── MBXR (client)
```

Prototype proved the concept, restfulbee takes it forward.

## Related Projects

| Project         | Location                                 | Description                     |
| --------------- | ---------------------------------------- | ------------------------------- |
| **MBXCast**     | `MBXCast/`                               | UPnP streaming (PhantomBee)     |
| **fireants**    | `fireants/`                              | Shared libs (Firebug, SsdpCore) |
| **tntctl**      | `jellybee` codebase                      | MBXRemote client (Windows)      |
| **Clouseau**    | `ReferenceCode/mb_clouseau/`             | MusicBee data inspector         |
| **BeeKeeper**   | `ReferenceCode/beekeeper-master/`        | Reference: REST API approach    |
| **MBRC Plugin** | `ReferenceCode/mbrc-plugin-main-server/` | Reference: MBRC protocol        |

## Building

```bash
# MBXQ (Queue/Shuffle)
cd MBXQ
dotnet build

# MBXH (Hub)
cd MBXH
dotnet build
```

## License

Copyright (c) 2026 Haro@halrad.com
