# ITunesRPC

## Description
ITunesRPC est une application Java qui affiche en temps réel les informations des morceaux écoutés sur iTunes ou Apple Music sur PC. L'application montre le titre de la chanson, sa position dans l'album/playlist, l'artiste, le nom de l'album, et la pochette d'album.

## Fonctionnalités
- Détection automatique d'iTunes et Apple Music
- Affichage en temps réel des informations de la piste en cours de lecture
- Affichage de la pochette d'album
- Mise à jour automatique de l'application
- Interface utilisateur moderne et intuitive

## Prérequis
- Java 11 ou supérieur
- iTunes ou Apple Music pour Windows

## Installation
1. Téléchargez la dernière version depuis la [page des releases](https://github.com/darkiiuseai/ITunesRPC/releases)
2. Exécutez le fichier JAR téléchargé avec la commande : `java -jar ITunesRPC-1.0.0.jar`

## Compilation depuis les sources
```bash
# Cloner le dépôt
git clone https://github.com/darkiiuseai/ITunesRPC.git
cd ITunesRPC

# Compiler avec Maven
mvn clean package

# Exécuter l'application
java -jar target/ITunesRPC-1.0-SNAPSHOT.jar
```

## Utilisation
1. Lancez l'application ITunesRPC
2. Ouvrez iTunes ou Apple Music et commencez à lire de la musique
3. L'application détectera automatiquement la piste en cours de lecture et affichera ses informations

## Captures d'écran
![Capture d'écran de l'application](docs/screenshot.png)

## Contribution
Les contributions sont les bienvenues ! N'hésitez pas à ouvrir une issue ou à soumettre une pull request.

## Licence
Ce projet est sous licence MIT. Voir le fichier [LICENSE](LICENSE) pour plus de détails.

## Auteur
- [darkiiuseai](https://github.com/darkiiuseai)