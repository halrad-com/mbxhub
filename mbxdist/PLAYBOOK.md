# MBXDist Release Playbook

Pinned cert: HALRAD LLC token, thumb `7267AEC2ABA9C2F85BEE3D3AC9544417B6694FB4` (valid to 2028-02).
Publish = stage a version. Promote = repoint the recommendation. Clients move only on promote+push.

## Publish a package version
Signing is per-resource: release files you sign are verified signature+thumbprint; files you do not
sign (ffmpeg, essentia, possibly truedat) are verified sha256-only under the signed client. Only
`mbxdist.exe` MUST always be signed - it carries the catalog.

1. Signed files: sign on the token FIRST (`signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 <file>`), then
   `tools\publish.ps1 -PackageDir core -Version <v> -TargetRoot musicbee-plugins -Files <signed files>`
2. Unsigned files: `tools\publish.ps1 ... -NoAuthenticode` (fragment gets `authenticode: false`).
3. Paste `manifest-fragment.json` into `src\MBXDist.App\Embedded\manifests.<package-id>.json` (`locked: true` for in-use DLLs).

Sign BEFORE publish - the manifest sha256 must hash the final bytes.

## Promote
1. Edit `Embedded\catalog.json` to the new version. Bump `AppInfo.Version`.
2. `tools\promote.ps1 -MbxdistVersion <v>`
3. Sign `mbxdist\<v>\mbxdist.exe` on the token.
4. `dotnet test mbxdist\MBXDist.sln` green.
5. Commit + push. Clients self-update, then apply.

## Rollback
Promote again with the catalog pointing at the older published version + a HIGHER AppInfo.Version.
Never delete feed version folders.

## Verify
Client: `mbxdist check` (exit 10) -> `mbxdist update` (exit 0) -> `mbxdist check` (exit 0).

## Exit codes
0 ok · 10 update available · 20 locked, staged .pending (close app, re-run) · 30 signature FAIL (stop, investigate) · 1 error (404 = not published; "no install target" = run `mbxdist init <root> <path>`).

## Cert rotation
Add new thumbprint ALONGSIDE old in `AppInfo.PinnedThumbprints` -> ship signed with OLD cert -> fleet moves -> sign with new -> drop old later.

## New family member
Add `Embedded\manifests.<id>.json` (+ EmbeddedResource in csproj) + catalog line -> publish -> promote.
