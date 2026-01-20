# Software Bill of Materials (SBOM)

**Product:** Queue Services (MBXQ)
**Version:** 0.4.5.0
**Generated:** 2026-01-19
**Supplier:** Halrad LLC

---

## Summary

| Category | Count |
|----------|-------|
| Project Assemblies | 2 |
| NuGet Packages (Build-only) | 1 |
| First-party Libraries | 0 |
| .NET Framework Assemblies | 2 |

---

## Project Structure

MBXQ consists of two assemblies merged into a single DLL via ILRepack:

| Assembly | Type | Description |
|----------|------|-------------|
| **mb_MBXQ.dll** | Plugin | Final merged MusicBee plugin |
| MBXShuffle | Library | Shuffle and influencer algorithms |
| MBXGuard | Library | Guardian Services |

### Project References

```
mb_MBXQ.dll (merged output)
└── MBXShuffle
    └── MBXGuard
```

---

## NuGet Package Dependencies

### Build-only Dependencies

| Package | Version | License | Description |
|---------|---------|---------|-------------|
| [ILRepack.Lib.MSBuild.Task](https://github.com/ravibpatel/ILRepack.Lib.MSBuild.Task) | 2.0.34.2 | MIT | MSBuild task to merge assemblies into single DLL |

**Note:** ILRepack is a build tool only. It is not distributed with the plugin.

### Runtime Dependencies

None. MBXQ has no third-party runtime dependencies.

---

## .NET Framework Assemblies

Target framework: **.NET Framework 4.8**

| Assembly | Purpose |
|----------|---------|
| System.Windows.Forms | Settings UI |
| System.Drawing | UI graphics |

---

## Proprietary Components

The following components are proprietary Halrad LLC code:

| Component | Files | Description |
|-----------|-------|-------------|
| MBXShuffle | `MBXShuffle/*` | Shuffle and influencer algorithms |
| MBXGuard | `MBXGuard/*` | Guardian Services |

---

## License Summary

| License | Components |
|---------|------------|
| **MIT** | ILRepack.Lib.MSBuild.Task |
| **Proprietary** | MBXShuffle, MBXGuard |
| **Microsoft EULA** | .NET Framework 4.8 |

---

## Dependency Graph

```
mb_MBXQ.dll
├── MBXShuffle (Proprietary)
├── MBXGuard (Proprietary)
└── .NET Framework 4.8 (Microsoft)
    ├── System.Windows.Forms
    └── System.Drawing
```

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
