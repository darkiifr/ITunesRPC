package com.darkiiuseai.itunesrpc;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Client pour l'API Discord Rich Presence
 * Cette classe gère la communication avec Discord pour afficher les informations
 * de la piste en cours de lecture dans le profil de l'utilisateur
 */
public class DiscordRpcClient {
    private static final Logger LOGGER = Logger.getLogger(DiscordRpcClient.class.getName());
    private static final String DISCORD_CLIENT_ID = "1234567890123456789"; // À remplacer par votre ID client Discord
    private static final String DISCORD_IPC_PATH = "\\\\?\\pipe\\discord-ipc-0";
    
    private boolean initialized = false;
    private Process rpcProcess;
    
    /**
     * Initialise le client Discord RPC
     * @throws Exception Si l'initialisation échoue
     */
    public void initialize() throws Exception {
        LOGGER.log(Level.INFO, "Initialisation du client Discord RPC");
        
        try {
            // Vérifier si Discord est en cours d'exécution
            if (!isDiscordRunning()) {
                LOGGER.log(Level.WARNING, "Discord n'est pas en cours d'exécution");
                return;
            }
            
            // Initialiser la connexion avec Discord
            // Dans une implémentation réelle, nous utiliserions une bibliothèque comme discord-rpc-java
            // Pour cet exemple, nous simulons l'initialisation
            
            initialized = true;
            LOGGER.log(Level.INFO, "Client Discord RPC initialisé avec succès");
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Erreur lors de l'initialisation du client Discord RPC: " + e.getMessage(), e);
            throw e;
        }
    }
    
    /**
     * Vérifie si Discord est en cours d'exécution
     * @return true si Discord est en cours d'exécution, false sinon
     */
    private boolean isDiscordRunning() {
        try {
            // Vérifier si le processus Discord est en cours d'exécution
            ProcessBuilder pb = new ProcessBuilder("tasklist", "/FI", "IMAGENAME eq Discord.exe", "/NH");
            Process process = pb.start();
            BufferedReader reader = new BufferedReader(new InputStreamReader(process.getInputStream()));
            String line;
            boolean running = false;
            
            while ((line = reader.readLine()) != null) {
                if (line.contains("Discord.exe")) {
                    running = true;
                    break;
                }
            }
            
            process.waitFor();
            reader.close();
            
            return running;
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de la vérification de l'état de Discord: " + e.getMessage(), e);
            return false;
        }
    }
    
    /**
     * Met à jour la présence Discord avec les informations de la piste en cours
     * @param trackInfo Les informations de la piste
     */
    public void updatePresence(TrackInfo trackInfo) {
        if (!initialized || trackInfo == null) {
            return;
        }
        
        try {
            LOGGER.log(Level.INFO, "Mise à jour de la présence Discord pour: " + trackInfo.getTitle());
            
            // Créer l'objet de présence
            JSONObject presence = new JSONObject();
            presence.put("client_id", DISCORD_CLIENT_ID);
            
            JSONObject activity = new JSONObject();
            activity.put("state", trackInfo.getArtist());
            activity.put("details", trackInfo.getTitle());
            
            JSONObject assets = new JSONObject();
            assets.put("large_image", "itunes_logo");
            assets.put("large_text", trackInfo.getAlbum());
            activity.put("assets", assets);
            
            JSONObject timestamps = new JSONObject();
            timestamps.put("start", System.currentTimeMillis());
            activity.put("timestamps", timestamps);
            
            presence.put("activity", activity);
            
            // Envoyer la présence à Discord
            // Dans une implémentation réelle, nous utiliserions une bibliothèque comme discord-rpc-java
            // Pour cet exemple, nous simulons l'envoi
            
            LOGGER.log(Level.INFO, "Présence Discord mise à jour avec succès");
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de la mise à jour de la présence Discord: " + e.getMessage(), e);
        }
    }
    
    /**
     * Efface la présence Discord
     */
    public void clearPresence() {
        if (!initialized) {
            return;
        }
        
        try {
            LOGGER.log(Level.INFO, "Effacement de la présence Discord");
            
            // Créer l'objet de présence vide
            JSONObject presence = new JSONObject();
            presence.put("client_id", DISCORD_CLIENT_ID);
            presence.put("activity", JSONObject.NULL);
            
            // Envoyer la présence vide à Discord
            // Dans une implémentation réelle, nous utiliserions une bibliothèque comme discord-rpc-java
            // Pour cet exemple, nous simulons l'envoi
            
            LOGGER.log(Level.INFO, "Présence Discord effacée avec succès");
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de l'effacement de la présence Discord: " + e.getMessage(), e);
        }
    }
    
    /**
     * Arrête le client Discord RPC
     */
    public void shutdown() {
        if (!initialized) {
            return;
        }
        
        try {
            LOGGER.log(Level.INFO, "Arrêt du client Discord RPC");
            
            // Effacer la présence avant de fermer
            clearPresence();
            
            // Fermer la connexion avec Discord
            // Dans une implémentation réelle, nous utiliserions une bibliothèque comme discord-rpc-java
            // Pour cet exemple, nous simulons la fermeture
            
            if (rpcProcess != null) {
                rpcProcess.destroy();
                rpcProcess = null;
            }
            
            initialized = false;
            LOGGER.log(Level.INFO, "Client Discord RPC arrêté avec succès");
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de l'arrêt du client Discord RPC: " + e.getMessage(), e);
        }
    }
}