# Servy Scoop Package

`servy.json` is the canonical Scoop manifest for Servy. `scoop.yml` updates it on each release and publishes it to the aelassas bucket and to Scoop Extras.

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

## Publish

```powershell
scoop update
scoop bucket add aelassas https://github.com/aelassas/scoop-bucket
scoop bucket add extras
scoop search servy
scoop install servy
scoop uninstall servy
```
