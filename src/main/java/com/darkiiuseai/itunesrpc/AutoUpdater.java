package com.darkiiuseai.itunesrpc;

import javafx.application.Platform;
import javafx.scene.control.Alert;
import javafx.scene.control.ButtonType;
import org.apache.http.HttpEntity;
import org.apache.http.client.methods.CloseableHttpResponse;
import org.apache.http.client.methods.HttpGet;
import org.apache.http.impl.client.CloseableHttpClient;
import org.apache.http.impl.client.HttpClients;
import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStreamReader;
import java.net.URI;
import java.nio.channels.Channels;
import java.nio.channels.ReadableByteChannel;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Optional;

/**
 * Classe responsable de la vérification et de l'installation des mises à jour
 */
public class AutoUpdater {
    
    private final String owner;
    private final String repo;
    private final String currentVersion;
    private static final String API_URL = "https://api.github.com/repos/%s/%s/releases/latest";
    
    /**
     * Constructeur pour l'AutoUpdater
     * 
     * @param owner Le propriétaire du dépôt GitHub
     * @param repo Le nom du dépôt GitHub
     */
    public AutoUpdater(String owner, String repo) {
        this.owner = owner;
        this.repo = repo;
        this.currentVersion = "1.0.0"; // Version actuelle de l'application
    }
    
