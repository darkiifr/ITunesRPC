# Instructions d'utilisation - iTunes RPC

## Modifications apportées

L'application a été améliorée pour mieux s'intégrer avec iTunes en utilisant l'API COM native de Windows. Voici les principales modifications :

1. **Intégration COM complète** : Implémentation des interfaces COM spécifiques à iTunes pour accéder directement aux fonctionnalités de l'API iTunes.

2. **Bibliothèques JNA améliorées** : Mise à jour des dépendances JNA vers la version 5.13.0 pour une meilleure intégration COM.

3. **Utilisation des méthodes réelles de l'API iTunes** : Remplacement des méthodes simulées par des appels réels à l'API COM d'iTunes pour obtenir les informations de piste, l'état du lecteur et les illustrations d'album.

4. **Gestion améliorée des erreurs** : Amélioration de la journalisation et de la gestion des erreurs pour faciliter le débogage.

5. **Libération correcte des ressources COM** : Implémentation d'une libération systématique des ressources COM pour éviter les fuites de mémoire.

6. **Exécution en mode administrateur** : Vérification et demande automatique des privilèges administrateur pour un accès optimal à l'API COM.

7. **Mode silencieux** : Possibilité d'exécuter l'application en arrière-plan, même après la fermeture de la fenêtre principale.

8. **Démarrage automatique** : Option pour lancer automatiquement l'application au démarrage de Windows en mode silencieux.

## Comment tester l'application

### Prérequis

- Windows 10 ou 11
- iTunes installé sur votre système
- Java 11 ou supérieur
- Maven (pour la compilation)

### Compilation

Pour compiler l'application, exécutez la commande suivante dans le répertoire du projet :

```bash
mvn clean package
```

Si Maven n'est pas disponible sur votre système, vous pouvez utiliser le wrapper Maven (si disponible) :

```bash
./mvnw clean package
```

Ou compiler manuellement avec javac en incluant toutes les dépendances nécessaires.

### Exécution

Après la compilation, vous pouvez exécuter l'application avec la commande :

```bash
java -jar target/ITunesRPC-1.0-SNAPSHOT.jar
```

### Test de l'intégration iTunes

1. Lancez iTunes et démarrez la lecture d'une piste
2. Lancez l'application iTunes RPC
3. L'application devrait détecter automatiquement iTunes et afficher les informations de la piste en cours de lecture
4. Changez de piste dans iTunes pour vérifier que l'application détecte correctement les changements

## Dépannage

### iTunes n'est pas détecté

- Vérifiez qu'iTunes est en cours d'exécution et qu'une piste est en lecture
- Assurez-vous que l'application est exécutée avec les mêmes privilèges qu'iTunes
- Consultez les journaux de l'application pour identifier les erreurs potentielles

### Erreurs COM

Si vous rencontrez des erreurs liées à l'intégration COM :

1. Vérifiez que vous utilisez une version compatible d'iTunes
2. Essayez de redémarrer iTunes et l'application
3. Vérifiez que les bibliothèques JNA sont correctement chargées

## Fonctionnalités avancées

### Mode administrateur

L'application nécessite des privilèges administrateur pour accéder correctement à l'API COM d'iTunes. Au démarrage, l'application vérifie automatiquement si elle dispose des privilèges nécessaires :

- Si l'application n'est pas exécutée en tant qu'administrateur, une boîte de dialogue vous proposera de la relancer avec les privilèges appropriés.
- En mode silencieux ou au démarrage, l'application demandera automatiquement l'élévation des privilèges sans intervention de l'utilisateur.

### Mode silencieux

Le mode silencieux permet à l'application de continuer à fonctionner en arrière-plan, même après la fermeture de la fenêtre principale :

- Activez cette option en cochant la case "Mode silencieux" dans l'interface principale.
- Lorsque le mode silencieux est activé, la fermeture de la fenêtre principale ne termine pas l'application, mais la minimise en arrière-plan.
- Pour quitter complètement l'application en mode silencieux, vous devez désactiver cette option avant de fermer la fenêtre.

### Démarrage automatique

L'application peut être configurée pour démarrer automatiquement avec Windows :

- Activez cette option en cochant la case "Lancer au démarrage" dans l'interface principale.
- L'application sera lancée automatiquement au démarrage de Windows en mode silencieux et avec les privilèges administrateur.

### Arguments de ligne de commande

L'application prend en charge les arguments de ligne de commande suivants :

- `-silent` : Démarre l'application en mode silencieux (sans interface graphique)
- `-startup` : Indique que l'application est lancée au démarrage de Windows

## Remarques importantes

- Cette implémentation nécessite Windows et iTunes installé sur le système
- L'application doit être exécutée avec des privilèges administrateur pour accéder correctement à l'API COM d'iTunes
- Si l'intégration COM échoue, l'application tentera de détecter iTunes via la fenêtre de l'application
- Le mode silencieux et le démarrage automatique nécessitent des privilèges administrateur pour fonctionner correctement