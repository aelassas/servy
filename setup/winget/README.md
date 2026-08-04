These manifests serve as a template for the installer manifest of the Servy application. They are not intended to be used directly.

WinGet publishing is automated: `.github/workflows/winget.yml` runs on every published release and submits the manifest to `microsoft/winget-pkgs` via `winget-releaser`. The steps below are only needed to regenerate the template or to publish by hand.

# Generate new manifests

```
wingetcreate update aelassas.Servy --version <version> --url https://github.com/aelassas/servy/releases/download/v<version>/servy-<version>-x64-installer.exe https://github.com/aelassas/servy/releases/download/v<version>/servy-<version>-arm64-installer.exe
```

# Verify manifests

```
winget validate .\manifests\a\aelassas\Servy\<version>
winget install --manifest .\manifests\a\aelassas\Servy\<version>\
```

# Submit a new PR to microsoft/winget-pkgs

```
git checkout -b servy-<version>
git add manifests/a/aelassas/Servy/<version>/*
git commit -m "New version: aelassas.Servy version <version>"
git push origin servy-<version>
```

# Open a Pull Request

[https://github.com/aelassas/winget-pkgs](https://github.com/aelassas/winget-pkgs)


# Test

```
winget source update
winget show servy
winget search servy
winget install servy
winget install servy --silent
winget uninstall servy
```