## Scoop manifest

`servy.json` is the canonical Scoop manifest for Servy. `scoop.yml` updates it on each release and publishes it to the aelassas bucket and to Scoop Extras.

## Local test
```powershell
scoop install servy.json
scoop uninstall servy
```

Fix encoding:
```powershell
[System.IO.File]::WriteAllText("servy.json", [System.IO.File]::ReadAllText("servy.json"), (New-Object System.Text.UTF8Encoding($false)))
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
