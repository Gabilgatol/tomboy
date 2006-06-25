@echo off

mkdir bin
cd Win32
call build.bat
cd ..
cd Tomboy
call build.bat
cd ..
cd Tomboy\Plugins
call build.bat
cd ..\..
cd data
call build.bat
cd ..

