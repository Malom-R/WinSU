# WinSU

**WinSU** est un lanceur de jeux plein écran pour Windows, pensé pour être utilisé à la manette depuis un canapé : on y range
ses jeux par console, on les lance d'un appui, et WinSU revient au premier plan quand la partie se termine.

> Projet en développement (version 1.6.0). Inspiré du lanceur Android **iiSU** : WinSU n'en reprend aucun fichier et n'y est
> pas lié.

## Ce qu'on peut faire avec

- **Ranger ses jeux par console** : un catalogue de 25 consoles (Nintendo, Sega, PlayStation, Xbox, et « Windows » pour tout
  programme) ; seules les consoles ajoutées s'affichent, dans l'ordre choisi.
- **Ajouter des jeux** : un raccourci par jeu (`.lnk`, `.url` des jeux Steam, `.exe`, `.bat`, `.cmd`), choisis dans la boîte
  « Ouvrir » de Windows ; les fichiers restent là où ils sont.
- **Lancer et suivre un jeu** : WinSU suit tout l'arbre de processus du jeu (ou l'état de Steam pour un jeu Steam), compte le
  temps de jeu, se réduit ou se met en veille derrière le jeu, n'accepte aucune entrée pendant la partie, puis revient au
  premier plan.
- **Choisir son interface** : trois styles (Standard, Switch, Wii U), changés à chaud ; thèmes clair, sombre ou du système,
  et un éditeur pour créer ses propres thèmes ; plusieurs polices ; fond animé.
- **Habiller ses jeux** : jaquette, bannière et logo choisis sur le disque, ou cherchés sur **SteamGridDB** avec sa propre clé
  d'API ; sans image, WinSU dessine une tuile aux couleurs de la console.
- **Jouer à la manette ou au clavier** : manettes reconnues par SDL3 (glyphes Xbox, PlayStation ou Switch), touches
  reconfigurables, assistant au premier lancement ; la souris marche aussi.
- **Le reste** : sons d'interface, menu d'alimentation (veille, redémarrer, éteindre, quitter), français et anglais.

## Compiler et lancer

Prérequis : Windows 10 ou 11 et le [SDK .NET 10](https://dotnet.microsoft.com/download).

```
dotnet build WinSU.slnx -c Release
WinSU.App\bin\Release\net10.0-windows\WinSU.exe
```

Les données (réglages, bibliothèque, temps de jeu, images) sont dans `%LOCALAPPDATA%\WinSU`. Autres possibilités :
`--data-dir <dossier>` pour un autre dossier, ou un fichier `portable.txt` à côté de l'exe pour les garder dans `data\`, à côté
de lui.

Le code est rangé en trois projets : `WinSU.Core` (modèles et contrats, sans bibliothèque), `WinSU.Infrastructure` (fichiers,
base SQLite, manette, son, Windows, SteamGridDB) et `WinSU.App` (l'application Avalonia).

## Sources et crédits

**Icônes des consoles** : [assets de RetroArch](https://github.com/libretro/retroarch-assets) (libretro), thème « Automatic » de
[baxysquare](https://github.com/baxysquare/baxy-retroarch-themes) et thème « Monochrome », sous licence
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Détail : `WinSU.App/Assets/Icons/CREDITS.txt`.

**Sons d'interface** : « [Interface Sounds](https://kenney.nl/assets/interface-sounds) » de Kenney, sous licence
[CC0](https://creativecommons.org/publicdomain/zero/1.0/). Détail : `WinSU.App/Assets/Sounds/CREDITS.txt`.

**Polices**, toutes sous licence [SIL Open Font License 1.1](https://openfontlicense.org) (un `OFL.txt` dans chaque dossier de
`WinSU.App/Fonts`) :
- DM Sans (Colophon Foundry), la police par défaut ;
- Inter (Rasmus Andersson) ;
- Atkinson Hyperlegible Next (Applied Design Works, Letters from Sweden) ;
- Lexend (Bonnie Shaver-Troup, Thomas Jockin) ;
- Exo 2 (Natanael Gama) ;
- Chakra Petch (Cadson Demak) ;
- Oxanium (Severin Meyer) ;
- WinSU Glyphs : glyphes des boutons dessinés pour WinSU, lettres et chiffres de DM Sans.

**Bibliothèques** :
- [Avalonia](https://avaloniaui.net) (MIT), avec SkiaSharp (MIT) ;
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MIT) ;
- Microsoft.Extensions.Hosting et Microsoft.Data.Sqlite (MIT), SQLite (domaine public) ;
- [Serilog](https://serilog.net) (Apache 2.0) ;
- [NAudio](https://github.com/naudio/NAudio) (MIT) et [NVorbis](https://github.com/NVorbis/NVorbis) (MIT) ;
- [SDL3](https://www.libsdl.org) (zlib), par [ppy.SDL3-CS](https://github.com/ppy/SDL3-CS) (MIT).

**Images en ligne** : [SteamGridDB](https://www.steamgriddb.com), avec la clé d'API de chaque utilisateur ; les images
appartiennent à leurs auteurs.

**Inspiration** : le lanceur Android iiSU. Le catalogue, les textes, les tuiles, les cadres, la palette, le logo et l'icône de
WinSU sont dessinés pour WinSU.

Les noms et les formes des consoles sont des marques de leurs fabricants (Nintendo, Sega, Sony, Microsoft), Steam est une
marque de Valve : WinSU n'est lié à aucun d'eux.

## Licence

Le code de WinSU est distribué sous licence **GNU GPL v3** (voir [LICENSE](LICENSE)). Les icônes, les sons et les polices
gardent leur propre licence, citée plus haut.
