# Generate new manifests
wingetcreate update aelassas.servy --version 1.0 --url https://github.com/aelassas/servy/releases/download/v1.0/servy-1.0-net48-x64-installer.exe

# Verify manifests
winget validate .\manifests\a\aelassas\servy\1.0\
winget install --manifest .\manifests\a\aelassas\servy\1.0\

# Submit a new PR to microsoft/winget-pkgs
git checkout -b servy-1.0
git add manifests/a/aelassas/servy/1.0/*
git commit -m "aelassas.servy version 1.0"
git push origin servy-1.0

# Open a Pull Request
https://github.com/aelassas/winget-pkgs
