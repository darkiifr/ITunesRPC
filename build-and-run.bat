@echo off
echo ===================================
echo    Compilation et lancement iTunes RPC
echo ===================================
echo.

:: Vérifier si .NET SDK est installé
dotnet --version > nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] .NET SDK n'est pas installé ou n'est pas accessible.
    echo Veuillez installer .NET 6.0 SDK depuis https://dotnet.microsoft.com/download/dotnet/6.0
    pause
    exit /b 1
)

echo [INFO] Restauration des packages NuGet...
dotnet restore ItunesRPC.sln
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Échec de la restauration des packages.
    pause
    exit /b 1
)

echo [INFO] Compilation du projet en mode Release...
dotnet build ItunesRPC.sln -c Release
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Échec de la compilation.
    pause
    exit /b 1
)

echo [INFO] Publication du projet...
dotnet publish ItunesRPC\ItunesRPC.csproj -c Release -o .\publish /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Échec de la publication.
    pause
    exit /b 1
)

echo.
echo [SUCCÈS] Compilation terminée avec succès !
echo Les fichiers compilés se trouvent dans le dossier "publish"
echo.

set /p run_app=Voulez-vous lancer l'application maintenant ? (O/N): 
if /i "%run_app%"=="O" (
    echo [INFO] Lancement de l'application...
    start .\publish\ItunesRPC.exe
) else (
    echo [INFO] Vous pouvez lancer l'application manuellement depuis le dossier "publish"
)

echo.
echo [INFO] Opération terminée.
pause