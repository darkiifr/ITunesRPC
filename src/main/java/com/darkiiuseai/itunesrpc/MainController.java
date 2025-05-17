package com.darkiiuseai.itunesrpc;

import javafx.application.Platform;
import javafx.fxml.FXML;
import javafx.scene.control.Label;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.layout.VBox;

/**
 * Contrôleur pour la vue principale de l'application
 */
public class MainController {
    
    @FXML private Label titleLabel;
    @FXML private Label artistLabel;
    @FXML private Label albumLabel;
    @FXML private Label positionLabel;
    @FXML private Label durationLabel;
    @FXML private ImageView albumArtView;
    @FXML private VBox noTrackBox;
    @FXML private VBox trackInfoBox;
    
    private static final Image DEFAULT_ALBUM_ART = new Image(
            MainController.class.getResourceAsStream("/images/default_album.png"));
    
    /**
     * Initialise le contrôleur
     */
    @FXML
    public void initialize() {
        // Afficher l'état initial (aucune piste en lecture)
        showNoTrackPlaying();
    }
    
    /**
     * Met à jour l'interface avec les informations de la piste actuelle
     * 
     * @param trackInfo Les informations de la piste, ou null si aucune piste n'est en lecture
     */
    public void updateTrackInfo(TrackInfo trackInfo) {
        // Exécuter sur le thread JavaFX
        Platform.runLater(() -> {
            if (trackInfo == null) {
                showNoTrackPlaying();
                return;
            }
            
            // Mettre à jour les labels avec les informations de la piste
            titleLabel.setText(trackInfo.getTitle());
            artistLabel.setText(trackInfo.getArtist());
            albumLabel.setText(trackInfo.getAlbum());
            positionLabel.setText(trackInfo.getTrackPosition());
            durationLabel.setText(trackInfo.getDuration());
            
            // Mettre à jour l'image de l'album
            Image albumArt = trackInfo.getAlbumArt();
            albumArtView.setImage(albumArt != null ? albumArt : DEFAULT_ALBUM_ART);
            
            // Afficher les informations de la piste
            noTrackBox.setVisible(false);
            trackInfoBox.setVisible(true);
        });
    }
    
    /**
     * Affiche l'état "Aucune piste en lecture"
     */
    private void showNoTrackPlaying() {
        noTrackBox.setVisible(true);
        trackInfoBox.setVisible(false);
        albumArtView.setImage(DEFAULT_ALBUM_ART);
    }
    
    /**
     * Active ou désactive le mode silencieux
     */
    private void toggleSilentMode() {
        if (mainApp != null && mainApp.getSilentModeManager() != null) {
            boolean silentMode = silentModeCheckBox.isSelected();
            mainApp.getSilentModeManager().setSilentMode(silentMode);
            LOGGER.log(Level.INFO, "Mode silencieux " + (silentMode ? "activé" : "désactivé"));
        }
    }
    
    /**
     * Active ou désactive le démarrage automatique
     */
    private void toggleStartup() {
        if (mainApp != null && mainApp.getSilentModeManager() != null) {
            boolean startupEnabled = startupCheckBox.isSelected();
            boolean success = mainApp.getSilentModeManager().setStartupEnabled(startupEnabled);
            
            if (success) {
                LOGGER.log(Level.INFO, "Démarrage automatique " + (startupEnabled ? "activé" : "désactivé"));
            } else {
                LOGGER.log(Level.WARNING, "Échec de la configuration du démarrage automatique");
                // Rétablir l'état précédent de la case à cocher
                startupCheckBox.setSelected(!startupEnabled);
            }
        }
    }
}