# publish.ps1
# Main setup bundle script

$tfm = "net8.0-windows"
$version = "1.0.0"

# Get the directory of this script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Function to safely call a script with parameters
function Invoke-Script {
    param(
        [string]$ScriptPath,
        [hashtable]$Params
    )

    if (-Not (Test-Path $ScriptPath)) {
        Write-Error "Script not found: $ScriptPath"
        return
    }

    Write-Host "Calling $ScriptPath with parameters: $Params"
    & $ScriptPath @Params
}

# Build self-contained bundle
Invoke-Script -ScriptPath (Join-Path $ScriptDir "publish-sc.ps1") -Params @{ version = $version; tfm = $tfm }

# Build framework-dependent bundle
Invoke-Script -ScriptPath (Join-Path $ScriptDir "publish-fd.ps1") -Params @{ version = $version; tfm = $tfm }

# Pause when double-clicked
if ($Host.Name -eq "ConsoleHost") {
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
