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
- Pendant le déplacement, le widget **accroche aux quatre bords** à environ 8 px (adaptés à la mise à l’échelle Windows). Éloigner la souris du bord le détache, même en déplaçant lentement la souris. Le point de prise initial reste la référence pendant tout le déplacement. Le widget reste dans la zone utilisable du moniteur sous le pointeur, au-dessus d’une barre des tâches en bas ou à côté d’une barre latérale. Déplacer le pointeur sur un autre écran pour y faire passer le widget. **Général → Déplacement → Accrocher aux bords de l’écran**, activé par défaut, permet de désactiver ce comportement. La position est mémorisée à la fin du déplacement.
- Le **bandeau supérieur** (titre, carte réseau, boutons, échelle et durée) apparaît uniquement lorsque la souris survole le widget. Hors survol, le graphique récupère cet espace ; les débits restent visibles en bas.
- **Roue dentée**, **double-clic** ou **clic droit → Paramètres** : configurer le widget.
- **−** ou **Échap** : masquer le widget dans la zone de notification près de l’horloge. Son icône peut se trouver dans le menu des icônes masquées de Windows.
- **Double-clic sur l’icône** : afficher le widget.
- **×** ou **Quitter** : arrêter l’application.
- **Clic droit** : activer le premier plan, remettre les compteurs à zéro ou exporter le graphique en CSV.
- Survoler le widget : consulter les volumes reçus/envoyés depuis le lancement ou la dernière remise à zéro.

La position, les dimensions et les paramètres sont mémorisés dans `%LOCALAPPDATA%\MiniDebit\settings.json`. Fermer l’application puis supprimer ce fichier pour retrouver les valeurs par défaut.

## Réglages

![Paramètres](docs/settings.png)

L’onglet **Général** regroupe les paramètres de mesure et du widget. L’onglet **Graphique** permet de personnaliser les couleurs de réception, d’envoi, du fond et de la grille, l’épaisseur des courbes (0,5 à 6 px), leur style (continu, pointillé ou tirets), la présence et le style de la grille, ainsi que le remplissage et son opacité (0 à 100 %). « Auto (thème) » fait suivre au fond et à la grille le thème clair ou sombre.

![Apparence du graphique](docs/settings-graph.png)

**Graphique → Marge du graphique (px)** règle l’espace autour du graphique, de **0 à 20 px**, avec **3 px par défaut** pour gagner de la surface d’affichage. Le réglage s’applique aux quatre côtés, avec ou sans survol, et l’aperçu réagit immédiatement. À zéro, le cadre reste visible s’il est activé ; dans un petit widget, les marges verticales sont limitées si nécessaire pour conserver un graphique lisible. La valeur est mémorisée avec les autres paramètres et suit la mise à l’échelle Windows.

Le graphique affiche une ligne horizontale en tirets au niveau du **maximum observé sur la période visible**, avec la valeur juste au-dessus. La flèche et la couleur indiquent si ce pic vient de la réception (↓) ou de l’envoi (↑) ; les deux débits sont comparés, sans les additionner. Le maximum baisse lorsque les anciens pics sortent de la période. Il utilise les mêmes unités que le widget et reste visible hors survol. **Graphique → Maximum de la période** permet de masquer ou d’afficher ce repère. Si le maximum dépasse une échelle fixe, la ligne reste au plafond et la valeur exacte porte la mention « hors échelle ».

L’aperçu réagit aux réglages avant leur enregistrement et utilise des données d’exemple. **Enregistrer** applique et mémorise les changements ; **Annuler** laisse l’apparence du widget intacte. **Rétablir l’apparence par défaut** ne remet à zéro que les réglages du graphique, après enregistrement.

L’onglet **Débits** configure le bandeau inférieur : couleur du fond, police (famille, taille et style) et couleur du texte des valeurs et des libellés Réception / Envoi. **Fond graphique**, activé par défaut, reprend automatiquement la couleur du graphique, y compris ses changements ultérieurs. **Auto (contraste)** adapte la couleur du texte au fond ; une couleur personnalisée peut aussi être choisie. Les flèches gardent les couleurs des courbes. L’aperçu permet de contrôler le rendu avant d’enregistrer. Une police absente sur un autre ordinateur est remplacée par Segoe UI.

![Apparence du bandeau des débits](docs/settings-rates.png)

