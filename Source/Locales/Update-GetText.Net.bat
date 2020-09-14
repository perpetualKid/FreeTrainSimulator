@ECHO OFF
REM Script must be run from Locales directory.

dotnet new tool-manifest --force
dotnet tool install gettext.net.extractor

FOR /D %%M IN (.\..\*) DO (
	dotnet gettext.extractor -s %%M -t .\%%~nxM\%%~nxM.pot
)

REM PUSHD ..\3rdPartyLibs
REM FOR /D %%M IN (..\Locales\*) DO (
REM 	GNU.Gettext.Xgettext.exe -D ..\%%~nxM --recursive -o ..\Locales\%%~nxM\%%~nxM.pot
REM 	FOR %%L IN (%%M\*.po) DO GNU.Gettext.Msgfmt.exe -l %%~nL -r %%~nxM -d ..\..\Program -L ..\..\Program %%L
REM )
REM POPD
IF "%~1"=="" PAUSE
