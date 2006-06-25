@echo off

if x%HOME%x == xx set HOME=%USERPROFILE%
set KDEHOME=.

if x%MONO%x == xx goto usepath

%MONO%\bin\mono --debug Tomboy.exe --panel-applet

goto end

:usepath

mono --debug Tomboy.exe --panel-applet

:end
