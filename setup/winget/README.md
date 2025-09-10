# Generate new manifests
wingetcreate update aelassas.Servy --version 1.1 --url https://github.com/aelassas/servy/releases/download/v1.1/servy-1.1-net8.0-x64-installer.exe

# Verify manifests
winget validate .\manifests\a\aelassas\Servy\1.1\
winget install --manifest .\manifests\a\aelassas\Servy\1.1\

# Submit a new PR to microsoft/winget-pkgs
git checkout -b servy-1.1
git add manifests/a/aelassas/Servy/1.1/*
git commit -m "New version: aelassas.Servy version 1.1"
git push origin servy-1.1

# Open a Pull Request
https://github.com/aelassas/winget-pkgs

# Test
winget source update
winget show servy
winget search servy
winget install servy
winget install servy --silent
winget uninstall servy

