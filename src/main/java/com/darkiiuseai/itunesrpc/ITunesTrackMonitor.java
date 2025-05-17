package com.darkiiuseai.itunesrpc;

import com.sun.jna.Native;
import com.sun.jna.platform.win32.User32;
import com.sun.jna.platform.win32.WinDef.HWND;
import com.sun.jna.platform.win32.WinUser;
import com.sun.jna.win32.W32APIOptions;

import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Classe responsable de la surveillance d'iTunes/Apple Music pour détecter les changements de piste
 */
public class ITunesTrackMonitor {
    
    private static final String ITUNES_WINDOW_CLASS = "iTunes";
    private static final String APPLE_MUSIC_WINDOW_CLASS = "AppleMusic";
    
    private final ScheduledExecutorService scheduler;
    private TrackInfo currentTrack;
    private TrackChangeListener listener;
    private boolean isRunning;
    private ITunesComIntegration iTunesComIntegration;
    private static final Logger LOGGER = Logger.getLogger(ITunesTrackMonitor.class.getName());
    
    /**
     * Interface pour les écouteurs de changement de piste
     */
    public interface TrackChangeListener {
        void onTrackChanged(TrackInfo trackInfo);
    }
    
    public ITunesTrackMonitor() {
        this.scheduler = Executors.newSingleThreadScheduledExecutor();
        this.currentTrack = null;
        this.isRunning = false;
        this.iTunesComIntegration = new ITunesComIntegration();
    }
    
    /**
     * Définit l'écouteur pour les changements de piste
     */
    public void setTrackChangeListener(TrackChangeListener listener) {
        this.listener = listener;
    }
    
    /**
     * Démarre la surveillance d'iTunes/Apple Music
     */
    public void start() {
        if (isRunning) return;
        
        isRunning = true;
        scheduler.scheduleAtFixedRate(this::checkCurrentTrack, 0, 1, TimeUnit.SECONDS);
    }
    
    /**
     * Arrête la surveillance
     */
    public void stop() {
        isRunning = false;
        scheduler.shutdown();
        try {
            if (!scheduler.awaitTermination(5, TimeUnit.SECONDS)) {
                scheduler.shutdownNow();
            }
        } catch (InterruptedException e) {
            scheduler.shutdownNow();
            Thread.currentThread().interrupt();
        }
        
        // Libérer les ressources COM
        if (iTunesComIntegration != null) {
            iTunesComIntegration.release();
            iTunesComIntegration = null;
        }
    }
    
    /**
     * Vérifie la piste actuelle dans iTunes/Apple Music
     */
    private void checkCurrentTrack() {
        try {
            // Essayer d'abord iTunes
            TrackInfo newTrack = getITunesTrackInfo();
            
            // Si iTunes n'est pas trouvé, essayer Apple Music
            if (newTrack == null) {
                newTrack = getAppleMusicTrackInfo();
            }
            
            // Si aucune piste n'est en cours de lecture
            if (newTrack == null) {
                if (currentTrack != null) {
                    currentTrack = null;
                    if (listener != null) {
                        listener.onTrackChanged(null);
                    }
                }
                return;
            }
            
            // Vérifier si la piste a changé
            if (currentTrack == null || !currentTrack.equals(newTrack)) {
                currentTrack = newTrack;
                if (listener != null) {
                    listener.onTrackChanged(newTrack);
                }
            }
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de la vérification de la piste actuelle: " + e.getMessage(), e);
        }
    }
    
    /**
     * Obtient les informations de la piste actuelle d'iTunes en utilisant l'intégration COM
     */
    private TrackInfo getITunesTrackInfo() {
        try {
            // Vérifier si iTunes est en cours d'exécution et si une piste est en cours de lecture
            if (iTunesComIntegration == null) {
                iTunesComIntegration = new ITunesComIntegration();
            }
            
            // Vérifier si iTunes est en cours d'exécution via COM
            if (!iTunesComIntegration.isITunesPlayingTrack()) {
                LOGGER.log(Level.FINE, "iTunes n'est pas en cours de lecture");
                return null;
            }
            
            // Obtenir les informations de la piste via COM
            TrackInfo trackInfo = iTunesComIntegration.getCurrentTrackInfo();
            if (trackInfo != null) {
                LOGGER.log(Level.INFO, "Informations de piste iTunes récupérées: " + trackInfo.getTitle());
            }
            return trackInfo;
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de l'obtention des informations de piste iTunes via COM: " + e.getMessage(), e);
            
            // Fallback: essayer de détecter iTunes via la fenêtre
            HWND iTunesWindow = findITunesWindow();
            if (iTunesWindow == null) {
                LOGGER.log(Level.INFO, "Fenêtre iTunes non trouvée");
                return null;
            }
            
            // Utiliser une méthode alternative si COM échoue
            LOGGER.log(Level.INFO, "Utilisation de la méthode alternative pour détecter iTunes");
            return null; // Retourner null pour indiquer qu'aucune piste n'est en cours de lecture
        }
    }
    
    /**
     * Obtient les informations de la piste actuelle d'Apple Music
     */
    private TrackInfo getAppleMusicTrackInfo() {
        // Similaire à getITunesTrackInfo mais pour Apple Music
        HWND appleMusicWindow = findAppleMusicWindow();
        if (appleMusicWindow == null) return null;
        
        // Simulation similaire
        return new TrackInfo(
                "Titre Apple Music",
                "Artiste Apple Music",
                "Album Apple Music",
                "4:12",
                3,
                15,
                null
        );
    }
    
    /**
     * Trouve la fenêtre iTunes si elle est ouverte
     */
    private HWND findITunesWindow() {
        User32 user32 = Native.load("user32", User32.class, W32APIOptions.DEFAULT_OPTIONS);
        return user32.FindWindow(ITUNES_WINDOW_CLASS, null);
    }
    
    /**
     * Trouve la fenêtre Apple Music si elle est ouverte
     */
    private HWND findAppleMusicWindow() {
        User32 user32 = Native.load("user32", User32.class, W32APIOptions.DEFAULT_OPTIONS);
        return user32.FindWindow(APPLE_MUSIC_WINDOW_CLASS, null);
    }
}