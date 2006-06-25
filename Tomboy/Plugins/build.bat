@echo off
set MSC=call mcs

set REFS=-lib:%MONO%\lib\mono\gtk-sharp-2.0 -r:gtk-sharp -r:glib-sharp -r:pango-sharp -r:gdk-sharp -r:..\Tomboy.exe -r:..\..\Win32\Win32Stub.dll
set DEFINES=-d:DEBUG,TRACE,WIN32 -debug

set SOURCES=NoteOfTheDay.cs ExportToHTML.cs
set RESOURCES=-resource:ExportToHTML.xsl,ExportToHTML.xsl

@echo on
%MSC% -out:Plugins.dll -target:library %REFS% %DEFINES% %SOURCES% %RESOURCES%
mkdir ..\..\bin\Plugins
copy Plugins.dll ..\..\bin\Plugins

