#Requires -Version 5.1
<#
.SYNOPSIS
    Shared testing utilities and assertion harness for Servy CLI unit test suites.

.DESCRIPTION
    Provides common test counters, assertion functions (Assert-Equal, Assert-True),
    and standardized summary rendering for Servy CLI unit tests.
#>

$script:TotalTests  = 0
$script:PassedTests = 0
$script:FailedTests = 0

<#
.SYNOPSIS
    Asserts equality between two values.
#>
function Assert-Equal {
    param(
        [string]$TestName,
        $Actual,
        $Expected
    )
    $script:TotalTests++
    if ($Actual -eq $Expected) {
        Write-Host "  [PASS] $TestName" -ForegroundColor Green
        $script:PassedTests++
    }
    else {
        Write-Host "  [FAIL] $TestName - Expected: '$Expected', Actual: '$Actual'" -ForegroundColor Red
        $script:FailedTests++
    }
}

<#
.SYNOPSIS
    Asserts that a given boolean condition evaluates to true.
#>
function Assert-True {
    param(
        [string]$TestName,
        [bool]$Condition
    )
    $script:TotalTests++
    if ($Condition) {
        Write-Host "  [PASS] $TestName" -ForegroundColor Green
        $script:PassedTests++
    }
    else {
        Write-Host "  [FAIL] $TestName - Expected condition to be True" -ForegroundColor Red
        $script:FailedTests++
    }
}

<#
.SYNOPSIS
    Outputs the final test summary and exits with the appropriate status code.
#>
function Invoke-TestSummary {
    Write-Host "`n====================================================" -ForegroundColor Cyan
    Write-Host " Test Summary" -ForegroundColor Cyan
    Write-Host " Total   : $script:TotalTests" -ForegroundColor Gray
    Write-Host " Passed  : $script:PassedTests" -ForegroundColor Green
    if ($script:FailedTests -gt 0) {
        Write-Host " Failed  : $script:FailedTests" -ForegroundColor Red
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 1
    }
    else {
        Write-Host " Failed  : 0" -ForegroundColor Green
        Write-Host "====================================================" -ForegroundColor Cyan
        exit 0
    }
}
