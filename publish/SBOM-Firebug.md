# Software Bill of Materials (SBOM)

**Product:** Firewall Configuration Utility
**Version:** 0.4.5.0
**Generated:** 2026-01-25
**Supplier:** Halrad LLC

---

## Summary

| Category | Count |
|----------|-------|
| Project Assemblies | 2 |
| NuGet Packages (Build-only) | 1 |
| .NET Framework Assemblies | 2 |

---

## Project Structure

Firebug consists of a CLI executable and a library merged into a single EXE via ILRepack:

| Assembly | Type | Description |
|----------|------|-------------|
| **firebug.exe** | Executable | Final merged Windows Firewall utility |
| Firebug.Cli | Application | Command-line interface and entry point |
| Firebug | Library | Windows Firewall and URL ACL management |

### Project References

```
firebug.exe (merged output)
├── Firebug.Cli
│   └── Firebug
└── Firebug
    └── (no dependencies)
```

---

## NuGet Package Dependencies

### Build-only Dependencies

| Package | Version | License | Description |
|---------|---------|---------|-------------|
| [ILRepack.Lib.MSBuild.Task](https://github.com/ravibpatel/ILRepack.Lib.MSBuild.Task) | 2.0.34.2 | MIT | MSBuild task to merge assemblies into single EXE |

**Note:** ILRepack is a build tool only. It is not distributed with the utility.

### Runtime Dependencies

None. Firebug has no third-party runtime dependencies.

---

## .NET Framework Assemblies

Target framework: **.NET Framework 4.8**

| Assembly | Purpose |
|----------|---------|
| System.Security.Principal | UAC elevation detection |
| System.Diagnostics.Process | Self-elevation via runas |

---

## Proprietary Components

The following components are proprietary Halrad LLC code:

| Component | Files | Description |
|-----------|-------|-------------|
| Firebug.Cli | `Firebug.Cli/*` | CLI entry point, argument parsing, self-elevation |
| Firebug | `Firebug/*` | Windows Firewall API wrapper, URL ACL management |

---

## License Summary

| License | Components |
|---------|------------|
| **MIT** | ILRepack.Lib.MSBuild.Task |
| **Proprietary** | Firebug.Cli, Firebug |
| **Microsoft EULA** | .NET Framework 4.8 |

---

## Dependency Graph

```
firebug.exe
├── Firebug.Cli (Proprietary)
├── Firebug (Proprietary)
└── .NET Framework 4.8 (Microsoft)
    ├── System.Security.Principal
    └── System.Diagnostics.Process
```

---

## Features

- Add/remove Windows Firewall rules (TCP/UDP)
- Add/remove HTTP URL ACL reservations
- Check firewall and URL ACL status
- Self-elevation via UAC when needed
- Generate configuration scripts

---

## Vulnerability Tracking

For security vulnerability information:

- **.NET Framework:** Check https://msrc.microsoft.com/update-guide

---

## Contact

For licensing questions or SBOM inquiries:

**Email:** mbxhub@halrad.com
**Web:** https://halrad.com/

---

*Last updated: 2026-01-25*
