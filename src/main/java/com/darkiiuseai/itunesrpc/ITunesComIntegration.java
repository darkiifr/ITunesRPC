package com.darkiiuseai.itunesrpc;

import com.sun.jna.platform.win32.COM.COMException;
import com.sun.jna.platform.win32.COM.COMUtils;
import com.sun.jna.platform.win32.Ole32;
import com.sun.jna.platform.win32.WTypes;
import com.sun.jna.platform.win32.WinDef;
import com.sun.jna.platform.win32.WinNT;
import com.sun.jna.ptr.PointerByReference;

import javafx.scene.image.Image;

import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Classe responsable de l'intégration avec iTunes via l'API COM de Windows
 */
public class ITunesComIntegration {
    private static final Logger LOGGER = Logger.getLogger(ITunesComIntegration.class.getName());
    private static final String ITUNES_APP_ID = "iTunes.Application";
    private static final String TEMP_ARTWORK_FILE = System.getProperty("java.io.tmpdir") + "/itunes_artwork_temp.jpg";
    
    private PointerByReference iTunesApp;
    private boolean initialized;
    
    /**
     * Constructeur par défaut qui initialise l'intégration COM
     */
    public ITunesComIntegration() {
        try {
            // Initialiser la bibliothèque COM
            LOGGER.log(Level.INFO, "Initialisation de la bibliothèque COM");
            Ole32.INSTANCE.CoInitializeEx(null, Ole32.COINIT_MULTITHREADED);
            
            // Créer une instance de l'application iTunes
            iTunesApp = new PointerByReference();
            initialized = false;
            
            // Tenter de se connecter à iTunes
            connectToITunes();
            
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Erreur lors de l'initialisation de l'intégration COM: " + e.getMessage(), e);
            release();
            throw new RuntimeException("Impossible d'initialiser l'intégration COM avec iTunes", e);
        }
    }
    
    /**
     * Tente de se connecter à iTunes via COM
     */
    private void connectToITunes() {
        try {
            LOGGER.log(Level.INFO, "Tentative de connexion à iTunes");
            
            // Créer une instance de l'application iTunes
            WinNT.HRESULT hr = Ole32.INSTANCE.CoCreateInstance(
                    new WTypes.GUID("{DC0C2640-1415-4644-875C-6F4D769839BA}"), // CLSID de iTunes.Application
                    null,
                    WTypes.CLSCTX_LOCAL_SERVER,
                    new WTypes.GUID("{00000000-0000-0000-C000-000000000046}"), // IID de IUnknown
                    iTunesApp);
            
            if (COMUtils.SUCCEEDED(hr)) {
                LOGGER.log(Level.INFO, "Connexion à iTunes réussie");
                initialized = true;
            } else {
                LOGGER.log(Level.WARNING, "Échec de la connexion à iTunes via COM: code " + hr.intValue());
            }
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de la connexion à iTunes: " + e.getMessage(), e);
            initialized = false;
        }
    }
    
    /**
     * Vérifie si iTunes est en cours d'exécution
     * @return true si iTunes est en cours d'exécution, false sinon
     */
    public boolean isITunesRunning() {
        if (!initialized) {
            // Tenter de se reconnecter si nécessaire
            try {
                connectToITunes();
            } catch (Exception e) {
                return false;
            }
        }
        return initialized;
    }
    
    /**
     * Obtient les informations de la piste en cours de lecture
     * @return Les informations de la piste ou null si aucune piste n'est en lecture
     */
    public TrackInfo getCurrentTrackInfo() {
        if (!isITunesRunning()) {
            return null;
        }
        
        try {
            // Vérifier l'état du lecteur
            int playerState = getPlayerState();
            if (playerState != 1) { // 1 = en lecture
                LOGGER.log(Level.INFO, "iTunes n'est pas en lecture (état: " + playerState + ")");
                return null;
            }
            
            // Obtenir la piste en cours
            PointerByReference currentTrack = getCurrentTrack();
            if (currentTrack == null) {
                LOGGER.log(Level.INFO, "Aucune piste en cours");
                return null;
            }
            
            // Extraire les métadonnées de la piste
            String title = getTrackProperty(currentTrack, "Name");
            String artist = getTrackProperty(currentTrack, "Artist");
            String album = getTrackProperty(currentTrack, "Album");
            int trackNumber = getTrackPropertyInt(currentTrack, "TrackNumber");
            int trackCount = getTrackPropertyInt(currentTrack, "TrackCount");
            int durationSeconds = getTrackPropertyInt(currentTrack, "Duration");
            
            // Formater la durée
            String duration = formatDuration(durationSeconds);
            
            // Obtenir l'image de l'album
            Image albumArt = getAlbumArtwork(currentTrack);
            
            // Libérer la référence à la piste
            releaseComObject(currentTrack);
            
            // Créer et retourner l'objet TrackInfo
            return new TrackInfo(title, artist, album, duration, trackNumber, trackCount, albumArt);
            
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de l'obtention des informations de piste: " + e.getMessage(), e);
            return null;
        }
    }
    
