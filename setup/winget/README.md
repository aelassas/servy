# Servy WinGet Package

These notes cover regenerating the WinGet manifest by hand. The repository does not keep a local manifest copy - WinGet publishing is automated: `.github/workflows/winget.yml` runs on every published release and submits the manifest to `microsoft/winget-pkgs` via `winget-releaser`. The steps below are only needed to publish by hand.

## Generate new manifests

`wingetcreate` writes the manifest into `.\manifests\a\aelassas\Servy\<version>` relative to the current working directory.

```powershell
wingetcreate update aelassas.Servy --version <version> --urls https://github.com/aelassas/servy/releases/download/v<version>/servy-<version>-x64-installer.exe https://github.com/aelassas/servy/releases/download/v<version>/servy-<version>-arm64-installer.exe
```

## Verify manifests

```powershell
winget validate --manifest .\manifests\a\aelassas\Servy\<version>
winget install --manifest .\manifests\a\aelassas\Servy\<version>
```

## Submit a new PR to microsoft/winget-pkgs

```powershell
git clone https://github.com/aelassas/winget-pkgs
cd winget-pkgs
git checkout -b servy-<version>
Copy-Item -Recurse ..\manifests\a\aelassas\Servy\<version> manifests\a\aelassas\Servy\<version>
git add manifests/a/aelassas/Servy/<version>/*
git commit -m "New version: aelassas.Servy version <version>"
git push origin servy-<version>
```

## Open a pull request

[https://github.com/aelassas/winget-pkgs](https://github.com/aelassas/winget-pkgs)

## Test

```powershell
winget source update
winget show servy
winget search servy
winget install servy
winget install servy --silent
winget uninstall servy
```
