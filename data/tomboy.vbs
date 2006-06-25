Dim WSHShell, dir, fso, f1
Set fso = CreateObject("Scripting.FileSystemObject")
Set WSHShell = WScript.CreateObject("WScript.Shell")

WSHShell.run "Tomboy.bat",0,0