Dans **Débits → Bandeau compact**, cocher **Réduire les marges verticales** pour resserrer le bandeau, en conservant les valeurs et les libellés. Sa hauteur s’adapte à la police choisie ; l’espace libéré revient au graphique. L’aperçu reflète cette hauteur et l’option est mémorisée après enregistrement. Le mode standard reste sélectionné par défaut.

| Réglage | Valeurs |
|---|---|
| Carte réseau | Automatique, carte précise ou cumul des cartes actives |
| Actualisation | 250 à 5 000 ms ; 1 000 ms par défaut |
| Période affichée | 0,25 à 60 minutes (15 secondes à 1 heure) ; 5 minutes par défaut |
| Unités | Octets/s (Ko/s, Mo/s) ou bits/s (Mbit/s, Gbit/s) |
| Apparence | Sombre ou claire |
| Opacité | 35 à 100 % |
| Échelle verticale | Automatique, ou plafond fixe en Mbit/s |
| Premier plan | Activé ou désactivé |
| Au lancement | Widget visible ou uniquement icône près de l’horloge |

Le mode automatique favorise une carte Ethernet/Wi-Fi active avec une passerelle. Pour choisir exactement le lien à surveiller, sélectionner sa carte explicitement. Le cumul peut compter deux fois un trafic traversant une carte VPN et sa carte physique ; il faut donc privilégier une seule carte pour cette utilisation. Une sélection explicite indisponible affiche zéro jusqu’à sa reconnexion ; elle ne bascule pas sur une autre carte.

Les unités sont décimales : **1 Ko = 1 000 octets ; 1 Mo/s = 8 Mbit/s**. Un plafond fixe peut couper visuellement les pics du graphique ; les valeurs numériques continuent d’afficher le débit mesuré.

MiniDébit observe le trafic total de la carte, y compris les échanges locaux. Il ne mesure pas la vitesse maximale de la connexion Internet et ne sépare pas le trafic par application. Il ne lance aucun téléchargement de test. Le changement de carte remet les volumes de session et le graphique à zéro.

La **période affichée** règle la vitesse du défilement, indépendamment de l’**actualisation des mesures**. Avec 5 minutes, le graphique défile cinq fois plus lentement qu’avec une minute, tout en continuant de mesurer et d’afficher les débits toutes les 1 000 ms par défaut. Le réglage se trouve dans **Paramètres → Période affichée (min)**. Une période plus longue se remplit progressivement avec les nouvelles mesures ; le programme ne peut pas reconstituer des données déjà supprimées. Lors de la mise à jour, l’ancienne valeur de 60 secondes devient 5 minutes ; une autre période personnalisée et le délai de mesure sont conservés.

Les points du graphique restent en mémoire et leur nombre est borné. Le CSV contient uniquement la période encore affichée, avec horodatage et débits en octets/s. Il ne contient pas le contenu des paquets.

## Cadre de la fenêtre

L’onglet **Cadre** permet d’afficher ou de masquer la bordure autour de toute la fenêtre, de choisir sa couleur, son épaisseur (**0,5 à 8 px**) et son style (**continu, pointillé ou tirets**). Par défaut, le cadre est continu, épais de 1 px, avec une couleur qui suit le thème clair ou sombre. L’épaisseur suit la mise à l’échelle Windows et réserve sa place autour du graphique et du bandeau des débits. Le cadre reste visible hors survol.

![Paramètres du cadre](docs/settings-frame.png)

L’aperçu réagit aux réglages ; **Enregistrer** applique et mémorise le cadre, **Annuler** conserve le cadre actuel. **Rétablir le cadre par défaut** ne change que ces paramètres, après enregistrement.

## Historique SQLite

L’onglet **Historique** permet d’activer l’enregistrement de **chaque mesure**, au délai d’actualisation configuré, et de choisir une conservation de **1 à 90 jours**, avec **7 jours par défaut**. L’enregistrement est activé par défaut. Il continue lorsque le widget est masqué.

![Paramètres de l’historique](docs/settings-history.png)

La base est `%LOCALAPPDATA%\MiniDebit\traffic.sqlite`. La table `measurements` conserve l’horodatage UTC en millisecondes, l’identifiant de session, l’identifiant et le nom de la carte, le délai configuré, la durée réellement mesurée, les octets reçus/envoyés et les débits en octets par seconde. Les volumes proviennent des compteurs de la carte ; aucun contenu de paquet n’est enregistré. Les anciennes données du graphique ne sont pas reconstituées.

