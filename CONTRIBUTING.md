# Contributing

Living Empires is an early Unity strategy game. Contributions can be code, art, testing, accessibility improvements, documentation, or clear reports of a problem. Small, reviewable pull requests are the easiest place to begin.

## Pick a change

Read the [current scope](README.md), [roadmap](ROADMAP.md), and [starter issue candidates](Documentation/STARTER_ISSUES.md). Check existing issues and pull requests before starting. For a large feature, engine upgrade, save-format change, or substantial new asset pack, open an issue describing the player problem and proposed approach first. A short discussion can keep a large change aligned with the project.

Comment on an issue if you intend to work on it, but do not assume an assignment or a promised merge. Maintainers may request changes, narrow the scope, or decline a proposal. Treat contributors respectfully and keep discussion focused on the work.

## Set up the project

Use Unity **6000.6.0f1** and the committed packages, including URP **17.7.0**. Follow the [README setup steps](README.md#open-and-run), open `Assets/Scenes/FirstWinterDelivery.unity`, and confirm that each chapter can start before changing behavior.

Fork the repository, create a branch for one purpose, and open a pull request when the change is ready for review. Draft pull requests are welcome when you want feedback on a concrete approach.

## Keep changes reviewable

- Keep simulation rules in the simulation code; presentation and input should call those rules instead of maintaining competing state.
- Preserve existing save compatibility where practical. Explain any migration or intentional incompatibility, and verify loading with representative saves.
- Include Unity `.meta` files for added or moved assets. Move assets through Unity when possible so GUID references remain intact.
- Do not commit `Library`, `Temp`, `Logs`, local save files, build output, credentials, or machine-specific configuration. Avoid unrelated scene, prefab, and formatting churn.
- Keep asset source, author, license, and required attribution with an asset contribution. Update the relevant third-party notices for imported or derived content.
- Describe substantial AI assistance in the pull request, and review the resulting code or assets yourself. A generated asset still needs a clear origin and redistribution basis; never add credentials or private account records as provenance.

Original code, documentation, and original art contributions are submitted under the project's MIT license. Third-party content keeps its own license and must be clearly identified. Do not submit content you cannot authorize the project to distribute. See [THIRD_PARTY_NOTICES.md](Documentation/THIRD_PARTY_NOTICES.md).

## Validate the change

Choose checks that exercise the behavior you changed. Start with `Tools/Verify.ps1` for economy, military, and public narrative verification, or use **Living Empires > Community > Verify economy, military and narrative** in the Editor. `Tools/Build.ps1` builds a Windows player; `Tools/TestPlayer.ps1` checks runtime integration, imported assets, navigation, and military controls. See the [tool instructions](Tools/README.md) for focused suites, alternate Editor paths, and visible benchmarks. Report how you invoked the relevant checks.

For gameplay changes, describe a short reproduction and the resulting behavior. For interface or art changes, include a screenshot and the tested resolution. Check both chapters if the change touches shared simulation, saves, input, or menus. For build changes, report the Unity version, target platform, build result, and material warnings.

Add focused regression coverage when it protects a behavior that could fail again. Documentation corrections do not need a new automated test. Say which checks you did not run and why. Local measurements should include the revision, hardware, resolution, graphics settings, and measurement method.

## Open a pull request

Explain the problem, the resulting change, and how you verified it. Link the related issue if there is one. Call out save migrations, new assets, package changes, and known limitations. Keep generated binaries and logs out of the source diff; summarize useful results in the pull request.

Contributors are credited through repository history and acknowledged release contributions. Participation is voluntary. No payment, employment relationship, or revenue share is promised by an issue, discussion, or pull request. Any separate paid arrangement would need its own explicit agreement.

Bug reports should include the chapter, steps to reproduce, expected and actual results, Unity or player version, and platform. Remove personal information and credentials from logs before sharing them. The issue forms provide a short template.
