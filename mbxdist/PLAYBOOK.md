# MBXDist Release Playbook

This is an unreleased tool. Publishing stages immutable package bytes; promotion prepares the client carrying the recommendation. Neither action authorizes signing, committing, or pushing automatically.

The trust pin in `src/MBXDist.App/AppInfo.cs` is a **SHA-256 certificate hash**:

```text
C934BA3E5CF720F591E93591A04FE727A303D64EF2F60E1D21A24114744C266E
```

Do not substitute the SHA-1 thumbprint printed by some certificate tools. The configured certificate is the HALRAD LLC hardware-token code-signing certificate. This file records configuration, not a fresh certificate-validity check.

## 1. Prepare a package

Choose the release and final install layout. Sign resources intended to use Authenticode **before** calculating their manifest hashes. Signing changes bytes. Unsigned third-party resources must be explicitly marked `authenticode: false`.

Run the tooling from PowerShell 7. Example for final signed inputs:

```powershell
.\tools\publish.ps1 -PackageDir core -Version <version> -TargetRoot musicbee-plugins -Files <final-signed-files> -FeedRoot <absolute-staging-root>
```

Use `-NoAuthenticode` only for the intentionally unsigned case. The output is `<FeedRoot>/<PackageDir>/<Version>/` with a JSON **array** in `manifest-fragment.json`, even for one file. Existing version directories are rejected. Stage a fresh version instead of overwriting published bytes.

Copy the resource array into `src/MBXDist.App/Embedded/manifests.<package-id>.json`, confirm target roots and relative paths, and align the catalog and manifest versions. Every resource needs its actual positive size and final SHA-256. Zero/placeholder hashes, unsafe paths, duplicate targets, missing dependencies and inconsistent versions must be corrected before promotion.

The initial core manifest remains a sample. The separate historical `publish/` directory is not automatically an MBXDist feed: each embedded resource URL must resolve under the selected feed root.

## 2. Prepare the carrier

Update the embedded recommendation deliberately and bump `AppInfo.Version` for a changed recommendation. Then:

```powershell
.\tools\promote.ps1 -MbxdistVersion <client-version> -FeedRoot <absolute-staging-root>
```

Promotion publishes the self-contained Windows x64 single-file Release artifact, checks that the EXE starts by itself and reports the expected version, and validates embedded metadata and feed bytes before staging a new client version. It rejects an existing client version directory. Do not distribute the launcher from ordinary `bin/Release` build output.

Optional development controls are `-RuntimeFrameworkVersion <version>` and `-OfflineRestoreSource <absolute-package-source>`. Both are unset by default: ordinary promotion uses the SDK's servicing default and configured restore sources. An offline test may select an already cached runtime explicitly; that test choice is not a production runtime pin.

Promotion prepares an **unsigned** client and the local marker. The staging directory must not be a live feed. Sign the staged `mbxdist/<client-version>/mbxdist.exe` on the token before publishing either it or `mbxdist/latest.txt`. Never expose a marker pointing at an unsigned or absent client.

Token signing remains an explicit operator step, for example:

```text
signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 <final-mbxdist.exe>
```

## 3. Verify final signed bytes

Run Release builds/tests and the script tests under `tools/tests`. Provide a real signed fixture when exercising Windows signature acceptance:

```powershell
$env:MBXDIST_SIGNED_FIXTURE = '<absolute-signed-binary-path>'
$env:MBXDIST_SIGNED_THUMBPRINT = '<SHA-256-certificate-hash>'
dotnet test MBXDist.sln -c Release
.\tools\tests\packaging.tests.ps1
```

The fixture test is skipped when those variables are absent. For final release evidence:

1. Copy only the signed EXE into an empty directory and run `version`.
2. Run `validate-feed --feed-root <staging-root> --json` against final feed bytes.
3. Use a scratch `--config`, map a scratch install root, and explicitly install the selected package: `update <id>` → `verify` → `check`.
4. Hold a target open, update, close it, and repeat with the network unavailable. Confirm pending completion and correct state.
5. Exercise real signed client A → B self-update and confirm the original caller receives the final command's exit code and valid JSON.
6. Confirm wrong signatures, wrong hashes and tampered pending content cannot be installed.

Only then review the signed artifacts and marker together for an explicitly authorized commit/push/publication. Keep prior feed versions addressable. The scripts do not push.

## Recommendation reversal and certificate rotation

A newer client may recommend an older package, but already-newer installations are `ahead` and remain untouched. This is not an automatic rollback. No explicit downgrade mechanism is implemented; deciding one is separate work.

For certificate rotation, add the successor **SHA-256** certificate hash alongside the old pin, ship the transition client signed with the old certificate, then move to the new signer after clients have the transition. Remove the old pin only in a subsequent deliberate release.

## Adding family members

Prepare actual artifacts and installation mechanics before adding a manifest and catalog entry, and include the manifest as an `EmbeddedResource` in the App project. The current engine installs files; it does not register MSIX packages or run installers. General `update` acts on installed/interrupted packages; a new advertised member requires explicit selection. MusicBee update sourcing/delivery, Game Bar packaging and third-party hosting remain separate operator decisions.

See README for the command and exit-code contract. Remote telemetry remains disabled; diagnostics are local.
