# ==============================================================================
# Project: Servy
# Script:  tools-config.ps1
# Purpose: Dynamic tool path resolution with environment and fallback support.
# ==============================================================================

<#
.SYNOPSIS
    Resolves the full path of a required executable or tool.

.DESCRIPTION
    Checks for the tool path in the following order of priority:
    1. Environment variable (SERVY_TOOL_<Name>)
    2. System PATH (via Get-Command)
    3. Provided fallback paths

.PARAMETER Name
    The name of the tool to resolve (e.g., 'signtool', 'iscc').

.PARAMETER Fallbacks
    An array of file paths to check if the tool is not found in the environment or PATH.

.EXAMPLE
    Resolve-Tool -Name "SignTool" -Fallbacks @("C:\Program Files (x86)\...\signtool.exe")
#>
function Resolve-Tool {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [string[]]$Fallbacks
    )

    # 1. Check dynamic Environment Variable
    # Use Get-Item because $env:SERVY_TOOL_$Name is not valid syntax.
    $envVarName = "SERVY_TOOL_$Name"
    $envPath = (Get-Item -Path "Env:$envVarName" -ErrorAction SilentlyContinue).Value
    if ($envPath) { return $envPath }

    # 2. Check System PATH
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) { 
        # .Definition returns the path for Application types and the command text for others
        return $cmd.Definition 
    }

    # 3. Check Fallbacks
    if ($Fallbacks) {
        foreach ($p in $Fallbacks) { 
            if (Test-Path $p) { return $p } 
        }
    }

    throw "Required tool '$Name' not found. Please install it or set the '$envVarName' environment variable."
}