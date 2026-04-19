@echo off
cls

dotnet build -warnaserror:none -nologo -clp:ErrorsOnly
move /Y "C:\Users\fired\RiderProjects\AeternalEverwatcher2\bin\Debug\netstandard2.1\AeternalEverwatcher.dll" "C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\"

start steam://rungameid/1030300