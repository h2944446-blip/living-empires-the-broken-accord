# Community Edition validation

Validation date: 2026-09-20. Target: Windows x64, Unity 6000.6.0f1, URP 17.7.0. This record applies to the initial `v0.1.0-community` source and player, not subsequent contributions.

## Build and source

The community source was imported into a separate Unity project without copying the original project's Library or package cache. Registry dependencies resolved and the Windows player built successfully. The final portable build was also produced with the committed `Tools/Build.ps1` wrapper: **0 errors, 1 warning**. The warning is a deprecated debug-symbol pragma in the supplied TextMesh Pro shader. No C# warnings remained.

The public copy excludes the earlier narrative recordings and generated illustrations. All 11 authored story entries remain as text. Community product identity gives it a separate persistent save directory from the original private prototype.

The finished ZIP was extracted to another directory and its executable passed all 46 interface integration checks. The download excludes Editor backup folders, local reports and QA saves. Its SHA-256 is published beside the release download.

## Automated checks actually run

| Suite | Passed checks | Scope |
| --- | ---: | --- |
| Economy model | 1,216 | 17 groups, including ferry and bridge charter wins, missed commitments, winter recovery, independent treasuries, production, shipments and save continuity |
| Military model | 243 | 15 groups, including recruitment, resource reservation, upkeep, commands, combat, recovery and persistence |
| Community narrative | 50 | Unique events, complete text and speakers, excluded media, isolated product identity |
| Windows interface integration | 46 | Rendered UI, raycast-verified button callbacks, complete scrollable text council, available sound controls, disk save/load |
| Imported art | 74 | Runtime imported assets and render configuration |
| Mouse navigation | 41 | 58 queued MouseState events through normal frames: drag, zoom, rotation, UI blocking and cancellation |
| Military runtime | 39 | 27 raycast-verified UI clicks and 11 queued MouseState events, timed recruitment, troop presentation and save/load |

Every listed suite completed with zero failures. Player tests used visible windows at **1440 × 900**, Economy graphics. They checked that the two manual campaign save files remained unchanged or absent. No recurring exception/error matches were found in the four player test logs.

Economy playthroughs are automated simulation playthroughs: ferry and bridge cases won on day 121; the deliberately late winter case recovered on day 141. This is not a claim that a person manually played every campaign route. UI tests use the real canvas and input handlers but do not establish usability for new players, every resolution, or accessibility needs. Synthesized audio transport was checked; speaker output was not independently recorded or assessed by listening.

## Performance

Measured on an Intel Core i7-10510U laptop with Intel UHD Graphics and 16,219 MiB reported RAM, Direct3D 11, Windows player. The Unity Editor was closed. Both fixtures use an 8-second warm-up and a 30-second frame sample with active simulation, a visible game window and an inspected gameplay capture.

Settings: **1440 × 900 windowed, Balanced, URP render scale 1.0, 2× MSAA, shadow distance 70, HDR off, 60 FPS cap, vSync off**. This laptop required graphics-device index 1 because another enumerated adapter is virtual; the public launcher leaves adapter selection at Unity's default unless explicitly overridden.

| Fixture | Mean FPS | 95th-percentile frame | Visible workload |
| --- | ---: | ---: | --- |
| Developed settlement | 59.94 | 17.32 ms | 39 buildings, 116 workers, 5 cargo models |
| Military settlement | 59.85 | 17.50 ms | 24 buildings, 54 workers, 32 soldiers, 1 cargo model |

These are seeded stress fixtures, not earned campaign saves. The cap limits the measured mean; these samples do not prove performance on other computers, maximum-scale games, or long sessions. The README screenshots are actual captures from these fixtures, without retouching.

## Reproduce

Run `Tools/Verify.ps1`, `Tools/Build.ps1`, and `Tools/TestPlayer.ps1`. Run `Tools/Benchmark.ps1 -Scenario Settlement` and `Tools/Benchmark.ps1 -Scenario Military` separately with the Editor closed. See [the tools guide](../Tools/README.md) for optional Editor paths, graphics adapters and resolutions. Local logs and QA saves are ignored by Git and are not included in the release ZIP.

The standalone player was tested on Windows only. No macOS, Linux, web, controller, multiplayer, 720p or 1080p results are claimed for this release. The current scope and missing systems are described in [the README](../README.md) and [roadmap](../ROADMAP.md).
