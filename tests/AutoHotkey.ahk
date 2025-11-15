#Requires AutoHotkey v2.0

logFile := "C:\ahk-test.log"

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
