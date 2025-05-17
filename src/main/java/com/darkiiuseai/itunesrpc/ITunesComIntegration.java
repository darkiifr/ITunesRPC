package com.darkiiuseai.itunesrpc;

import com.sun.jna.Native;
import com.sun.jna.Pointer;
import com.sun.jna.platform.win32.COM.COMException;
import com.sun.jna.platform.win32.COM.COMUtils;
import com.sun.jna.platform.win32.COM.Unknown;
import com.sun.jna.platform.win32.Guid.CLSID;
import com.sun.jna.platform.win32.Guid.REFIID;
import com.sun.jna.platform.win32.Ole32;
import com.sun.jna.platform.win32.OleAuto;
import com.sun.jna.platform.win32.Variant;
import com.sun.jna.platform.win32.Variant.VARIANT;
import com.sun.jna.platform.win32.WTypes;
import com.sun.jna.platform.win32.WinDef.LCID;
import com.sun.jna.platform.win32.WinNT.HRESULT;
import com.sun.jna.ptr.PointerByReference;

import javafx.scene.image.Image;

import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Classe utilitaire pour l'intégration avec iTunes via COM sur Windows
 * Cette classe permet d'obtenir des informations détaillées sur les pistes en cours de lecture
 * en utilisant les interfaces COM spécifiques à iTunes
 */
public class ITunesComIntegration {
    
    private static final String ITUNES_APP_ID = "iTunes.Application";
    private static final String ITUNES_APP_CLSID = "{DC0C2640-1415-4644-875C-6F4D769839BA}";
    private static final String ITUNES_APP_IID = "{9DD6680B-3EDC-40DB-A771-E6FE4832E34A}";
    
    private Unknown iTunesApp;
    private IiTunes iTunes;
    private boolean initialized;
    private static final Logger LOGGER = Logger.getLogger(ITunesComIntegration.class.getName());
    
    /**
     * Interface COM pour l'application iTunes
     */
    @com.sun.jna.platform.win32.COM.util.IID("9DD6680B-3EDC-40DB-A771-E6FE4832E34A")
    public interface IiTunes extends Unknown {
        HRESULT get_CurrentTrack(PointerByReference outTrack);
        HRESULT get_PlayerState(IntByReference outState);
    }
    
    /**
     * Interface COM pour une piste iTunes
     */
    @com.sun.jna.platform.win32.COM.util.IID("4CB0915D-1E54-4727-BAF3-CE6CC9A225A1")
    public interface IITTrack extends Unknown {
        HRESULT get_Name(PointerByReference outName);
        HRESULT get_Artist(PointerByReference outArtist);
        HRESULT get_Album(PointerByReference outAlbum);
        HRESULT get_Duration(DoubleByReference outDuration);
        HRESULT get_TrackNumber(IntByReference outTrackNumber);
        HRESULT get_TrackCount(IntByReference outTrackCount);
        HRESULT get_Artwork(PointerByReference outArtworkCollection);
    }
    
    /**
     * Interface COM pour une collection d'illustrations d'iTunes
     */
    @com.sun.jna.platform.win32.COM.util.IID("BF2742D7-418C-4858-9AF9-2981B062D23E")
    public interface IITArtworkCollection extends Unknown {
        HRESULT get_Count(IntByReference outCount);
        HRESULT get_Item(int index, PointerByReference outArtwork);
    }
    
    /**
     * Interface COM pour une illustration d'iTunes
     */
    @com.sun.jna.platform.win32.COM.util.IID("D0D712AB-B505-4307-A7F9-4617ACCA89EA")
    public interface IITArtwork extends Unknown {
        HRESULT SaveArtworkToFile(String filePath);
    }
    
    /**
     * Classe pour gérer les entiers par référence dans JNA
     */
    public static class IntByReference extends com.sun.jna.ptr.IntByReference {
        public IntByReference() {
            super();
        }
        
        public IntByReference(int value) {
            super(value);
        }
    }
    
    /**
     * Classe pour gérer les doubles par référence dans JNA
     */
    public static class DoubleByReference extends com.sun.jna.ptr.ByReference {
        public DoubleByReference() {
            this(0.0);
        }
        
        public DoubleByReference(double value) {
            super(8);
            setValue(value);
        }
        
        public void setValue(double value) {
            getPointer().setDouble(0, value);
        }
        
        public double getValue() {
            return getPointer().getDouble(0);
        }
    }
    
