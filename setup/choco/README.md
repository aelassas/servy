## Local test
```
choco pack
choco install servy -s .
choco install servy -s . -y
choco uninstall servy -s .
choco push servy.1.1.0.nupkg --source https://push.chocolatey.org/

choco apikey --key="YOUR_API_KEY_HERE" --source="https://push.chocolatey.org/"
```

## Test
```
choco search servy
choco install servy -y
```

## local install
```
chocolateyinstall.ps1

$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$toolsDir       = Split-Path -Parent $MyInvocation.MyCommand.Definition
$installerType  = 'exe'
# Use local file for testing
$installerPath  = 'E:\dev\servy\src\setup\servy-1.9-x64-installer.exe'
$silentArgs     = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES'

Install-ChocolateyPackage $packageName $installerType $silentArgs $installerPath
```

## Regedit
```powershell
Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*, HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* | Where-Object { $_.DisplayName -like "Servy*" } | Select-Object DisplayName, DisplayVersion, UninstallString | ft -AutoSize
```