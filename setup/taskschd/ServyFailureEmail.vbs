Option Explicit

Dim fso, shell, scriptDir, ps1Path, command, exitCode
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

' Resolve the directory where this .vbs is located
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)

' Build the path to the sibling .ps1 file
ps1Path = fso.BuildPath(scriptDir, "ServyFailureEmail.ps1")

' Verify the file exists before attempting to run it
If Not fso.FileExists(ps1Path) Then
    ' WScript.Echo is a modal MessageBox under wscript.exe and is invisible in a
    ' Task Scheduler action, so report through the event log instead.
    shell.LogEvent 1, "Servy Notification Error: Required script not found - " & ps1Path
    WScript.Quit 1
End If

' Execute PowerShell hidden (-WindowStyle Hidden) and bypass policy
' We use triple-quotes to handle potential spaces in the install path
Dim psExe
psExe = fso.BuildPath(shell.ExpandEnvironmentStrings("%SystemRoot%"), "System32\WindowsPowerShell\v1.0\powershell.exe")
command = """" & psExe & """ -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & ps1Path & """"

' Execute the command and capture the exit code
exitCode = shell.Run(command, 0, True)

WScript.Quit exitCode
