@echo off
echo Lancement de l'application iTunes RPC avec console de debug...
echo.
cd /d "V:\Vins Software\Colio\ITunesRPC\ItunesRPC\bin\Debug\net6.0-windows10.0.17763.0"
echo Repertoire actuel: %CD%
echo.
echo Lancement de ItunesRPC.exe...
ItunesRPC.exe
echo.
echo Code de sortie: %ERRORLEVEL%
echo.
echo Appuyez sur une touche pour fermer cette fenetre...
pause > nul