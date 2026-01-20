# Software Bill of Materials (SBOM)

**Product:** MBXHub
**Version:** 1.0.26.118
**Generated:** 2026-01-19
**Supplier:** Halrad LLC

---

## Summary

| Category | Count |
|----------|-------|
| Project Assemblies | 5 |
| NuGet Packages (Build-only) | 1 |
| First-party Libraries | 1 |
| .NET Framework Assemblies | 4 |

---

## Project Structure

MBXHub consists of multiple assemblies merged into a single DLL via ILRepack:

| Assembly | Type | Description |
|----------|------|-------------|
| **mb_MBXHub.dll** | Plugin | Final merged MusicBee plugin |
| MBXHub.Plugin | Library | Plugin entry point, Settings UI |
| MBXHub.Core | Library | Shared interfaces and types |
| MBXHub.REST | Library | HTTP REST API endpoints |
| MBXHub.WebSocket | Library | Real-time WebSocket events |

### Project References

```
mb_MBXHub.dll (merged output)
├── MBXHub.Plugin
│   ├── MBXHub.Core
│   ├── MBXHub.REST
│   │   └── MBXHub.Core
│   ├── MBXHub.WebSocket
│   │   └── MBXHub.Core
│   └── SsdpCore (first-party)
└── SsdpCore (first-party)
    └── (no dependencies)
```

---

## First-party Libraries

### SsdpCore

| Attribute | Value |
|-----------|-------|
| **Component** | SsdpCore |
| **Author** | Halrad LLC |
| **Version** | 1.0.0 |
| **License** | MIT |
| **Source URL** | https://dev.azure.com/halrad/firebug/_git/firebug |
| **Usage** | SSDP/UPnP device discovery and advertising |
| **Description** | Clean-room implementation of SSDP protocol for network service discovery |

---

## NuGet Package Dependencies

### Build-only Dependencies

| Package | Version | License | Description |
|---------|---------|---------|-------------|
| [ILRepack.Lib.MSBuild.Task](https://github.com/ravibpatel/ILRepack.Lib.MSBuild.Task) | 2.0.34.2 | MIT | MSBuild task to merge assemblies into single DLL |

**Note:** ILRepack is a build tool only. It is not distributed with the plugin.

### Runtime Dependencies

None. MBXHub has no third-party runtime dependencies.

---

## .NET Framework Assemblies

Target framework: **.NET Framework 4.8**

| Assembly | Purpose |
|----------|---------|
| System.Web | HTTP utilities |
| System.Web.Extensions | JSON serialization (JavaScriptSerializer) |
| System.Net.Http | HTTP client for SSDP |
| System.Windows.Forms | Settings UI |

---

## Proprietary Components

The following components are proprietary Halrad LLC code:

| Component | Files | Description |
|-----------|-------|-------------|
| MBXHub.Plugin | `MBXHub.Plugin/*` | Plugin entry point, MusicBee integration, Settings UI |
| MBXHub.Core | `Core/*` | Shared interfaces, types, configuration |
| MBXHub.REST | `REST/*` | HTTP listener, routing, API handlers |
| MBXHub.WebSocket | `WebSocket/*` | WebSocket server, real-time events |

---

## License Summary

| License | Components |
|---------|------------|
| **MIT** | SsdpCore, ILRepack.Lib.MSBuild.Task |
| **Proprietary** | MBXHub.Plugin, MBXHub.Core, MBXHub.REST, MBXHub.WebSocket |
| **Microsoft EULA** | .NET Framework 4.8 |

---

## Dependency Graph

```
mb_MBXHub.dll
├── MBXHub.Plugin (Proprietary)
├── MBXHub.Core (Proprietary)
├── MBXHub.REST (Proprietary)
├── MBXHub.WebSocket (Proprietary)
├── SsdpCore (MIT, first-party)
└── .NET Framework 4.8 (Microsoft)
    ├── System.Web
    ├── System.Web.Extensions
    ├── System.Net.Http
    └── System.Windows.Forms
```

---

## Related Components

### firebug.exe

Utility for Windows Firewall configuration from the Firebug library (fireants repo):

| Attribute | Value |
|-----------|-------|
| **Version** | 0.1.0 |
| **Source** | [fireants/src/Firebug.Cli](https://dev.azure.com/halrad/firebug) |
| **Purpose** | Configure firewall rules for MBXHub ports |
| **Dependencies** | Firebug.dll (merged via ILRepack) |

---

## Vulnerability Tracking

For security vulnerability information:

- **.NET Framework:** Check https://msrc.microsoft.com/update-guide
- **NuGet Packages:** Check https://github.com/advisories

---

## Contact

For licensing questions or SBOM inquiries:

**Email:** mbxhub@halrad.com
**Web:** https://mbxhub.com/

---

*Last updated: 2026-01-19*
