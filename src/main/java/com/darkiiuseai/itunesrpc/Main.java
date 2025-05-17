package com.darkiiuseai.itunesrpc;

import javafx.application.Application;
import javafx.application.Platform;
import javafx.fxml.FXMLLoader;
import javafx.scene.Parent;
import javafx.scene.Scene;
import javafx.scene.control.Alert;
import javafx.scene.control.ButtonType;
import javafx.scene.image.Image;
import javafx.stage.Stage;

import java.io.IOException;
import java.util.Arrays;
import java.util.List;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Point d'entrée principal de l'application ITunesRPC
 */
public class Main extends Application {
    
    private ITunesTrackMonitor trackMonitor;
    private AutoUpdater autoUpdater;
    private SilentModeManager silentModeManager;
    private static final Logger LOGGER = Logger.getLogger(Main.class.getName());
    
    // Arguments de ligne de commande
    private static boolean silentMode = false;
    private static boolean startupMode = false;
    
    /**
     * Point d'entrée principal de l'application
     */
    public static void main(String[] args) {
        LOGGER.log(Level.INFO, "Démarrage de l'application avec les arguments: " + Arrays.toString(args));
        
        // Analyser les arguments de ligne de commande
        for (String arg : args) {
            if ("-silent".equalsIgnoreCase(arg)) {
                silentMode = true;
            } else if ("-startup".equalsIgnoreCase(arg)) {
                startupMode = true;
            }
        }
        
        // Vérifier les privilèges administrateur
        if (!AdminUtils.isRunAsAdmin()) {
            LOGGER.log(Level.WARNING, "L'application n'est pas exécutée en tant qu'administrateur");
            
            // En mode silencieux ou démarrage, relancer automatiquement en admin
            if (silentMode || startupMode) {
                LOGGER.log(Level.INFO, "Relance automatique en tant qu'administrateur (mode silencieux/démarrage)");
                AdminUtils.restartAsAdmin(silentMode, startupMode);
                System.exit(0);
                return;
            }
            
            // En mode normal, demander à l'utilisateur s'il souhaite relancer en admin
            // Nous devons lancer l'application JavaFX pour afficher la boîte de dialogue
            // La demande sera affichée dans la méthode start()
        }
        
        launch(args);
    }
    
    @Override
    public void start(Stage primaryStage) {
        try {
            LOGGER.log(Level.INFO, "Démarrage de l'application iTunes RPC");
            
            // Vérifier les privilèges administrateur en mode graphique
            if (!AdminUtils.isRunAsAdmin() && !silentMode && !startupMode) {
                Alert alert = new Alert(Alert.AlertType.CONFIRMATION);
                alert.setTitle("Privilèges administrateur requis");
                alert.setHeaderText("L'application nécessite des privilèges administrateur");
                alert.setContentText("Pour accéder correctement à l'API COM d'iTunes, l'application doit être exécutée en tant qu'administrateur. Souhaitez-vous relancer l'application avec des privilèges administrateur ?");
                
                if (alert.showAndWait().orElse(ButtonType.CANCEL) == ButtonType.OK) {
                    LOGGER.log(Level.INFO, "L'utilisateur a accepté de relancer l'application en tant qu'administrateur");
                    AdminUtils.restartAsAdmin(false, false);
                    Platform.exit();
                    return;
                } else {
                    LOGGER.log(Level.WARNING, "L'utilisateur a refusé de relancer l'application en tant qu'administrateur");
                }
            }
            
            // Vérifier les mises à jour au démarrage
            try {
                autoUpdater = new AutoUpdater("darkiiuseai", "ITunesRPC");
                autoUpdater.checkForUpdates();
                LOGGER.log(Level.INFO, "Vérification des mises à jour terminée");
            } catch (Exception e) {
                LOGGER.log(Level.WARNING, "Erreur lors de la vérification des mises à jour: " + e.getMessage(), e);
                // Continuer l'exécution même si la vérification des mises à jour échoue
            }
            
            // Charger l'interface utilisateur
            FXMLLoader loader = new FXMLLoader(getClass().getResource("/fxml/MainView.fxml"));
            Parent root = loader.load();
            
            // Configurer le contrôleur
            MainController controller = loader.getController();
            controller.setMainApp(this);
            LOGGER.log(Level.INFO, "Interface utilisateur chargée avec succès");
            
            // Démarrer le moniteur de piste iTunes
            try {
                trackMonitor = new ITunesTrackMonitor();
                trackMonitor.setTrackChangeListener(trackInfo -> {
                    if (trackInfo != null) {
                        LOGGER.log(Level.INFO, "Nouvelle piste détectée: " + trackInfo.getTitle() + " par " + trackInfo.getArtist());
                    } else {
                        LOGGER.log(Level.INFO, "Aucune piste en lecture");
                    }
                    controller.updateTrackInfo(trackInfo);
                });
                trackMonitor.start();
                LOGGER.log(Level.INFO, "Moniteur de piste iTunes démarré avec succès");
            } catch (Exception e) {
                LOGGER.log(Level.SEVERE, "Erreur lors de l'initialisation du moniteur de piste iTunes: " + e.getMessage(), e);
                // Afficher un message d'erreur à l'utilisateur
                controller.updateTrackInfo(null);
            }
            
            // Initialiser le gestionnaire de mode silencieux
            silentModeManager = new SilentModeManager(silentMode);
            silentModeManager.setTrackMonitor(trackMonitor);
            
            // En mode silencieux, ne pas afficher l'interface graphique
            if (silentMode) {
                LOGGER.log(Level.INFO, "Exécution en mode silencieux");
                silentModeManager.runInSilentMode();
            } else {
                // Configurer la fenêtre principale
                primaryStage.setTitle("iTunes RPC");
                primaryStage.getIcons().add(new Image(getClass().getResourceAsStream("/images/icon.png")));
                primaryStage.setScene(new Scene(root));
                primaryStage.setResizable(false);
                primaryStage.show();
                LOGGER.log(Level.INFO, "Fenêtre principale affichée");
                
                // Configurer l'action de fermeture
                primaryStage.setOnCloseRequest(event -> {
                    LOGGER.log(Level.INFO, "Fermeture de l'application");
                    
                    // Si le mode silencieux est activé, minimiser au lieu de fermer
                    if (silentModeManager.isSilentMode()) {
                        LOGGER.log(Level.INFO, "Mode silencieux activé, l'application continue en arrière-plan");
                        event.consume(); // Empêcher la fermeture
                        primaryStage.hide(); // Cacher la fenêtre
                    } else {
                        // Sinon, arrêter complètement l'application
                        if (trackMonitor != null) {
                            trackMonitor.stop();
                            LOGGER.log(Level.INFO, "Moniteur de piste iTunes arrêté");
                        }
                        Platform.exit();
                    }
                });
            }
            
        } catch (IOException e) {
            LOGGER.log(Level.SEVERE, "Erreur critique lors du démarrage de l'application: " + e.getMessage(), e);
            e.printStackTrace();
        }
    }
    
    @Override
    public void stop() {
        LOGGER.log(Level.INFO, "Arrêt de l'application");
        if (trackMonitor != null) {
            try {
                trackMonitor.stop();
                LOGGER.log(Level.INFO, "Moniteur de piste iTunes arrêté avec succès");
            } catch (Exception e) {
                LOGGER.log(Level.WARNING, "Erreur lors de l'arrêt du moniteur de piste iTunes: " + e.getMessage(), e);
            }
        }
    }

    /**
     * Ajoute des méthodes pour contrôler le mode silencieux et le démarrage automatique
     */
    public SilentModeManager getSilentModeManager() {
        return silentModeManager;
    }
}