    /**
     * Initialise l'intégration COM avec iTunes
     */
    public ITunesComIntegration() {
        this.initialized = false;
        try {
            // Initialiser COM
            HRESULT hr = Ole32.INSTANCE.CoInitializeEx(null, Ole32.COINIT_MULTITHREADED);
            COMUtils.checkRC(hr);
            
            LOGGER.log(Level.INFO, "Initialisation COM réussie");
            
            // Créer une instance de l'application iTunes
            PointerByReference pITunes = new PointerByReference();
            CLSID clsid = Ole32.INSTANCE.CLSIDFromString(ITUNES_APP_CLSID);
            REFIID riid = Ole32.INSTANCE.IIDFromString(ITUNES_APP_IID);
            
            hr = Ole32.INSTANCE.CoCreateInstance(
                    clsid,
                    null,
                    WTypes.CLSCTX_LOCAL_SERVER,
                    riid,
                    pITunes);
            
            if (hr.intValue() == 0) {
                iTunesApp = new Unknown(pITunes.getValue());
                iTunes = (IiTunes) iTunesApp.queryInterface(IiTunes.class);
                initialized = true;
                LOGGER.log(Level.INFO, "Instance iTunes créée avec succès");
            } else {
                LOGGER.log(Level.WARNING, "Impossible de créer l'instance iTunes, code: " + hr.intValue());
            }
        } catch (COMException e) {
            LOGGER.log(Level.SEVERE, "Erreur lors de l'initialisation de l'intégration COM avec iTunes: " + e.getMessage(), e);
        }
    }
    
    /**
     * Vérifie si iTunes est en cours d'exécution et si une piste est en cours de lecture
     */
    public boolean isITunesPlayingTrack() {
        if (!initialized || iTunes == null) return false;
        
        try {
            // Vérifier l'état du lecteur iTunes (0 = arrêté, 1 = lecture en cours)
            IntByReference playerState = new IntByReference(0);
            HRESULT hr = iTunes.get_PlayerState(playerState);
            
            if (hr.intValue() == 0) {
                // 1 = Lecture en cours
                boolean isPlaying = playerState.getValue() == 1;
                LOGGER.log(Level.INFO, "État du lecteur iTunes: " + (isPlaying ? "En lecture" : "Arrêté"));
                return isPlaying;
            } else {
                LOGGER.log(Level.WARNING, "Impossible d'obtenir l'état du lecteur iTunes, code: " + hr.intValue());
                return false;
            }
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Erreur lors de la vérification de l'état d'iTunes: " + e.getMessage(), e);
            return false;
        }
    }
    
    /**
     * Obtient les informations sur la piste en cours de lecture
     */
    public TrackInfo getCurrentTrackInfo() {
        if (!initialized || iTunes == null || !isITunesPlayingTrack()) return null;
        
        try {
            // Obtenir la piste en cours de lecture via COM
            PointerByReference pTrack = new PointerByReference();
            HRESULT hr = iTunes.get_CurrentTrack(pTrack);
            
            if (hr.intValue() != 0 || pTrack.getValue() == null) {
                LOGGER.log(Level.WARNING, "Impossible d'obtenir la piste en cours, code: " + hr.intValue());
                return null;
            }
            
            // Créer l'interface pour la piste
            IITTrack track = new Unknown(pTrack.getValue()).queryInterface(IITTrack.class);
            
            // Obtenir les propriétés de la piste
            String title = getStringProperty(track::get_Name, "Titre inconnu");
            String artist = getStringProperty(track::get_Artist, "Artiste inconnu");
            String album = getStringProperty(track::get_Album, "Album inconnu");
            
            // Obtenir la durée
            DoubleByReference durationRef = new DoubleByReference();
            hr = track.get_Duration(durationRef);
            String duration = "0:00";
            if (hr.intValue() == 0) {
                int durationInSeconds = (int) durationRef.getValue();
                int minutes = durationInSeconds / 60;
                int seconds = durationInSeconds % 60;
                duration = String.format("%d:%02d", minutes, seconds);
            }
            
            // Obtenir le numéro de piste et le total
            IntByReference trackNumberRef = new IntByReference();
            IntByReference totalTracksRef = new IntByReference();
            track.get_TrackNumber(trackNumberRef);
            track.get_TrackCount(totalTracksRef);
            int trackNumber = trackNumberRef.getValue();
            int totalTracks = totalTracksRef.getValue();
            
            // Obtenir l'image de l'album
            Image albumArt = null;
            byte[] artworkData = getArtworkData(track);
            if (artworkData != null) {
                albumArt = new Image(new ByteArrayInputStream(artworkData));
            }
            
            // Libérer la référence à la piste
            track.Release();
            
            LOGGER.log(Level.INFO, "Informations de piste récupérées: " + title + " par " + artist);
            return new TrackInfo(title, artist, album, duration, trackNumber, totalTracks, albumArt);
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Erreur lors de l'obtention des informations de piste: " + e.getMessage(), e);
            return null;
        }
    }
    
