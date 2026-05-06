Option Explicit

Dim fso, shell, scriptDir, ps1Path
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

' Resolve the directory where this .vbs is located
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
' Build the path to the sibling .ps1 file
ps1Path = fso.BuildPath(scriptDir, "ServyFailureEmail.ps1")

' Execute PowerShell hidden (-WindowStyle Hidden) and bypass policy
' We use triple-quotes to handle potential spaces in the install path
shell.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & ps1Path & """", 0, True