Le minuteur ajoute uniquement la mesure à une file bornée de 256 entrées. Un thread dédié écrit des transactions regroupées toutes les 30 secondes environ, ou à 128 mesures ; son cache SQLite est limité à 256 Kio. L’historique complet n’est jamais chargé en mémoire. MiniDébit utilise la [bibliothèque SQLite fournie par Windows](https://learn.microsoft.com/en-us/windows/apps/develop/data-access/sqlite-data-access), sans bibliothèque supplémentaire à installer ou distribuer. Le coût CPU et mémoire reste faible, mais ne peut pas être nul.

La fermeture normale écrit le dernier lot, avec une attente maximale de deux secondes. Un arrêt brutal peut perdre le dernier lot encore en mémoire, soit environ 30 secondes. Une erreur de stockage ou une file saturée laisse la surveillance fonctionner ; l’état et le nombre de mesures perdues sont consultables dans l’onglet Historique. Une erreur de stockage nécessite de corriger sa cause puis de relancer MiniDébit.

La purge des anciennes mesures intervient lors d’une écriture, chaque jour et après un changement de durée. L’espace libéré est réutilisé par SQLite ; le fichier ne rétrécit pas automatiquement. À 1 000 ms, une journée représente environ 86 400 lignes ; à 250 ms, environ 345 600. Désactiver l’historique arrête les nouvelles mesures sans supprimer la base existante. Pour sauvegarder la base, quitter MiniDébit puis copier `traffic.sqlite`.

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

Les tests vérifient les débits, les compteurs remis à zéro, les connexions/déconnexions, le cumul par carte, les unités, les limites et la persistance des paramètres, ainsi qu’une lecture réelle des interfaces Windows. Ils vérifient aussi une vraie base SQLite : intégrité, volumes, texte Unicode, annulation d’un lot invalide, purge, écriture à la fermeture et erreur de stockage.

Pour obtenir une capture et un rapport de fonctionnement sans changer les paramètres utilisateur :

```powershell
New-Item -ItemType Directory -Path artifacts -Force | Out-Null
.\dist\MiniDebit\MiniDebit.exe --capture "$PWD\artifacts\widget.png" --smoke-report "$PWD\artifacts\smoke.json"
```

Le diagnostic ferme l’application après 32 secondes. Il mesure la mémoire et le temps CPU pendant environ 30 secondes après une phase de démarrage de 2 secondes. Le pourcentage CPU est exprimé par rapport à **un cœur**, pas à l’ensemble du processeur. Ces mesures varient selon l’ordinateur, le DPI, l’intervalle, la taille du graphique et le trafic.

Une capture de la fenêtre des paramètres peut être produite avec `--capture-settings chemin.png`.
L’onglet Graphique peut être capturé avec `--capture-graph-settings chemin.png`.
L’onglet Débits peut être capturé avec `--capture-rate-settings chemin.png`.
L’onglet Historique peut être capturé avec `--capture-history-settings chemin.png`.
L’onglet Cadre peut être capturé avec `--capture-frame-settings chemin.png`.
Ajouter `--diagnostic-settings chemin.json` à une capture ou un diagnostic pour vérifier des réglages personnalisés dans un fichier distinct, sans écrire les paramètres utilisateur.
Les diagnostics désactivent l’historique par défaut ; ajouter `--history-db chemin.sqlite` pour tester les écritures dans une base distincte des données utilisateur. `--long-benchmark` prolonge le diagnostic à 72 secondes, dont environ 70 secondes mesurées après démarrage.
Ajouter `--compact` à un diagnostic pour capturer le widget ou l’onglet Débits en mode compact sans modifier les paramètres utilisateur.
Ajouter `--capture-hover chemin.png` au diagnostic pour capturer aussi le bandeau affiché au survol, sur le même relevé de trafic.

Les compteurs sont lus avec l’API Windows exposée par [.NET NetworkInterface.GetIPStatistics](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkinterface.getipstatistics). Le temps entre relevés est mesuré par une horloge monotone ; l’ajout, le retrait ou la remise à zéro des compteurs d’une carte ne produit pas de pic artificiel.
