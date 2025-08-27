# Email notification function
function Send-NotificationEmail {
    [cmdletbinding()]
    Param (
        [string] $Subject,
        [string] [parameter(ValueFromPipeline)] $Body
    )

    # Configure your SMTP settings
    $smtpServer = "smtp.example.com"
    $smtpPort   = 587
    $smtpUser   = "username@example.com"
    $smtpPass   = "password"
    $from       = "ServyNotifications@example.com"
    $to         = "admin@example.com"

    $securePass = ConvertTo-SecureString $smtpPass -AsPlainText -Force
    $cred = New-Object System.Management.Automation.PSCredential ($smtpUser, $securePass)

    Send-MailMessage -From $from -To $to -Subject $Subject -Body $Body -SmtpServer $smtpServer -Port $smtpPort -Credential $cred -UseSsl
}

# Filter hash table for Get-WinEvent
$filter = @{
    LogName = 'Application'
    ProviderName = 'Servy'
    Level = 2  # Error
}

# Get the latest Servy error
$lastError = Get-WinEvent -FilterHashtable $filter | Sort-Object TimeCreated -Descending | Select-Object -First 1

if ($lastError) {
    $message = $lastError.Message
    if ($message -match "^\[(.+?)\]\s*(.+)$") {
        $serviceName = $matches[1]
        $logText = $matches[2]
    } else {
        $serviceName = "Unknown Service"
        $logText = $message
    }

    $subject = "Servy - $serviceName Failure"
    $body    = "A failure has been detected in service '$serviceName'." + [Environment]::NewLine + "Details: $logText"

    Send-NotificationEmail -Subject $subject -Body $body
} else {
    Write-Host "No Servy error events found."
}
