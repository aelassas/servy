## Local test
```
scoop install servy.json
```

Fix encoding:
```powershell
[System.IO.File]::WriteAllText("servy.json", [System.IO.File]::ReadAllText("servy.json"), (New-Object System.Text.UTF8Encoding))                  
```

## Publish
```
scoop update
scoop bucket add aelassas https://github.com/aelassas/scoop-bucket
scoop install servy
scoop uninstall servy
```
