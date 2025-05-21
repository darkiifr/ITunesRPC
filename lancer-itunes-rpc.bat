@echo off
echo ===================================
echo    Lancement iTunes RPC avec Debug
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

echo [INFO] Nettoyage des fichiers temporaires...
if exist "ItunesRPC\bin" rmdir /s /q "ItunesRPC\bin"
if exist "ItunesRPC\obj" rmdir /s /q "ItunesRPC\obj"

echo [INFO] Restauration des packages NuGet...
dotnet restore ItunesRPC.sln
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Échec de la restauration des packages.
    pause
    exit /b 1
)

echo [INFO] Compilation du projet en mode Debug...
dotnet build ItunesRPC.sln -c Debug
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Échec de la compilation.
    pause
    exit /b 1
)

echo.
echo [INFO] Lancement de l'application avec journalisation détaillée...
echo.

:: Créer un dossier pour les logs s'il n'existe pas
if not exist "logs" mkdir logs

:: Définir la variable d'environnement pour activer la journalisation détaillée
set DOTNET_ENVIRONMENT=Development
set DOTNET_CONSOLE_LOGGER_FORMAT="{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}"

:: Exécuter l'application avec redirection des erreurs vers un fichier log
echo [INFO] L'application va démarrer. Une fenêtre devrait apparaître...
echo [INFO] Si aucune fenêtre n'apparaît, vérifiez les logs pour les erreurs.
echo.

dotnet run --project ItunesRPC\ItunesRPC.csproj --no-build > logs\output.log 2> logs\error.log

echo.
echo [INFO] L'application s'est terminée. Vérification des erreurs...

:: Vérifier si le fichier d'erreur contient des données
for %%A in (logs\error.log) do set size=%%~zA
if %size% gtr 0 (
    echo [ALERTE] Des erreurs ont été détectées. Contenu du fichier d'erreur:
    echo.
    type logs\error.log
) else (
    echo [INFO] Aucune erreur détectée dans la sortie d'erreur standard.
)

echo.
echo [INFO] Contenu de la sortie standard:
type logs\output.log

echo.
echo ===================================
echo    Fin du lancement
echo ===================================

pause