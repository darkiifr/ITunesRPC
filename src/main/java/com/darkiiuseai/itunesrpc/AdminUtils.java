package com.darkiiuseai.itunesrpc;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Utilitaire pour gérer les privilèges administrateur
 */
public class AdminUtils {
    
    private static final Logger LOGGER = Logger.getLogger(AdminUtils.class.getName());
    
    /**
     * Vérifie si l'application est exécutée en tant qu'administrateur
     * 
     * @return true si l'application est exécutée en tant qu'administrateur, false sinon
     */
    public static boolean isRunAsAdmin() {
        try {
            // Créer un processus pour exécuter la commande 'net session'
            // Cette commande nécessite des privilèges administrateur pour s'exécuter correctement
            Process process = Runtime.getRuntime().exec("net session");
            int exitValue = process.waitFor();
            
            // Si la commande s'exécute avec succès (code de sortie 0), l'application est exécutée en tant qu'administrateur
            return exitValue == 0;
        } catch (IOException | InterruptedException e) {
            LOGGER.log(Level.WARNING, "Erreur lors de la vérification des privilèges administrateur: " + e.getMessage(), e);
            return false;
        }
    }
    
    /**
     * Relance l'application avec des privilèges administrateur
     * 
     * @param silentMode true pour démarrer en mode silencieux
     * @param startupMode true pour démarrer en mode démarrage
     * @return true si la demande d'élévation a été envoyée, false en cas d'erreur
     */
    public static boolean restartAsAdmin(boolean silentMode, boolean startupMode) {
        try {
            // Obtenir le chemin du JAR en cours d'exécution
            String javaBin = System.getProperty("java.home") + File.separator + "bin" + File.separator + "javaw";
            String currentJarPath = new File(Main.class.getProtectionDomain().getCodeSource().getLocation().toURI()).getPath();
            
            // Préparer la commande pour relancer l'application avec des privilèges administrateur
            List<String> command = new ArrayList<>();
            
            // Utiliser PowerShell pour lancer le processus avec élévation
            command.add("powershell.exe");
            command.add("-Command");
            
            // Construire la commande PowerShell pour lancer le processus avec élévation
            StringBuilder psCommand = new StringBuilder();
            psCommand.append("Start-Process -FilePath \"" + javaBin + "\" ");
            psCommand.append("-ArgumentList \"-jar\",\"" + currentJarPath + "\"");
            
            // Ajouter les arguments pour le mode silencieux et le mode démarrage si nécessaire
            if (silentMode) {
                psCommand.append(",\"-silent\"");
            }
            if (startupMode) {
                psCommand.append(",\"-startup\"");
            }
            
            psCommand.append(" -Verb RunAs");
            command.add(psCommand.toString());
            
            // Exécuter la commande
            ProcessBuilder builder = new ProcessBuilder(command);
            builder.start();
            
            LOGGER.log(Level.INFO, "Demande d'élévation des privilèges envoyée");
            return true;
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Erreur lors de la relance de l'application avec des privilèges administrateur: " + e.getMessage(), e);
            return false;
        }
    }
}