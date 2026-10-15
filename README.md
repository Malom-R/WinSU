# WinSU

**WinSU** is a game launcher for Windows, designed to be used with a controller from the couch or on a handheld: organize your games by console and launch them with a single press.

> Project in development (version 1.7.0). Inspired by the Android launcher **iiSU**: WinSU is not affiliated with it.
> Please support their project — it's amazing.

## What you can do with it

- **Organize your games by console** (this launcher is not an emulation frontend — it does not ship any emulators natively): a catalog of 25 consoles (Nintendo, Sega, PlayStation, Xbox, and "Windows" for any program).
- **Choose your interface**: three styles (Standard, Switch, Wii U), switched on the fly; light and dark themes with an editor to create your own; several fonts; animated background.
- **Dress up your games**: box art, banner and logo picked from disk, or fetched from **SteamGridDB** with your own API key.

![description](https://github.com/Malom-R/WinSU/blob/main/Template/1.png)

![description](https://github.com/Malom-R/WinSU/blob/main/Template/2.png)

![description](https://github.com/Malom-R/WinSU/blob/main/Template/3.png)

## Build and run

Requirements: Windows 10 or 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build WinSU.slnx -c Release
WinSU.App\bin\Release\net10.0-windows\WinSU.exe
```

Data (settings, library, playtime, images) is stored in `%LOCALAPPDATA%\WinSU`. Alternatives: `--data-dir <folder>` for a different folder, or a `portable.txt` file next to the executable to keep everything in `data\` alongside it.

The code is split across three projects: `WinSU.Core` (models and contracts, no dependencies), `WinSU.Infrastructure` (files, SQLite database, controller, audio, Windows, SteamGridDB) and `WinSU.App` (the Avalonia application).

**English and French supported.**

## Sources and credits

**Console icons**: [RetroArch assets](https://github.com/libretro/retroarch-assets) (libretro), "Automatic" theme by [baxysquare](https://github.com/baxysquare/baxy-retroarch-themes) and the "Monochrome" theme, licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Details: `WinSU.App/Assets/Icons/CREDITS.txt`.

**Interface sounds**: "[Interface Sounds](https://kenney.nl/assets/interface-sounds)" by Kenney, licensed under [CC0](https://creativecommons.org/publicdomain/zero/1.0/). Details: `WinSU.App/Assets/Sounds/CREDITS.txt`.

**Fonts**, all licensed under the [SIL Open Font License 1.1](https://openfontlicense.org) (an `OFL.txt` is included in each folder under `WinSU.App/Fonts`):
- DM Sans (Colophon Foundry), the default font;
- Inter (Rasmus Andersson);
- Atkinson Hyperlegible Next (Applied Design Works, Letters from Sweden);
- Lexend (Bonnie Shaver-Troup, Thomas Jockin);
- Exo 2 (Natanael Gama);
- Chakra Petch (Cadson Demak);
- Oxanium (Severin Meyer);
- WinSU Glyphs: button glyphs drawn for WinSU, with letters and digits from DM Sans.

**Libraries**:
- [Avalonia](https://avaloniaui.net) (MIT), with SkiaSharp (MIT);
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MIT);
- Microsoft.Extensions.Hosting and Microsoft.Data.Sqlite (MIT), SQLite (public domain);
- [Serilog](https://serilog.net) (Apache 2.0);
- [NAudio](https://github.com/naudio/NAudio) (MIT) and [NVorbis](https://github.com/NVorbis/NVorbis) (MIT);
- [SDL3](https://www.libsdl.org) (zlib), via [ppy.SDL3-CS](https://github.com/ppy/SDL3-CS) (MIT).

**Online images**: [SteamGridDB](https://www.steamgriddb.com), using each user's own API key; images belong to their respective authors.

**Inspiration**: the Android launcher iiSU. WinSU's catalog, texts, tiles, frames, palette, logo and icon were designed for WinSU.

Console names and shapes are trademarks of their respective manufacturers (Nintendo, Sega, Sony, Microsoft); Steam is a trademark of Valve. WinSU is not affiliated with any of them.

## License

WinSU's code is distributed under the **GNU GPL v3** license (see [LICENSE](LICENSE)). Icons, sounds and fonts retain their own licenses, cited above.
