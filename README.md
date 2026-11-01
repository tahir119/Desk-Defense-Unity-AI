# Desk Defense: Dev vs Cockroaches

A one-tap casual game about every developer's workday: the deadline is close and the bugs keep coming. 🪳

You are Tahir, a game developer working 9 to 6. Cockroach-bugs crawl onto the desk and attack the monitor, laptop and keyboard. Every bug that reaches them slows the coding down. Tap them before they do, and ship 100% of the code before 6 PM to **DEPLOY**.

## Features

- Short story intro: a profile card, riding to the office, stairs, the lift to the 7th floor, and sitting down at the desk
- Every roach is a real bug: `NullReference`, `IndexOutOfRange`, `Off-by-one`… **Race Conditions** are fast, **Merge Conflicts** take 3 taps
- Chai power-up for a few seconds of slow motion
- Combos up to x10, squash-and-stretch, camera shake, "POOF!" star particles, speech bubbles
- The round is a 9:00 AM – 6:00 PM workday clock; the end screen shows bugs fixed, deploy time and best combo
- All art and sound are generated in code (no imported assets)
- Mobile-ready: portrait, 60 FPS, object pooling, no allocations in `Update`, safe area, back button pauses

## Open the project

1. Unity **6000.5.5f1** (Unity 6) with the Universal Render Pipeline.
2. Open this folder in Unity Hub and let the packages import.
3. Open `Assets/DeskDefense/Scenes/DeskDefense.unity` and press Play.

To regenerate the art and scene from scratch: **Desk Defense → Setup Everything**.

## Build

- **Desk Defense → Build WebGL (itch.io)**: browser build in `Builds/WebGL/DeskDefense`
- **Desk Defense → Build Android APK**: IL2CPP, ARM64, portrait, in `Builds/Android/DeskDefense.apk`

`DeskDefenseGame.unitypackage` contains the same `Assets/DeskDefense` folder for importing into another project.

## Code

| File | What it does |
|---|---|
| `Scripts/DDGame.cs` | Game loop, spawning, taps, combo, chai power-up, HUD, win/lose |
| `Scripts/DDIntro.cs` | Story intro built from shapes at runtime |
| `Scripts/Roach.cs` | Pooled cockroach: movement, wiggle, chewing, squash |
| `Scripts/Zone.cs` | Monitor / laptop / keyboard health and health bars |
| `Scripts/DDAudio.cs` | Synthesized sound effects and music |
| `Editor/DeskDefenseSetup.cs` | Generates sprites, builds the scene, build menus |

Built with Unity 6 and C# by Tahir.
