# Servy Testing Resources

This directory contains native utility binaries embedded as resources for process inspection and process handle unit testing.

## Sysinternals Handle Tooling

* **Tool Name:** Microsoft Sysinternals Handle
* **Version:** 5.0
* **Source:** [https://learn.microsoft.com/sysinternals/downloads/handle](https://learn.microsoft.com/sysinternals/downloads/handle)

### File Verification

To verify the integrity of these embedded resources using PowerShell:

```powershell
(Get-FileHash handle64.exe -Algorithm SHA256).Hash.ToLower()
# Expected: 24bafcc570cc9bbb6b6e6652a57a519e0464e3996891aaba6f55299cce20b04f
```

### Refresh / Upgrade Procedure

When updating Sysinternals Handle:

1. Download `Handle.zip` directly from official Microsoft channels (`https://download.sysinternals.com/files/Handle.zip`).
2. Extract `handle64.exe`.
3. Verify the digital signature of the extracted binaries to confirm publisher authenticity (`Microsoft Corporation`).
4. Replace the binaries in the project resource directories.
5. Compute the new SHA-256 hashes and update `THIRD-PARTY-NOTICES.md` and this `README.md`.
