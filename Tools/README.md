# Local PowerShell tools

Run these scripts on Windows with PowerShell 5.1 or later. They resolve the repository from their own location, so the current working directory does not need to be the project root. Close this project in the Unity Editor before running a batch verification or build.

```powershell
.\Tools\Verify.ps1
.\Tools\Build.ps1
.\Tools\Play.ps1
```

Verification and building look only for Unity **6000.6.0f1** in the standard Unity Hub folder under Program Files. If your installation is elsewhere, supply its actual executable path with `-EditorPath`. The scripts do not use a global Unity CLI or silently select a different Editor installation. Build includes model and narrative verification and outputs `Build/Windows/LivingEmpiresCommunity.exe` with its data and license files.

```powershell
.\Tools\Verify.ps1 -EditorPath $UnityEditorExecutable
.\Tools\Build.ps1 -EditorPath $UnityEditorExecutable -TimeoutSeconds 3600
```

Set `$UnityEditorExecutable` to the full path of your installed Unity 6000.6.0f1 `Unity.exe` before using those examples. `Verify.ps1` defaults to a 30-minute timeout; `Build.ps1` defaults to one hour. Both propagate a nonzero Unity exit code, reject missing or stale success reports, and use exit code 124 if the process they started times out. Each writes a timestamped log under the ignored root `Reports` directory.

`Play.ps1` opens the game visibly and returns after launch. Balanced graphics and a 1440×900 window are defaults. `-Quality Economy`, `-Width`, and `-Height` change those settings. Unity chooses the graphics device unless you explicitly provide `-GraphicsDevice` with an adapter index appropriate to your machine. No laptop-specific adapter or graphics API is forced.

## Player checks

Build first, then run any of the following:

```powershell
.\Tools\TestPlayer.ps1
.\Tools\TestPlayer.ps1 -Suite Military
.\Tools\Benchmark.ps1 -Scenario Settlement
.\Tools\Benchmark.ps1 -Scenario Military
```

`TestPlayer.ps1` runs Integration, Assets, Navigation, and Military in that order by default, stopping on the first failure. Each suite uses a new visible player process. Keep the window focused and do not interact with it while the test sends input. The default timeout is ten minutes per suite. The runtime checks use isolated QA state; the integration, navigation, and military suites also check that manual chapter saves remain unchanged. An interrupted test can leave an isolated QA file in the ignored build reports folder.

`Benchmark.ps1` runs one seeded fixture, with an eight-second warm-up and a thirty-second sample. Its default timeout is five minutes. It opens a visible player and does not write manual saves. Keep it visible, close competing game instances and the Editor, and inspect the resulting capture before interpreting timing results. A fixture result is not evidence for every map size or platform.

Both tools accept the same quality, resolution, and optional graphics-device parameters as Play. Logs and timestamped report copies go to root `Reports`; original runtime reports and benchmark captures are under `Build/Windows/Reports`. A successful process exit without a fresh expected report is treated as failure. These scripts never substitute an older executable from another build folder.