    /**
     * Obtient une propriété de type chaîne à partir d'une méthode COM
     */
    private String getStringProperty(PropertyGetter getter, String defaultValue) {
        try {
            PointerByReference pValue = new PointerByReference();
            HRESULT hr = getter.get(pValue);
            
            if (hr.intValue() == 0 && pValue.getValue() != null) {
                Pointer bstr = pValue.getValue();
                String value = OleAuto.INSTANCE.SysStringByteLen(bstr) > 0 ? 
                        bstr.getWideString(0) : defaultValue;
                OleAuto.INSTANCE.SysFreeString(bstr);
                return value;
            }
            return defaultValue;
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Erreur lors de l'obtention d'une propriété: " + e.getMessage(), e);
            return defaultValue;
        }
    }
    
    /**
     * Interface fonctionnelle pour obtenir des propriétés COM
     */
    @FunctionalInterface
    private interface PropertyGetter {
        HRESULT get(PointerByReference outValue);
    }
    
    /**
     * Obtient les données de l'image de l'album pour la piste en cours
     */
    private byte[] getArtworkData(IITTrack track) {
        try {
            // Obtenir la collection d'illustrations
            PointerByReference pArtworkCollection = new PointerByReference();
            HRESULT hr = track.get_Artwork(pArtworkCollection);
            
            if (hr.intValue() != 0 || pArtworkCollection.getValue() == null) {
                LOGGER.log(Level.INFO, "Aucune illustration disponible pour cette piste");
                return null;
            }
            
            // Créer l'interface pour la collection d'illustrations
            IITArtworkCollection artworkCollection = new Unknown(pArtworkCollection.getValue())
                    .queryInterface(IITArtworkCollection.class);
            
            // Vérifier s'il y a des illustrations
            IntByReference countRef = new IntByReference();
            hr = artworkCollection.get_Count(countRef);
            
            if (hr.intValue() != 0 || countRef.getValue() <= 0) {
                artworkCollection.Release();
                LOGGER.log(Level.INFO, "Aucune illustration dans la collection");
                return null;
            }
            
            // Obtenir la première illustration
            PointerByReference pArtwork = new PointerByReference();
            hr = artworkCollection.get_Item(1, pArtwork); // Les index COM commencent à 1
            
            if (hr.intValue() != 0 || pArtwork.getValue() == null) {
                artworkCollection.Release();
                LOGGER.log(Level.WARNING, "Impossible d'obtenir l'illustration, code: " + hr.intValue());
                return null;
            }
            
            // Créer l'interface pour l'illustration
            IITArtwork artwork = new Unknown(pArtwork.getValue()).queryInterface(IITArtwork.class);
            
            // Créer un fichier temporaire pour stocker l'image
            Path tempFile = Files.createTempFile("itunes_artwork_", ".jpg");
            
            // Sauvegarder l'illustration dans le fichier temporaire
            hr = artwork.SaveArtworkToFile(tempFile.toString());
            
            // Libérer les ressources COM
            artwork.Release();
            artworkCollection.Release();
            
            if (hr.intValue() != 0) {
                Files.deleteIfExists(tempFile);
                LOGGER.log(Level.WARNING, "Impossible de sauvegarder l'illustration, code: " + hr.intValue());
                return null;
            }
            
            // Lire le fichier en bytes
            byte[] imageData = Files.readAllBytes(tempFile);
            
            // Supprimer le fichier temporaire
            Files.deleteIfExists(tempFile);
            
            LOGGER.log(Level.INFO, "Illustration récupérée avec succès");
            return imageData;
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Erreur lors de l'extraction de l'image de l'album: " + e.getMessage(), e);
            return null;
        }
    }
    
    /**
     * Libère les ressources COM
     */
    public void release() {
        if (initialized) {
            // Libérer les ressources COM
            try {
                if (iTunes != null) {
                    iTunes.Release();
                    iTunes = null;
                    LOGGER.log(Level.INFO, "Interface iTunes libérée");
                }
                
                if (iTunesApp != null) {
                    iTunesApp.Release();
                    iTunesApp = null;
                    LOGGER.log(Level.INFO, "Instance iTunes libérée");
                }
                
                // Désinitialiser COM
                Ole32.INSTANCE.CoUninitialize();
                LOGGER.log(Level.INFO, "COM désinitalisé");
            } catch (Exception e) {
                LOGGER.log(Level.WARNING, "Erreur lors de la libération des ressources COM: " + e.getMessage(), e);
            } finally {
                initialized = false;
            }
        }
    }
}
}