# TerraNova

Ein 2D Sandbox-Abenteuerspiel inspiriert von Terraria, entwickelt mit C# und MonoGame.

## Technologie-Stack

- **Engine:** MonoGame 3.8 (der offizielle Nachfolger von XNA 4.0, das Terraria verwendet)
- **Sprache:** C# 12 / .NET 8.0
- **Plattformen:** Windows, Linux, macOS

## Voraussetzungen

### Windows
1. [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. [Visual Studio 2022](https://visualstudio.microsoft.com/) (optional, empfohlen)
   - Workload: ".NET Desktop Development"

### Linux
```bash
# Ubuntu/Debian
sudo apt update
sudo apt install dotnet-sdk-8.0

# Fedora
sudo dnf install dotnet-sdk-8.0
```

### macOS
```bash
brew install dotnet-sdk
```

## Installation

```bash
# Repository klonen
git clone <repository-url>
cd TerraNova

# NuGet-Pakete wiederherstellen
dotnet restore

# Projekt bauen
dotnet build
```

## Ausführen

```bash
# Debug-Modus
dotnet run

# Release-Modus (optimiert)
dotnet run -c Release
```

## Projekt bauen für Verteilung

```bash
# Windows (self-contained)
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish/windows

# Linux
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish/linux

# macOS
dotnet publish -c Release -r osx-x64 --self-contained true -o ./publish/macos
```

## Projektstruktur

```
TerraNova/
├── Content/                    # Spielinhalte (Texturen, Audio, Fonts)
│   ├── Fonts/
│   ├── Textures/
│   ├── Audio/
│   └── Content.mgcb           # MonoGame Content Pipeline
├── src/
│   ├── Core/                  # Kern-Systeme
│   │   ├── Camera2D.cs        # Kamera mit Smooth-Follow
│   │   ├── FrameCounter.cs    # FPS-Tracking
│   │   ├── GameConfig.cs      # Konfiguration (JSON)
│   │   ├── InputManager.cs    # Tastatur/Maus/Gamepad
│   │   └── TextureManager.cs  # Textur-Verwaltung
│   ├── Entities/              # Spielobjekte
│   │   ├── Entity.cs          # Basis-Klasse
│   │   ├── Player.cs          # Spieler mit Mining/Building
│   │   └── Item.cs            # Items und Inventar
│   ├── World/                 # Welt-Systeme
│   │   ├── Chunk.cs           # Chunk-basierte Speicherung
│   │   ├── GameWorld.cs       # Welt-Verwaltung
│   │   ├── WorldGenerator.cs  # Prozedurale Generierung
│   │   ├── SimplexNoise.cs    # Noise für Terrain
│   │   └── TileType.cs        # Tile-Definitionen
│   ├── Systems/               # Spiel-Systeme
│   │   ├── LightingSystem.cs  # Dynamische Beleuchtung
│   │   └── ParticleSystem.cs  # Partikel-Effekte
│   ├── UI/                    # Benutzeroberfläche
│   │   └── UIManager.cs       # HUD, Inventar, Menüs
│   ├── Program.cs             # Einstiegspunkt
│   └── TerraNovaGame.cs       # Haupt-Spielklasse
├── TerraNova.csproj           # Projekt-Datei
└── TerraNova.sln              # Solution-Datei
```

## Architektur

### Chunk-System
Die Welt ist in 32x32 Tile-Chunks unterteilt für:
- Effizientes Laden/Entladen basierend auf Spielerposition
- Speicheroptimierung (nur aktive Chunks im RAM)
- Einfache Speicherung/Serialisierung

### Beleuchtungs-System
- Sonnenlicht-Propagation von oben
- Punkt-Lichtquellen (Fackeln, etc.)
- Light-Map-Textur für GPU-Rendering
- Dirty-Region-Tracking für inkrementelle Updates

### Welt-Generierung
- Multi-Oktaven Simplex Noise für Terrain
- Biome: Wald, Wüste, Schnee, Dschungel
- Höhlensystem mit Wurm-Algorithmus
- Erzverteilung basierend auf Tiefe
- Struktur-Generation (Bäume, Kabinen)

## Steuerung

| Taste | Aktion |
|-------|--------|
| A/D oder Pfeiltasten | Bewegen |
| W/Leertaste | Springen |
| Linke Maustaste (halten) | Block abbauen |
| Rechte Maustaste | Block platzieren |
| 1-9 | Hotbar-Slot wählen |
| Tab/I | Inventar öffnen |
| F3 | Debug-Anzeige |
| F11 | Vollbild |
| ESC | Pause |

## Konfiguration

Beim ersten Start wird `config.json` erstellt:

```json
{
  "ScreenWidth": 1920,
  "ScreenHeight": 1080,
  "Fullscreen": false,
  "WorldWidth": 4200,
  "WorldHeight": 1200,
  "PlayerSpeed": 4.0,
  "JumpForce": 11.0
}
```

## Erweiterung

### Neuen Tile-Typ hinzufügen

1. In `TileType.cs` Enum erweitern:
```csharp
public enum TileType : byte
{
    // ...
    MeinNeuerTile = 30,
}
```

2. Properties definieren:
```csharp
Set(TileType.MeinNeuerTile, true, 2.0f, TileType.MeinNeuerTile, 0, "Mein Tile");
```

3. Textur zum Atlas hinzufügen

### Neuen Item-Typ hinzufügen

1. In `Item.cs` ItemType erweitern
2. Properties in `ItemProperties` registrieren
3. (Optional) Crafting-Rezept hinzufügen

## Roadmap

- [ ] Enemies/Combat
- [ ] Crafting-System
- [ ] Boss-Kämpfe
- [ ] NPCs
- [ ] Multiplayer
- [ ] Sound/Musik
- [ ] Werkzeug-Upgrades
- [ ] Speichern/Laden

## Lizenz

MIT License

## Credits

- Inspiriert von [Terraria](https://terraria.org/) by Re-Logic
- Engine: [MonoGame](https://monogame.net/)
