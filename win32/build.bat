@echo off
set MSC=call mcs

set REFS=-r:gtk-sharp -r:glib-sharp -r:pango-sharp -r:gdk-sharp -r:System.Windows.Forms -r:System.Drawing -lib:%MONO%\lib\mono\gtk-sharp-2.0\ 
set DEFINES=-d:DEBUG,TRACE,WIN32 -debug

set SOURCES=*.cs

set IMG_DIR=..\data\images
set IMAGES=-resource:%IMG_DIR%\tintin.png,tintin.png -resource:%IMG_DIR%\stock_notes.png,stock_notes.png -resource:%IMG_DIR%\gnome-stock-searchtool.png,gnome-stock-searchtool.png

set RESOURCES=-resource:tintin.ico,tintin.ico %IMAGES%

@echo on
%MSC% -out:Win32Stub.dll -target:library %REFS% %DEFINES% %SOURCES% %RESOURCES%

copy Win32Stub.dll ..\bin