    /**
     * Obtient l'état du lecteur iTunes
     * @return L'état du lecteur (0 = arrêté, 1 = en lecture, 2 = en pause)
     */
    private int getPlayerState() {
        try {
            // Appel COM pour obtenir l'état du lecteur
            // Simulation pour l'exemple
            return 1; // Simuler l'état "en lecture"
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de l'obtention de l'état du lecteur: " + e.getMessage(), e);
            return 0;
        }
    }
    
    /**
     * Obtient la piste en cours de lecture
     * @return Une référence à la piste en cours ou null si aucune piste n'est en cours
     */
    private PointerByReference getCurrentTrack() {
        try {
            // Appel COM pour obtenir la piste en cours
            // Simulation pour l'exemple
            return new PointerByReference(); // Simuler une référence à une piste
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de l'obtention de la piste en cours: " + e.getMessage(), e);
            return null;
        }
    }
    
    /**
     * Obtient une propriété de type chaîne d'une piste
     * @param track Référence à la piste
     * @param propertyName Nom de la propriété
     * @return La valeur de la propriété ou une chaîne vide en cas d'erreur
     */
    private String getTrackProperty(PointerByReference track, String propertyName) {
        try {
            // Appel COM pour obtenir la propriété
            // Simulation pour l'exemple
            switch (propertyName) {
                case "Name":
                    return "Titre de la piste";
                case "Artist":
                    return "Artiste";
                case "Album":
                    return "Album";
                default:
                    return "";
            }
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de l'obtention de la propriété '" + propertyName + "': " + e.getMessage(), e);
            return "";
        }
    }
    
    /**
     * Obtient une propriété de type entier d'une piste
     * @param track Référence à la piste
     * @param propertyName Nom de la propriété
     * @return La valeur de la propriété ou 0 en cas d'erreur
     */
    private int getTrackPropertyInt(PointerByReference track, String propertyName) {
        try {
            // Appel COM pour obtenir la propriété
            // Simulation pour l'exemple
            switch (propertyName) {
                case "TrackNumber":
                    return 1;
                case "TrackCount":
                    return 10;
                case "Duration":
                    return 180; // 3 minutes
                default:
                    return 0;
            }
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de l'obtention de la propriété '" + propertyName + "': " + e.getMessage(), e);
            return 0;
        }
    }
    
    /**
     * Obtient l'image de l'album d'une piste
     * @param track Référence à la piste
     * @return L'image de l'album ou null en cas d'erreur
     */
    private Image getAlbumArtwork(PointerByReference track) {
        try {
            // Appel COM pour obtenir l'illustration de l'album
            // Pour l'exemple, nous utilisons une image par défaut
            return new Image(getClass().getResourceAsStream("/images/default_album.png"));
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de l'obtention de l'image de l'album: " + e.getMessage(), e);
            return null;
        }
    }
    
    /**
     * Formate une durée en secondes en format mm:ss
     * @param seconds Durée en secondes
     * @return Durée formatée
     */
    private String formatDuration(int seconds) {
        int minutes = seconds / 60;
        int remainingSeconds = seconds % 60;
        return String.format("%d:%02d", minutes, remainingSeconds);
    }
    
    /**
     * Libère un objet COM
     * @param obj Référence à l'objet COM
     */
    private void releaseComObject(PointerByReference obj) {
        if (obj != null) {
            try {
                // Libérer l'objet COM
                // Dans une implémentation réelle, nous utiliserions IUnknown.Release()
            } catch (Exception e) {
                LOGGER.log(Level.WARNING, "Erreur lors de la libération de l'objet COM: " + e.getMessage(), e);
            }
        }
    }
    
    /**
     * Libère toutes les ressources COM
     */
    public void release() {
        try {
            if (iTunesApp != null) {
                releaseComObject(iTunesApp);
                iTunesApp = null;
            }
            
            // Libérer la bibliothèque COM
            Ole32.INSTANCE.CoUninitialize();
            initialized = false;
            LOGGER.log(Level.INFO, "Ressources COM libérées avec succès");
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de la libération des ressources COM: " + e.getMessage(), e);
        }
    }
}