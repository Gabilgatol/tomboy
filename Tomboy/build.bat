@echo off
set MSC=call mcs

set REFS=-lib:%MONO%\lib\mono\gtk-sharp-2.0\ -r:gtk-sharp.dll -r:glib-sharp.dll -r:pango-sharp.dll -r:gdk-sharp.dll -r:atk-sharp.dll -r:../Win32/Win32Stub.dll
set DEFINES=-d:DEBUG,TRACE,WIN32,OLD_GTKSPELL -debug+

rem set PA_DIR=panelapplet\generated
rem set PA_SOURCES=panelapplet\BonoboUIVerb.cs %PA_DIR%\ChangeBackgroundHandler.cs %PA_DIR%\BackgroundType.cs

set SOURCES=%PA_SOURCES% Applet.cs Defines.cs Note.cs NoteBuffer.cs NoteManager.cs NoteTag.cs NoteWindow.cs PluginManager.cs Preferences.cs RecentChanges.cs Search.cs Tomboy.cs Tray.cs Trie.cs Undo.cs Utils.cs Watchers.cs
set IMG_DIR=..\data\images
set IMAGES=-resource:%IMG_DIR%\tintin.png,tintin.png -resource:%IMG_DIR%\stock_notes.png,stock_notes.png -resource:%IMG_DIR%\gnome-stock-searchtool.png,gnome-stock-searchtool.png
set RESOURCES=-resource:..\data\Start_Here.note,Start_Here.note %IMAGES%

@echo on
%MSC% -out:Tomboy.exe -target:exe %REFS% %DEFINES% %SOURCES% %RESOURCES%

copy Tomboy.exe ..\bin

