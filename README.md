# Living Empires: The Broken Accord

A small, offline settlement and strategy game built in Unity. Grow a river town, keep trade promises through winter, and command a militia in a separate military chapter.

This repository is an early playable project being developed in public. It contains two bounded scenarios, with room for contributions to gameplay, usability, art, documentation, and testing. It is not yet a full civilization campaign.

[Windows prerelease](https://github.com/h2944446-blip/living-empires-the-broken-accord/releases/tag/v0.1.0-community) · [Latest verification](Documentation/VALIDATION.md) · [Report an issue](https://github.com/h2944446-blip/living-empires-the-broken-accord/issues)

To play the Windows prerelease, extract the whole release ZIP and run `LivingEmpiresCommunity.exe`. See the [portable player instructions](Documentation/WINDOWS_README.txt).

![The Toll War military chapter](images/toll-war.png)

![First Winter Delivery settlement chapter](images/first-winter.png)

## Playable chapters

**First Winter Delivery** is the founding economy chapter. Establish a mill, bakery, and warehouse; restore a river crossing; complete two deliveries; and keep seven days of food before day 120. Production uses assigned workers. Each town owns its money and goods, market purchases arrive by shipment, and protected food reserves limit exports. Contracts, negotiation, trust, winter, and recovery are part of the simulation.

**The Toll War** starts paused on day 30 with a prepared settlement, a restored bridge, barracks, 72 residents, 560 gold, and no standing army. Recruit spearmen and archers, pay their wages and rations, and issue move, attack, hold, patrol, escort, rally, retreat, and demobilization orders. Defeat three finite raiding parties or clear their camp to win. It is a separate scenario, not an automatic continuation of the founding chapter.

The army is limited to six squads, including recruits and wounded squads. Each squad has four visible soldiers and shared health. Defeated militia withdraw and recover; combat does not model individual deaths. Workers and soldiers use original stylized procedural rigs. Council scenes in this public edition use text; the earlier voice recordings and portrait images are not included.

## Open and run

1. Install Unity **6000.6.0f1** through Unity Hub. Add Windows Build Support if you want to create a Windows player.
2. Clone or download this repository and add its root folder as an existing project in Unity Hub.
3. Open it with the pinned Editor version and let Unity import assets and resolve packages. The project uses **Universal Render Pipeline 17.7.0**; keep the committed package manifest and lock file together.
4. Open `Assets/Scenes/FirstWinterDelivery.unity`, or choose **Living Empires > Open playable chapter**.
5. Press Play and choose a chapter from the menu.

No paid art pack or external voice service is needed for ordinary gameplay. Unity may need an internet connection for installation and package resolution. The repository contains source and assets; build a player locally with **Living Empires > Community > Build portable Windows release**. The Windows output is `Build/Windows/LivingEmpiresCommunity.exe`. Keep its data folder, runtime files, and license notices beside the executable when distributing a build.

## Controls

| Action | Input |
| --- | --- |
| Change settlement / army mode | Mode buttons or Tab; Army mode requires The Toll War |
| Pan | Right-drag over the world; WASD or arrow keys |
| Pan in Settlement mode | Left-drag empty ground |
| Select troops in Army mode | Left-click a squad or drag a selection box |
| Add or remove troops from selection | Shift-click; Shift-drag extends a selection |
| Move / attack | Short right-click on ground / a hostile squad or camp |
| Choose an explicit order | Army order button, then a target in the world |
| Zoom | Mouse wheel or camera + / − buttons |
| Orbit / tilt | Middle-drag; Q / E rotates |
| View another area / reset camera | Click the minimap / press Home |
| Pause / speed | Space or pause button / 1×, 3×, and 8× buttons |
| Place / inspect a building | Settlement build button then a valid tile / click a building |
| Cancel | Escape; right-click also cancels building placement |
| Save / load active chapter | F5 / F9 or menu buttons |

Right-drag pans without issuing an army order on release. Shift changes selection; queued movement orders are not implemented. New chapters replace the unsaved session, so save before switching. The chapters use separate save files, and loading pauses play. Unity's `Application.persistentDataPath` determines the save directory.

## Project layout

| Path | Purpose |
| --- | --- |
| `Assets/Scripts/Simulation.cs`, `SimulationTypes.cs`, `MilitarySimulation.cs` | Economy, serializable game state, and military rules |
| `Assets/Scripts/GameController.cs` | Time, chapter selection, and saves |
| `Assets/Scripts/GameUI.cs`, `StrategyCamera.cs`, `MilitaryController.cs` | Interface, camera gestures, and squad presentation |
| `Assets/Scripts/WorldArt.cs`, `ValleyScenery.cs`, `MilitaryArt.cs` | Original procedural world and character art |
| `Assets/Editor` | Project setup, import, verification, and build helpers |
| `Documentation` | Contribution tasks and asset/license records |

The [PowerShell tools](Tools/README.md) provide local verification, a Windows build, player launch, runtime checks, and visible benchmarks. `Tools/Verify.ps1` runs economy, military, and public narrative checks; use **Living Empires > Community > Verify economy, military and narrative** for the same checks in the Editor. `Tools/Build.ps1` builds and `Tools/Play.ps1` launches the player. Reports from earlier private builds do not establish results for this public edition; validate the revision you are using. No general performance or platform compatibility guarantee is implied.

## Join development

Start with [CONTRIBUTING.md](CONTRIBUTING.md) and the [starter issue candidates](Documentation/STARTER_ISSUES.md). Small pull requests, reproducible bug reports, and playtest observations are welcome. The [roadmap](ROADMAP.md) distinguishes the current game from possible future work; it is not a release schedule. Changes are tracked in [CHANGELOG.md](CHANGELOG.md).

Current limits include no territorial conquest, cavalry, siege engines, treaty system, research tree, fog of war, multiplayer, or complete open-world campaign. There are no queued waypoints or control groups. Equipment upgrades affect statistics without replacing each soldier's model, and archer damage is resolved without physical arrow projectiles. Named heroes do not yet have bespoke 3D likenesses or cinematic performances.

## License and credits

Original project code, documentation, and original project-generated art are offered under the [MIT License](LICENSE). That license does **not** replace the terms of third-party content, imported or derived assets, Unity software, or Unity packages.

Read [THIRD_PARTY_NOTICES.md](Documentation/THIRD_PARTY_NOTICES.md) and the records under [Documentation/Licenses](Documentation/Licenses) before redistributing assets or a player build. Kenney assets retain their CC0 terms. The included iedalton assets retain their CC BY attribution requirements. Unity packages and fonts retain their own notices and licenses. Original procedural art is distinct from prefabs or materials derived from imported assets.

Contributors receive credit through repository history and acknowledged release contributions. Contributions are voluntary; submitting an issue or pull request does not create employment, payment, or revenue-sharing commitments.
