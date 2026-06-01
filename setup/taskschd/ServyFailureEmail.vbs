Option Explicit

Dim fso, shell, scriptDir, ps1Path, exitCode, cmd
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

' Resolve the directory
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)

' Build the path
ps1Path = fso.BuildPath(scriptDir, "ServyFailureEmail.ps1")

' Verify the file exists before attempting to run it
If Not fso.FileExists(ps1Path) Then
    WScript.Echo "Error: File not found - " & ps1Path
    WScript.Quit 1
End If

' Build the command string
' We use the & chr(34) & sequence for quotes to be absolutely clear
cmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " & chr(34) & ps1Path & chr(34)

' Execute the command with parentheses because we are assigning to exitCode
exitCode = shell.Run(cmd, 0, True)

WScript.Quit exitCode