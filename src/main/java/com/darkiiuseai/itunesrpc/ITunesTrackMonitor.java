package com.darkiiuseai.itunesrpc;

import javafx.application.Platform;
import javafx.scene.image.Image;

import java.util.Timer;
import java.util.TimerTask;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Classe responsable de la surveillance des changements de piste dans iTunes
 * et de la mise à jour des informations de présence Discord
 */
public class ITunesTrackMonitor {
    private static final Logger LOGGER = Logger.getLogger(ITunesTrackMonitor.class.getName());
    private static final int POLLING_INTERVAL = 2000; // Intervalle de vérification en millisecondes
    
    private final AtomicBoolean running = new AtomicBoolean(false);
    private Timer timer;
    private TrackChangeListener trackChangeListener;
    private ITunesComIntegration itunesIntegration;
    private DiscordRpcClient discordClient;
    private TrackInfo currentTrack;
    
    /**
     * Interface pour les écouteurs de changement de piste
     */
    public interface TrackChangeListener {
        void onTrackChange(TrackInfo trackInfo);
    }
    
    /**
     * Constructeur par défaut
     */
    public ITunesTrackMonitor() {
        try {
            this.itunesIntegration = new ITunesComIntegration();
            this.discordClient = new DiscordRpcClient();
            LOGGER.log(Level.INFO, "ITunesTrackMonitor initialisé avec succès");
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Erreur lors de l'initialisation de ITunesTrackMonitor: " + e.getMessage(), e);
            throw new RuntimeException("Impossible d'initialiser le moniteur de piste iTunes", e);
        }
    }
    
    /**
     * Définit l'écouteur de changement de piste
     * @param listener L'écouteur à définir
     */
    public void setTrackChangeListener(TrackChangeListener listener) {
        this.trackChangeListener = listener;
    }
    
    /**
     * Démarre la surveillance des pistes iTunes
     */
    public void start() {
        if (running.compareAndSet(false, true)) {
            LOGGER.log(Level.INFO, "Démarrage du moniteur de piste iTunes");
            
            // Initialiser le client Discord RPC
            try {
                discordClient.initialize();
                LOGGER.log(Level.INFO, "Client Discord RPC initialisé avec succès");
            } catch (Exception e) {
                LOGGER.log(Level.WARNING, "Erreur lors de l'initialisation du client Discord RPC: " + e.getMessage(), e);
                // Continuer même si Discord n'est pas disponible
            }
            
            // Démarrer la tâche de surveillance
            timer = new Timer(true);
            timer.scheduleAtFixedRate(new TimerTask() {
                @Override
                public void run() {
                    checkCurrentTrack();
                }
            }, 0, POLLING_INTERVAL);
        }
    }
    
    /**
     * Arrête la surveillance des pistes iTunes
     */
    public void stop() {
        if (running.compareAndSet(true, false)) {
            LOGGER.log(Level.INFO, "Arrêt du moniteur de piste iTunes");
            
            if (timer != null) {
                timer.cancel();
                timer = null;
            }
            
            // Fermer le client Discord RPC
            try {
                if (discordClient != null) {
                    discordClient.shutdown();
                    LOGGER.log(Level.INFO, "Client Discord RPC arrêté avec succès");
                }
            } catch (Exception e) {
                LOGGER.log(Level.WARNING, "Erreur lors de l'arrêt du client Discord RPC: " + e.getMessage(), e);
            }
            
            // Libérer les ressources COM
            try {
                if (itunesIntegration != null) {
                    itunesIntegration.release();
                    LOGGER.log(Level.INFO, "Ressources COM libérées avec succès");
                }
            } catch (Exception e) {
                LOGGER.log(Level.WARNING, "Erreur lors de la libération des ressources COM: " + e.getMessage(), e);
            }
        }
    }
    
    /**
     * Vérifie la piste en cours de lecture dans iTunes
     */
    private void checkCurrentTrack() {
        try {
            // Vérifier si iTunes est en cours d'exécution
            if (!itunesIntegration.isITunesRunning()) {
                if (currentTrack != null) {
                    currentTrack = null;
                    updateTrackInfo(null);
                }
                return;
            }
            
            // Obtenir les informations de la piste en cours
            TrackInfo newTrack = itunesIntegration.getCurrentTrackInfo();
            
            // Si aucune piste n'est en lecture ou si la piste a changé
            if ((newTrack == null && currentTrack != null) || 
                (newTrack != null && !newTrack.equals(currentTrack))) {
                
                currentTrack = newTrack;
                updateTrackInfo(newTrack);
            }
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de la vérification de la piste en cours: " + e.getMessage(), e);
        }
    }
    
    /**
     * Met à jour les informations de piste et notifie les écouteurs
     * @param trackInfo Les nouvelles informations de piste
     */
    private void updateTrackInfo(final TrackInfo trackInfo) {
        // Mettre à jour la présence Discord
        try {
            if (trackInfo != null) {
                discordClient.updatePresence(trackInfo);
                LOGGER.log(Level.INFO, "Présence Discord mise à jour: " + trackInfo.getTitle());
            } else {
                discordClient.clearPresence();
                LOGGER.log(Level.INFO, "Présence Discord effacée");
            }
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de la mise à jour de la présence Discord: " + e.getMessage(), e);
        }
        
        // Notifier les écouteurs sur le thread JavaFX
        if (trackChangeListener != null) {
            Platform.runLater(() -> trackChangeListener.onTrackChange(trackInfo));
        }
    }
}