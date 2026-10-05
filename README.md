# MiniDébit

Petit widget Windows de surveillance du trafic réseau, inspiré de la capture fournie : graphique en temps réel, réception et envoi affichés séparément, fenêtre déplaçable et redimensionnable.

![Widget en fonctionnement](docs/widget.png)

Au survol :

![Widget survolé](docs/widget-hover.png)

## Lancer

Double-cliquer sur **Lancer.cmd**, ou sur **dist\MiniDebit\MiniDebit.exe**. Garder les fichiers du dossier `dist\MiniDebit` ensemble.

L’application utilise **C# / WinForms et .NET 10 Desktop**. Ce runtime est déjà installé sur l’ordinateur utilisé pour la compilation. Sur un autre PC Windows 10/11, installer le [runtime .NET 10 Desktop](https://dotnet.microsoft.com/fr-fr/download/dotnet/10.0) correspondant à l’architecture. Le SDK est nécessaire uniquement pour compiler. Aucun droit administrateur n’est demandé par MiniDébit.

La distribution inclut le programme, ses fichiers .NET et ce guide ; elle n’embarque pas le runtime afin de garder le téléchargement et le dossier très petits.

## Utilisation

- **Glisser la fenêtre** pour la déplacer ; tirer son coin inférieur droit pour la redimensionner.
- Le **bandeau supérieur** (titre, carte réseau, boutons, échelle et durée) apparaît uniquement lorsque la souris survole le widget. Hors survol, le graphique récupère 52 pixels de hauteur ; les débits restent visibles en bas.
- **Roue dentée**, **double-clic** ou **clic droit → Paramètres** : configurer le widget.
- **−** ou **Échap** : masquer le widget dans la zone de notification près de l’horloge. Son icône peut se trouver dans le menu des icônes masquées de Windows.
- **Double-clic sur l’icône** : afficher le widget.
- **×** ou **Quitter** : arrêter l’application.
- **Clic droit** : activer le premier plan, remettre les compteurs à zéro ou exporter le graphique en CSV.
- Survoler le widget : consulter les volumes reçus/envoyés depuis le lancement ou la dernière remise à zéro.

La position, les dimensions et les paramètres sont mémorisés dans `%LOCALAPPDATA%\MiniDebit\settings.json`. Fermer l’application puis supprimer ce fichier pour retrouver les valeurs par défaut.

## Réglages

![Paramètres](docs/settings.png)

| Réglage | Valeurs |
|---|---|
| Carte réseau | Automatique, carte précise ou cumul des cartes actives |
| Actualisation | 250 à 5 000 ms ; 1 000 ms par défaut |
| Historique | 15 à 600 secondes ; 60 secondes par défaut |
| Unités | Octets/s (Ko/s, Mo/s) ou bits/s (Mbit/s, Gbit/s) |
| Apparence | Sombre ou claire |
| Opacité | 35 à 100 % |
| Échelle verticale | Automatique, ou plafond fixe en Mbit/s |
| Premier plan | Activé ou désactivé |
| Au lancement | Widget visible ou uniquement icône près de l’horloge |

Le mode automatique favorise une carte Ethernet/Wi-Fi active avec une passerelle. Pour choisir exactement le lien à surveiller, sélectionner sa carte explicitement. Le cumul peut compter deux fois un trafic traversant une carte VPN et sa carte physique ; il faut donc privilégier une seule carte pour cette utilisation. Une sélection explicite indisponible affiche zéro jusqu’à sa reconnexion ; elle ne bascule pas sur une autre carte.

Les unités sont décimales : **1 Ko = 1 000 octets ; 1 Mo/s = 8 Mbit/s**. Un plafond fixe peut couper visuellement les pics du graphique ; les valeurs numériques continuent d’afficher le débit mesuré.

MiniDébit observe le trafic total de la carte, y compris les échanges locaux. Il ne mesure pas la vitesse maximale de la connexion Internet et ne sépare pas le trafic par application. Il ne lance aucun téléchargement de test. Le changement de carte remet les volumes de session et le graphique à zéro.

L’historique reste en mémoire et sa taille est bornée. Le CSV contient uniquement la période encore affichée, avec horodatage et débits en octets/s. Il ne contient pas le contenu des paquets.

Pour lancer MiniDébit à l’ouverture de session Windows, placer un raccourci vers l’exécutable dans le dossier ouvert par **Win+R → shell:startup**. Choisir « Dans la zone de notification » dans les paramètres pour un lancement discret. Aucune entrée de démarrage n’est ajoutée automatiquement.

## Compiler et vérifier

Depuis PowerShell, dans ce dossier :

```powershell
.\build.ps1 -Test
```

Si la politique PowerShell bloque les scripts, exécuter uniquement ce script avec :

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

La compilation utilise le SDK 10.0.401 (ou un correctif compatible) et les composants locaux. Aucune bibliothèque NuGet externe n’est utilisée. `MiniDebit.slnx` contient le programme et les tests. `build.ps1` conserve son état de compilation dans `artifacts\build-home` et publie dans `dist\MiniDebit`.

Les tests vérifient les débits, les compteurs remis à zéro, les connexions/déconnexions, le cumul par carte, les unités, les limites et la persistance des paramètres, ainsi qu’une lecture réelle des interfaces Windows.

Pour obtenir une capture et un rapport de fonctionnement sans changer les paramètres utilisateur :

```powershell
New-Item -ItemType Directory -Path artifacts -Force | Out-Null
.\dist\MiniDebit\MiniDebit.exe --capture "$PWD\artifacts\widget.png" --smoke-report "$PWD\artifacts\smoke.json"
```

Le diagnostic ferme l’application après 32 secondes. Il mesure la mémoire et le temps CPU pendant environ 30 secondes après une phase de démarrage de 2 secondes. Le pourcentage CPU est exprimé par rapport à **un cœur**, pas à l’ensemble du processeur. Ces mesures varient selon l’ordinateur, le DPI, l’intervalle, la taille du graphique et le trafic.

Une capture de la fenêtre des paramètres peut être produite avec `--capture-settings chemin.png`.
Ajouter `--capture-hover chemin.png` au diagnostic pour capturer aussi le bandeau affiché au survol, sur le même relevé de trafic.

Les compteurs sont lus avec l’API Windows exposée par [.NET NetworkInterface.GetIPStatistics](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkinterface.getipstatistics). Le temps entre relevés est mesuré par une horloge monotone ; l’ajout, le retrait ou la remise à zéro des compteurs d’une carte ne produit pas de pic artificiel.
