# Servy Scoop Package

`servy.json` is the canonical Scoop manifest for Servy. `scoop.yml` updates it in the aelassas bucket on each release, and opens a pull request against Scoop Extras when the workflow is started manually with `run_extras_pr` enabled.

## Local test

Run from the `setup/scoop` directory:

```powershell
cd setup/scoop
scoop install servy.json
scoop uninstall servy
```

If a manual edit introduced a UTF-8 BOM, strip it using an absolute path to avoid `.NET` current-directory resolution issues:

```powershell
$manifest = (Resolve-Path .\servy.json).Path
[System.IO.File]::WriteAllText($manifest, [System.IO.File]::ReadAllText($manifest), (New-Object System.Text.UTF8Encoding($false)))
```

## Verify published package

```powershell
scoop update
scoop bucket add aelassas https://github.com/aelassas/scoop-bucket
scoop bucket add extras
scoop search servy
scoop install servy
scoop uninstall servy
```
