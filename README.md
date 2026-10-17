# Polyus — gravity platformer prototype

Unity **6000.6.3f1**, Universal 2D, Input System **1.20.0**.

## Run in Unity

1. Open this repository folder in Unity Hub using the pinned editor version.
2. Wait for package import and script compilation. Check Console for errors.
3. Choose **Polyus → Create or open prototype** in the top menu.
4. Save any pending scene changes when Unity asks. The command creates
   `Assets/Scenes/PolyusPrototype.unity` if absent, or opens the existing scene.
   It does not overwrite `Game.unity` or `SampleScene.unity`.
5. Set the Game view to **16:9**, then press **Play**.

The first Play opens the prototype menu. Click **Play / Resume** to start.
The room, sprites and interface are generated at runtime. A mostly empty scene
before Play is expected. These are simple colored shapes, not final artwork.
No downloaded art, external services, secrets or paid assets are required.

## Controls

| Action | Keyboard | On-screen |
| --- | --- | --- |
| Move | A / D or left / right arrows | Hold < / > |
| Jump when grounded | Space | JUMP |
| Reverse gravity | G | FLIP |
| Pause / resume | Escape | II |
| Restart room | R | RETRY |

Shooting and aiming are automatic when a drone is in range and visible.
Platforms block shots. Eliminate four drones to win. The hero has 100 HP,
takes 20 damage per hit, and has one second of invulnerability after damage. FLIP has a
0.35-second cooldown. Jump pushes away from the current floor or ceiling.
Leaving the app pauses the prototype; resume using II.

## Manual acceptance checks

Unity Editor is not installed in the cloud workspace. Runtime compilation,
playability, rendering and device builds must be checked in Unity; they are
not claimed as tested by the cloud changes.

- Start the generated scene: room, cyan hero, four orange drones and HUD appear.
- Move both directions. The hero stands on platforms and cannot leave the walls.
- Jump from the floor, then FLIP and land on the ceiling. Jump works there too.
- Shoot drones automatically with clear line of sight. Solid platforms block fire.
- Take a hit: HP drops by 20, followed by a brief flashing invulnerability period.
- Defeat all four drones: ROOM CLEARED appears; lose all HP: TRY AGAIN appears.
- Pause: movement, drone motion and shots stop. Resume and RETRY work repeatedly.
- Hold movement with one finger while tapping JUMP / FLIP with another on a device.

## Android later

Install Android Build Support, SDK & NDK Tools, and OpenJDK through Unity Hub.
In File → Build Profiles, select Android and add `PolyusPrototype.unity` to
the scene list. Set landscape orientation in Player Settings, then build to
a connected test device. Store SDK integration, audio and production artwork
are not included. See the monetization guide for the simulated economy.

## Repository

Keep `Assets` (including `.meta` files), `Packages`, and `ProjectSettings`
under version control. `.gitignore` excludes Unity caches and local IDE files.
Use the existing checkout; cloud tasks are already isolated and do not need
additional Git worktrees. Do not copy a later version over unsaved local work.
