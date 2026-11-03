# AlleAusgaenge (all extracts)

Makes every extract on every map always available: chance set to 100 %, entry points merged, time window open. Trains are excluded.

## Requirements

- SPT 4.1.6 (server).

## Installation

1. Download `AlleAusgaenge-1.0.0.zip` from the [Releases](https://github.com/Kaanori/spt-alleausgaenge/releases) page.
2. Extract it **into your SPT game folder** (the folder that contains `EscapeFromTarkov.exe`).
   The ZIP already has the right layout; the mod ends up in `SPT_Runtime/user/mods/AlleAusgaenge/`.
   On Windows, make sure "Extract All" does not add an extra subfolder.
3. Restart the SPT server.

## Uninstall

Delete the mod's files from `SPT_Runtime/user/mods/AlleAusgaenge/` (settings: `BepInEx/config/` for client mods) and restart the SPT server.

## Settings: `ausgaenge.json`

```json
{
  "chance": true,
  "einstiegspunkte": true,
  "zeitfenster": true
}
```

Keys: `chance` (chance), `einstiegspunkte` (entry points), `zeitfenster` (time window).

Takes effect on the next server start. The file is not overwritten on install if one already exists in the mod folder.

## Building from source

Needs the .NET 10 SDK and an SPT 4.1.6 installation to build against.

```bash
./build.sh             # Linux: build only
./build.sh --install   # build and copy into the game folder
```
```powershell
.\build.ps1            # Windows
.\build.ps1 -Install
```

The game folder is set in `Directory.Build.props` (Windows default `C:/SPT`)
or via the environment variable `SPT_DIR`.

## Support

Bugs and questions: [GitHub issues](https://github.com/Kaanori/spt-alleausgaenge/issues).

## License

MIT — see [LICENSE](LICENSE).
