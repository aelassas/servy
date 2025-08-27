Set objShell = CreateObject("Wscript.Shell")
objShell.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""C:\Program Files\Servy\taskschd\ServyFailureNotification.ps1""", 0, False
