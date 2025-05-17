package com.darkiiuseai.itunesrpc;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.nio.file.StandardCopyOption;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Gestionnaire du mode silencieux et du démarrage automatique
 */
public class SilentModeManager {
    
    private static final Logger LOGGER = Logger.getLogger(SilentModeManager.class.getName());
    private static final String STARTUP_FOLDER = System.getProperty("user.home") + 
            "\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\";
    private static final String SHORTCUT_NAME = "ITunesRPC.lnk";
    
    private boolean silentMode;
    private ITunesTrackMonitor trackMonitor;
    
    /**
     * Constructeur
     * 
     * @param silentMode true si l'application doit démarrer en mode silencieux
     */
    public SilentModeManager(boolean silentMode) {
        this.silentMode = silentMode;
    }
    
    /**
     * Définit le moniteur de piste à utiliser
     * 
     * @param trackMonitor le moniteur de piste
     */
    public void setTrackMonitor(ITunesTrackMonitor trackMonitor) {
        this.trackMonitor = trackMonitor;
    }
    
    /**
     * Vérifie si l'application est en mode silencieux
     * 
     * @return true si l'application est en mode silencieux
     */
    public boolean isSilentMode() {
        return silentMode;
    }
    
    /**
     * Active ou désactive le mode silencieux
     * 
     * @param silentMode true pour activer le mode silencieux
     */
    public void setSilentMode(boolean silentMode) {
        this.silentMode = silentMode;
    }
    
    /**
     * Configure le démarrage automatique de l'application
     * 
     * @param enable true pour activer le démarrage automatique, false pour le désactiver
     * @return true si l'opération a réussi, false sinon
     */
    public boolean setStartupEnabled(boolean enable) {
        try {
            File startupFolder = new File(STARTUP_FOLDER);
            if (!startupFolder.exists()) {
                startupFolder.mkdirs();
            }
            
            File shortcutFile = new File(STARTUP_FOLDER + SHORTCUT_NAME);
            
            if (enable) {
                // Créer un script PowerShell pour créer un raccourci
                String jarPath = new File(Main.class.getProtectionDomain().getCodeSource().getLocation().toURI()).getPath();
                Path tempScript = Files.createTempFile("createShortcut", ".ps1");
                
                // Écrire le script PowerShell
                String script = 
                        "$WshShell = New-Object -ComObject WScript.Shell\n" +
                        "$Shortcut = $WshShell.CreateShortcut('" + shortcutFile.getAbsolutePath() + "')\n" +
                        "$Shortcut.TargetPath = '" + System.getProperty("java.home") + "\\bin\\javaw.exe'\n" +
                        "$Shortcut.Arguments = '-jar \"" + jarPath + "\" -silent -startup'\n" +
                        "$Shortcut.WorkingDirectory = '" + new File(jarPath).getParent() + "'\n" +
                        "$Shortcut.Description = 'iTunes RPC'\n" +
                        "$Shortcut.IconLocation = '" + jarPath + ",0'\n" +
                        "$Shortcut.Save()";
                
                Files.write(tempScript, script.getBytes());
                
                // Exécuter le script PowerShell
                ProcessBuilder pb = new ProcessBuilder("powershell.exe", "-ExecutionPolicy", "Bypass", "-File", tempScript.toString());
                Process process = pb.start();
                int exitCode = process.waitFor();
                
                // Supprimer le script temporaire
                Files.delete(tempScript);
                
                return exitCode == 0;
            } else {
                // Supprimer le raccourci s'il existe
                if (shortcutFile.exists()) {
                    return shortcutFile.delete();
                }
                return true;
            }
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Erreur lors de la configuration du démarrage automatique: " + e.getMessage(), e);
            return false;
        }
    }
    
    /**
     * Vérifie si le démarrage automatique est activé
     * 
     * @return true si le démarrage automatique est activé
     */
    public boolean isStartupEnabled() {
        File shortcutFile = new File(STARTUP_FOLDER + SHORTCUT_NAME);
        return shortcutFile.exists();
    }
    
    /**
     * Exécute l'application en mode silencieux (sans interface graphique)
     */
    public void runInSilentMode() {
        LOGGER.log(Level.INFO, "Démarrage en mode silencieux");
        
        // Démarrer le moniteur de piste iTunes
        if (trackMonitor != null) {
            trackMonitor.start();
        }
        
        // Ajouter un hook d'arrêt pour arrêter proprement le moniteur
        Runtime.getRuntime().addShutdownHook(new Thread(() -> {
            LOGGER.log(Level.INFO, "Arrêt du mode silencieux");
            if (trackMonitor != null) {
                trackMonitor.stop();
            }
        }));
    }
}