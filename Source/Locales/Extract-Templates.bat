@ECHO OFF

PUSHD %~p0
FOR /D %%M IN (.\..\*) DO (
	dotnet gettext.extractor -s %%M -t .\%%~nxM\%%~nxM.pot -o
	IF EXIST .\%%~nxM\%%~nxM.pot (
	echo Updated translation template .\%%~nxM\%%~nxM.pot
	)
)
POPD
IF "%~1"=="" PAUSE
