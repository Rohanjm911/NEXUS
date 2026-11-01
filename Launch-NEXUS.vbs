Set WshShell = CreateObject("WScript.Shell")
strDir = CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName)
WshShell.CurrentDirectory = strDir
WshShell.Run Chr(34) & strDir & "\NEXUS.Desktop.exe" & Chr(34), 1, False
Set WshShell = Nothing

