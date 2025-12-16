## Local test
```
scoop install servy.json
scoop uninstall servy
```

Fix encoding:
```powershell
[System.IO.File]::WriteAllText("servy.json", [System.IO.File]::ReadAllText("servy.json"), (New-Object System.Text.UTF8Encoding))                  
```

## Publish
```
scoop update
scoop bucket add aelassas https://github.com/aelassas/scoop-bucket
scoop bucket add extras
scoop search servy
scoop install servy
scoop uninstall servy
```
