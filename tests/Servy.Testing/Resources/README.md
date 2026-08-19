# Servy Testing Resources

This directory contains native utility binaries embedded as resources for process inspection and process handle unit testing.

## Sysinternals Handle Tooling

For versioning, official download sources, and SHA-256 checksums, refer to [`THIRD-PARTY-NOTICES.md`](../../../THIRD-PARTY-NOTICES.md) at the repository root.

### File Verification

To verify the digital signatures and SHA-256 integrity of these embedded resources using PowerShell (compare hash output against `THIRD-PARTY-NOTICES.md`):

```powershell
# Verify digital signatures (Publisher should be "Microsoft Corporation")
Get-AuthenticodeSignature handle64.exe, handle64a.exe

# Compute SHA-256 hashes
Get-FileHash handle64.exe, handle64a.exe -Algorithm SHA256 | Select-Object Path, @{N="Hash";E={$_.Hash.ToLower()}}
```

### Refresh / Upgrade Procedure

When updating Sysinternals Handle:

1. Download `Handle.zip` directly from official Microsoft channels (`https://download.sysinternals.com/files/Handle.zip`).
2. Extract `handle64.exe` and `handle64a.exe`.
3. Verify the digital signature of the extracted binaries to confirm publisher authenticity (`Microsoft Corporation`):
```powershell
Get-AuthenticodeSignature handle64.exe, handle64a.exe
```

4. Replace the binaries in all four project resource directories across the repository:
* `src/Servy.CLI/Resources/`
* `src/Servy.Manager/Resources/`
* `src/Servy/Resources/`
* `tests/Servy.Testing/Resources/`

5. Compute the new SHA-256 hashes and update `THIRD-PARTY-NOTICES.md` at the repository root.