    /**
     * Vérifie si une mise à jour est disponible
     */
    public void checkForUpdates() {
        new Thread(() -> {
            try {
                String apiUrl = String.format(API_URL, owner, repo);
                CloseableHttpClient httpClient = HttpClients.createDefault();
                HttpGet request = new HttpGet(apiUrl);
                request.addHeader("Accept", "application/vnd.github.v3+json");
                
                try (CloseableHttpResponse response = httpClient.execute(request)) {
                    HttpEntity entity = response.getEntity();
                    if (entity != null) {
                        try (BufferedReader reader = new BufferedReader(
                                new InputStreamReader(entity.getContent()))) {
                            StringBuilder result = new StringBuilder();
                            String line;
                            while ((line = reader.readLine()) != null) {
                                result.append(line);
                            }
                            
                            JSONObject jsonResponse = new JSONObject(result.toString());
                            String latestVersion = jsonResponse.getString("tag_name").replace("v", "");
                            
                            if (isNewerVersion(latestVersion)) {
                                String downloadUrl = null;
                                JSONArray assets = jsonResponse.getJSONArray("assets");
                                for (int i = 0; i < assets.length(); i++) {
                                    JSONObject asset = assets.getJSONObject(i);
                                    if (asset.getString("name").endsWith(".jar")) {
                                        downloadUrl = asset.getString("browser_download_url");
                                        break;
                                    }
                                }
                                
                                if (downloadUrl != null) {
                                    final String finalDownloadUrl = downloadUrl;
                                    Platform.runLater(() -> promptUpdate(latestVersion, finalDownloadUrl));
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                e.printStackTrace();
                // Échec silencieux - ne pas interrompre l'expérience utilisateur
            }
        }).start();
    }
    
    /**
     * Vérifie si la version distante est plus récente que la version actuelle
     * 
     * @param remoteVersion La version distante à comparer
     * @return true si la version distante est plus récente
     */
    private boolean isNewerVersion(String remoteVersion) {
        String[] currentParts = currentVersion.split("\\.");
        String[] remoteParts = remoteVersion.split("\\.");
        
        int length = Math.max(currentParts.length, remoteParts.length);
        for (int i = 0; i < length; i++) {
            int currentPart = i < currentParts.length ? Integer.parseInt(currentParts[i]) : 0;
            int remotePart = i < remoteParts.length ? Integer.parseInt(remoteParts[i]) : 0;
            
            if (remotePart > currentPart) {
                return true;
            } else if (remotePart < currentPart) {
                return false;
            }
        }
        
        return false; // Les versions sont identiques
    }
    
    /**
     * Affiche une boîte de dialogue pour proposer la mise à jour
     * 
     * @param newVersion La nouvelle version disponible
     * @param downloadUrl L'URL de téléchargement de la nouvelle version
     */
    private void promptUpdate(String newVersion, String downloadUrl) {
        Alert alert = new Alert(Alert.AlertType.CONFIRMATION);
        alert.setTitle("Mise à jour disponible");
        alert.setHeaderText("Une nouvelle version est disponible");
        alert.setContentText("La version " + newVersion + " est disponible. Voulez-vous la télécharger et l'installer maintenant ?");
        
        Optional<ButtonType> result = alert.showAndWait();
        if (result.isPresent() && result.get() == ButtonType.OK) {
            downloadAndInstallUpdate(downloadUrl);
        }
    }
    
    /**
     * Télécharge et installe la mise à jour
     * 
     * @param downloadUrl L'URL de téléchargement de la nouvelle version
     */
    private void downloadAndInstallUpdate(String downloadUrl) {
        try {
            // Créer un répertoire temporaire pour la mise à jour
            Path updateDir = Files.createTempDirectory("itunesrpc_update");
            File updateFile = updateDir.resolve("ITunesRPC-update.jar").toFile();
            
            // Télécharger le fichier de mise à jour
            try (CloseableHttpClient httpClient = HttpClients.createDefault()) {
                HttpGet request = new HttpGet(new URI(downloadUrl));
                try (CloseableHttpResponse response = httpClient.execute(request)) {
                    HttpEntity entity = response.getEntity();
                    if (entity != null) {
                        try (ReadableByteChannel rbc = Channels.newChannel(entity.getContent());
                             FileOutputStream fos = new FileOutputStream(updateFile)) {
                            fos.getChannel().transferFrom(rbc, 0, Long.MAX_VALUE);
                        }
                    }
                }
            }
            
            // Obtenir le chemin de l'application actuelle
            String jarPath = AutoUpdater.class.getProtectionDomain()
                    .getCodeSource().getLocation().toURI().getPath();
            File currentJar = new File(jarPath);
            
            // Créer un script de mise à jour qui remplacera le fichier JAR actuel
            // et redémarrera l'application
            String updaterScript;
            if (System.getProperty("os.name").toLowerCase().contains("win")) {
                updaterScript = "@echo off\n" +
                        "timeout /t 2 /nobreak > nul\n" +
                        "copy /Y \"" + updateFile.getAbsolutePath() + "\" \"" + currentJar.getAbsolutePath() + "\"\n" +
                        "start javaw -jar \"" + currentJar.getAbsolutePath() + "\"\n" +
                        "del %0\n";
                File batchFile = updateDir.resolve("updater.bat").toFile();
                Files.write(batchFile.toPath(), updaterScript.getBytes());
                
                // Exécuter le script et quitter l'application
                new ProcessBuilder("cmd", "/c", batchFile.getAbsolutePath()).start();
                Platform.exit();
            } else {
                updaterScript = "#!/bin/bash\n" +
                        "sleep 2\n" +
                        "cp \"" + updateFile.getAbsolutePath() + "\" \"" + currentJar.getAbsolutePath() + "\"\n" +
                        "java -jar \"" + currentJar.getAbsolutePath() + "\" &\n" +
                        "rm $0\n";
                File shellScript = updateDir.resolve("updater.sh").toFile();
                Files.write(shellScript.toPath(), updaterScript.getBytes());
                shellScript.setExecutable(true);
                
                // Exécuter le script et quitter l'application
                new ProcessBuilder("/bin/bash", shellScript.getAbsolutePath()).start();
                Platform.exit();
            }
            
        } catch (Exception e) {
            e.printStackTrace();
            Platform.runLater(() -> {
                Alert alert = new Alert(Alert.AlertType.ERROR);
                alert.setTitle("Erreur de mise à jour");
                alert.setHeaderText("Échec de la mise à jour");
                alert.setContentText("Une erreur s'est produite lors de la mise à jour : " + e.getMessage());
                alert.showAndWait();
            });
        }
    }
}