# Servy Chocolatey Package

> **Note:** Packaging and publishing to Chocolatey is automatically handled by the [choco.yml](../../.github/workflows/choco.yml) GitHub Actions workflow whenever a new release is published.

## Local test

```powershell
cd setup/choco/servy
choco pack
choco install servy -s . -y
choco uninstall servy -s . -y
```

### Manual push (emergency / backup)

```powershell
choco apikey --key="YOUR_API_KEY_HERE" --source="https://community.chocolatey.org/api/v2/package"
choco push servy.<version>.0.nupkg --source "https://community.chocolatey.org/api/v2/package"
```

## Verify published package

```powershell
choco search servy
choco install servy -y
```

## Check installed registry entries

To verify the installed registry entries for Servy:

```powershell
Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*, HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* | Where-Object { $_.DisplayName -like "Servy*" } | Select-Object DisplayName, DisplayVersion, UninstallString | Format-Table -AutoSize
```
