package com.darkiiuseai.itunesrpc;

import javafx.application.Platform;
import javafx.fxml.FXML;
import javafx.scene.control.Button;
import javafx.scene.control.CheckBox;
import javafx.scene.control.Label;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.layout.VBox;

import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Contrôleur pour l'interface utilisateur principale
 */
public class MainController {
    private static final Logger LOGGER = Logger.getLogger(MainController.class.getName());
    
    @FXML private Label titleLabel;
    @FXML private Label artistLabel;
    @FXML private Label albumLabel;
    @FXML private Label positionLabel;
    @FXML private Label durationLabel;
    @FXML private ImageView albumArtView;
    @FXML private CheckBox startupCheckBox;
    @FXML private CheckBox silentModeCheckBox;
    @FXML private VBox noTrackBox;
    @FXML private VBox trackInfoBox;
    @FXML private Button serviceButton;
    
    private Main mainApp;
    private boolean serviceRunning = true;
    
    /**
     * Initialise le contrôleur
     */
    @FXML
    private void initialize() {
        LOGGER.log(Level.INFO, "Initialisation du contrôleur principal");
        
        // Initialiser les composants de l'interface utilisateur
        noTrackBox.setVisible(true);
        trackInfoBox.setVisible(false);
        
        // Initialiser le texte du bouton de service
        serviceButton.setText(serviceRunning ? "Arrêter le service" : "Démarrer le service");
        
        // Configurer les écouteurs d'événements
        startupCheckBox.setOnAction(event -> toggleStartWithWindows());
        silentModeCheckBox.setOnAction(event -> toggleSilentMode());
        
        // Charger l'image par défaut pour l'album
        try {
            Image defaultImage = new Image(getClass().getResourceAsStream("/images/default_album.png"));
            albumArtView.setImage(defaultImage);
        } catch (Exception e) {
            LOGGER.log(Level.WARNING, "Impossible de charger l'image par défaut: " + e.getMessage(), e);
        }
    }
    
    /**
     * Définit l'application principale
     * @param mainApp L'application principale
     */
    public void setMainApp(Main mainApp) {
        this.mainApp = mainApp;
        
        // Mettre à jour l'état des cases à cocher en fonction des paramètres actuels
        Platform.runLater(() -> {
            silentModeCheckBox.setSelected(mainApp.getSilentModeManager().isSilentMode());
            startupCheckBox.setSelected(mainApp.getSilentModeManager().isStartupEnabled());
        });
    }
    
    /**
     * Met à jour les informations de piste affichées
     * @param trackInfo Les informations de la piste
     */
    public void updateTrackInfo(TrackInfo trackInfo) {
        Platform.runLater(() -> {
            if (trackInfo != null) {
                // Afficher les informations de la piste
                titleLabel.setText(trackInfo.getTitle());
                artistLabel.setText(trackInfo.getArtist());
                albumLabel.setText(trackInfo.getAlbum());
                positionLabel.setText(trackInfo.getTrackPosition());
                durationLabel.setText(trackInfo.getDuration());
                
                // Afficher l'image de l'album si disponible
                if (trackInfo.getAlbumArt() != null) {
                    albumArtView.setImage(trackInfo.getAlbumArt());
                } else {
                    // Utiliser l'image par défaut
                    try {
                        Image defaultImage = new Image(getClass().getResourceAsStream("/images/default_album.png"));
                        albumArtView.setImage(defaultImage);
                    } catch (Exception e) {
                        LOGGER.log(Level.WARNING, "Impossible de charger l'image par défaut: " + e.getMessage(), e);
                    }
                }
                
                // Afficher la boîte d'informations de piste
                noTrackBox.setVisible(false);
                trackInfoBox.setVisible(true);
                
                LOGGER.log(Level.INFO, "Informations de piste mises à jour: " + trackInfo.getTitle());
            } else {
                // Aucune piste en lecture, afficher le message approprié
                noTrackBox.setVisible(true);
                trackInfoBox.setVisible(false);
                
                LOGGER.log(Level.INFO, "Aucune piste en lecture");
            }
        });
    }
    
    /**
     * Bascule l'état du service (démarrage/arrêt)
     */
    @FXML
    private void toggleService() {
        if (serviceRunning) {
            // Arrêter le service
            serviceRunning = false;
            LOGGER.log(Level.INFO, "Service arrêté par l'utilisateur");
            
            // Mettre à jour l'interface utilisateur
            noTrackBox.setVisible(true);
            trackInfoBox.setVisible(false);
            serviceButton.setText("Démarrer le service");
            
            // Arrêter le moniteur de piste
            if (mainApp != null && mainApp.getTrackMonitor() != null) {
                mainApp.getTrackMonitor().stop();
                LOGGER.log(Level.INFO, "Moniteur de piste iTunes arrêté");
            }
        } else {
            // Démarrer le service
            serviceRunning = true;
            LOGGER.log(Level.INFO, "Service démarré par l'utilisateur");
            
            // Mettre à jour l'interface utilisateur
            serviceButton.setText("Arrêter le service");
            
            // Démarrer le moniteur de piste
            if (mainApp != null && mainApp.getTrackMonitor() != null) {
                mainApp.getTrackMonitor().start();
                LOGGER.log(Level.INFO, "Moniteur de piste iTunes démarré");
            }
        }
    }
    
    /**
     * Bascule l'option de démarrage avec Windows
     */
    private void toggleStartWithWindows() {
        boolean startWithWindows = startupCheckBox.isSelected();
        LOGGER.log(Level.INFO, "Option de démarrage avec Windows définie sur: " + startWithWindows);
        
        if (mainApp != null && mainApp.getSilentModeManager() != null) {
            mainApp.getSilentModeManager().setStartupEnabled(startWithWindows);
        }
    }
    
    /**
     * Bascule le mode silencieux
     */
    private void toggleSilentMode() {
        boolean silentMode = silentModeCheckBox.isSelected();
        LOGGER.log(Level.INFO, "Mode silencieux défini sur: " + silentMode);
        
        if (mainApp != null && mainApp.getSilentModeManager() != null) {
            mainApp.getSilentModeManager().setSilentMode(silentMode);
        }
    }
}