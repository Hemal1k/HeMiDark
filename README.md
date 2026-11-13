# HeMi Dark

A dark, minimal skin in the style of **Windows 11** for **VLC 3.0.x** – made for watching video. 9 accent colours, sharp at
every screen scaling from 100 to 200 %.

**[Download](https://github.com/Hemal1k/HeMiDark/releases/latest)** · [Project page](https://hemal1k.github.io/hemidark.html) · [Report a problem](https://github.com/Hemal1k/HeMiDark/issues)

> Not signed yet – Windows SmartScreen may warn the first time (More info → Run anyway).

## Download

| File | For |
|---|---|
| `HeMiDark-Setup.exe` | The installer – for the current user (no administrator). Pick the colour and the scaling of your screen (the current one is preselected) and it sets the skin in VLC. Run it again for another colour; uninstalling puts VLC back to its usual look. |
| `HeMiDark-Skins.zip` | Just the skins (`.vlt`) by colour, to set in VLC yourself – see `README.txt` inside. |

Needs VLC 3.0.x for Windows (tested with 3.0.24).

By hand: VLC → Tools → Preferences → Interface → *Use custom skin* → choose the `.vlt` → Save → restart VLC.
Back: menu (the button at the top left) → Tools → Preferences → *Use native style*.

## What is inside

- **Main window**: title bar with the VLC menu, the file name, always on top, minimize / maximize / close; “Open file” and drag
  and drop when nothing plays; seek bar, times, volume, stop / previous / play-pause / next / repeat, audio track, subtitles,
  video settings, playlist, equalizer, full screen.
- **Playlist**, **equalizer** (on / off, preamp, 10 bands) and the **full-screen controller** in the same style.
- **9 colours** (blue, teal, green, yellow, orange, red, pink, purple, graphite) × **6 scalings** (100, 125, 140, 150, 175,
  200 %) = 54 skins. The skins of VLC do not scale with Windows, so every scaling is its own skin; 100 % fits Full HD.

Subtitles: VLC 3 has no menu of its own for them in skins; the subtitles button opens the main menu (Subtitle → Sub Track),
the key V cycles them, B the audio tracks.

## Building

The skins are not drawn by hand: a small .NET 10 program draws every picture (GDI+, icons from Segoe Fluent Icons, text in
Selawik), writes `theme.xml` and packs one `.vlt` per colour and scaling. Needs the [.NET 10 SDK](https://dotnet.microsoft.com)
on Windows 11 (or the font [Segoe Fluent Icons](https://aka.ms/SegoeFluentIcons)).

```
dotnet run --project src -c Release
```

writes `build\skins\<colour>\HeMi-Dark-<colour>-<scaling>.vlt`. Switches: `--colors blue,green`, `--scales 100,150`,
`--accent #4CC2FF` or `--accent system` (the accent colour of Windows, into `build\skins\custom`).

`release.ps1` builds the downloads into `build\dist` (needs [Inno Setup 6](https://jrsoftware.org/isinfo.php)). A release
published on GitHub runs the same script in `.github\workflows\release.yml` and gets the files attached.

To try a skin without touching your VLC settings: set `APPDATA` to an empty folder and start
`vlc.exe --intf=skins --skins2-last=<file.vlt> --no-one-instance --file-logging --logfile=<log>` – problems are in the log
as “skins2 warning” / “skins2 error”.

## Project

| File | Contents |
|---|---|
| `src\Program.cs` | Switches, the colours, the loop over colours and scalings. |
| `src\SkinBuild.cs` | One skin: the palette, the pictures of the controls, the four windows, the picture atlas, `theme.xml`, packing. |
| `src\Gfx.cs` | Drawing helpers: rounded shapes, window frame pieces, icons centred by their real pixels, text. |
| `src\Assets` | Selawik (SIL Open Font License, copied into every skin) and `README.txt` for the downloads. |
| `installer` | `HeMiDark.iss` (Inno Setup) and `vlc-skin.ps1`, which sets or resets the skin in `%APPDATA%\vlc\vlcrc`. |

## License

[MIT](LICENSE). The font Selawik is under the [SIL Open Font License](src/Assets/Fonts/Selawik-LICENSE.txt).
