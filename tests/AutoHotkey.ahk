; Manual test fixture: minimal long-running AHK v2 script to wrap as a Servy service.
; Appends a heartbeat line every 5 s to ahk-test.log in the script's own folder.
#Requires AutoHotkey v2.0

logFile := A_ScriptDir "\ahk-test.log"

WriteLogLine(text) {
    global logFile
    formatted := FormatTime(A_Now, "yyyy-MM-dd HH:mm:ss")
    FileAppend("[" formatted "] " text "`n", logFile)
}

; Initial message
WriteLogLine("Service started.")

SetTimer(WriteLog, 5000)

WriteLog() {
    WriteLogLine("Still running...")
}
