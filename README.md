# iTunes RPC

Une application WPF qui affiche les informations des musiques écoutées sur iTunes/Apple Music sur Discord. Cette application permet de partager votre activité d'écoute avec vos amis via Discord Rich Presence.

## Fonctionnalités

- Affichage des informations de la musique en cours de lecture (titre, artiste, album)
- Affichage de la position du morceau dans l'album ou la playlist
- Affichage de la pochette d'album
- Intégration avec Discord Rich Presence
- Mise à jour automatique via GitHub
- Démarrage automatique avec Windows
- Fonctionnement en arrière-plan lorsque l'application est fermée
- Reconnexion automatique à Discord en cas de perte de connexion
- Gestion optimisée des ressources et des erreurs
- Téléchargement optimisé des mises à jour

## Prérequis

- Windows 10 ou supérieur
- .NET 6.0 Runtime
- iTunes ou Apple Music pour Windows
- Discord (pour l'affichage Rich Presence)

## Installation

1. Téléchargez la dernière version depuis la [page des releases](https://github.com/darkiiuseai/ITunesRPC/releases)
2. Exécutez le fichier d'installation
3. L'application démarrera automatiquement après l'installation

## Utilisation

1. Lancez iTunes et commencez à écouter de la musique
2. L'application détectera automatiquement la musique en cours de lecture
3. Les informations seront affichées dans l'application et sur Discord
4. Vous pouvez minimiser l'application, elle continuera de fonctionner en arrière-plan

## Configuration

- **Démarrer avec Windows** : Active/désactive le démarrage automatique de l'application au démarrage de Windows
- **Minimiser dans la zone de notification** : Active/désactive la minimisation de l'application dans la zone de notification lors de la fermeture de la fenêtre

## Développement

### Prérequis pour le développement

- Visual Studio 2022
- .NET 6.0 SDK
- Docker (optionnel, pour le déploiement)

### Compilation

```bash
dotnet restore
dotnet build
```

### Exécution

```bash
dotnet run --project ItunesRPC/ItunesRPC.csproj
```

### Création d'une image Docker

```bash
docker build -t itunesrpc .
```

## Licence

Ce projet est sous licence MIT. Voir le fichier LICENSE pour plus de détails.

## Remerciements

- [DiscordRichPresence](https://github.com/Lachee/discord-rpc-csharp) - Bibliothèque C# pour Discord Rich Presence
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) - Bibliothèque pour l'icône de notification WPF
- [Octokit](https://github.com/octokit/octokit.net) - Bibliothèque .NET pour l'API GitHub