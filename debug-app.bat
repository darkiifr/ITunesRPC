@echo off
echo ===================================
echo    Débogage iTunes RPC
echo ===================================
echo.

echo [INFO] Exécution de l'application avec journalisation des erreurs...
echo.

:: Créer un dossier pour les logs s'il n'existe pas
if not exist "logs" mkdir logs

:: Exécuter l'application avec redirection des erreurs vers un fichier log
dotnet run --project ItunesRPC\ItunesRPC.csproj > logs\output.log 2> logs\error.log

echo.
echo [INFO] Vérification des erreurs...

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
echo [INFO] Vérification de la sortie standard...
type logs\output.log

echo.
echo ===================================
echo    Fin du débogage
echo ===================================

pause