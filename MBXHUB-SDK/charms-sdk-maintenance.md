# Maintaining the Charms SDK documentation

The internal spec owns intended behavior and rationale. `MBXHUB-SDK/charms-sdk.md` is the partner reference; `MBXHUB-SDK/integration-guide.md` already owns the end-to-end walkthrough and is linked rather than duplicated. `MBXHUB-SDK/charms-sdk-contract.json` owns shared field, placement, registration-error, refusal-state and lifecycle tables. Edit prose outside generated markers. Edit shared tables in JSON, then regenerate.

Use PowerShell 7, whose built-in `ConvertFrom-Markdown` avoids a new dependency:

```powershell
./build-charms-sdk.ps1 -InternalRepo C:/Users/scott/source/repos/MBX/restfulbee
./build-charms-sdk.ps1 -InternalRepo C:/Users/scott/source/repos/MBX/restfulbee -Check
```

**The website copy is no longer this script's output.** Since 2026-09-19 `https://mbxhub.com/downloads/docs/charms-sdk.html` is rendered from `MBXHUB-SDK/charms-sdk.md` by the site's own renderer in the internal repo (`deploy/render-md.mjs`, plain Node, no PowerShell 7), so it carries the site's masthead and cannot drift from this file: run `node deploy/render-md.mjs` there after editing the Markdown. `MBXHUB-SDK/charms-sdk.html` here remains this script's standalone output for reading on GitHub.

Without `-InternalRepo`, generation/checking only touches the public Markdown and `MBXHUB-SDK/charms-sdk.html`. With it, the script also refreshes shared tables in the internal spec and writes its deployment HTML at `deploy/mbxhub.com/downloads/docs/charms-sdk.html`. No server is contacted or deployment performed. HTML links to repository-relative resources are made absolute for the public repository; website-root links retain their website meaning.

The implementation ledger at `docs/superpowers/analysis/2026-09-07-charms-sdk-implementation-status.md` in restfulbee records evidence and unresolved design work. Update that ledger when implementation changes; do not silently turn parsed values into supported features. Reviews are immutable historical findings against named revisions. Their disposition is recorded in the ledger, not by rewriting their original verdicts.
