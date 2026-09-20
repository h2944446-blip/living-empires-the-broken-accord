# Third-party notices

This community source project includes the components below. Their original licenses and notices apply to the identified material and relevant derivatives. A repository license for project-authored code or content does not replace these terms. No creator endorsement is implied.

Paths in this document are relative to the repository root unless they are Markdown links. [PUBLIC_ASSET_INVENTORY.json](PUBLIC_ASSET_INVENTORY.json) records the included source assets, relative paths, byte sizes, SHA-256 hashes and associated Unity import metadata hashes. It is a filesystem snapshot, not a claim that every file appears in a player build.

## Kenney Fantasy Town Kit 2.0

Creator/distributor: **Kenney**. Source: [Fantasy Town Kit](https://kenney.nl/assets/fantasy-town-kit). License: [Creative Commons Zero 1.0 Universal (CC0)](https://creativecommons.org/publicdomain/zero/1.0/).

Selected source FBX models and palette textures are retained in `Assets/Art/ThirdParty/Kenney/Buildings` and `Assets/Art/ThirdParty/Kenney/fantasy-town-kit`. Original notices remain beside the source assets and in [Kenney-Fantasy-Town-License.txt](Licenses/Art/Kenney-Fantasy-Town-License.txt). Credit is retained voluntarily.

Project adaptations include modular building assembly, scale and pivot normalization, palette UV changes, opaque URP materials, combined meshes, original supporting geometry and gameplay colliders. Building derivatives are in `Assets/Art/ImportedGenerated/Buildings`, `Assets/Resources/ImportedArt/KenneyBuildings`, and the non-Sketchfab prefabs in `Assets/Resources/ImportedArt/Buildings`. The cart, planks and stall also have environment derivatives under `Assets/Art/ThirdParty/Kenney/GeneratedEnvironment` and `Assets/Resources/ImportedArt/Environment`.

## Kenney Nature Kit 2.1

Creator/distributor: **Kenney**. Source: [Nature Kit](https://kenney.nl/assets/nature-kit). License: [Creative Commons Zero 1.0 Universal (CC0)](https://creativecommons.org/publicdomain/zero/1.0/).

Ten selected models and accompanying material-color records are retained in `Assets/Art/ThirdParty/Kenney/nature-kit`. The original notice is retained beside the models and in [Kenney-Nature-License.txt](Licenses/Art/Kenney-Nature-License.txt). Credit is retained voluntarily.

Project adaptations include scale and ground-pivot normalization, palette materials and combined meshes. Derivatives are in `Assets/Art/ThirdParty/Kenney/GeneratedEnvironment` and `Assets/Resources/ImportedArt/Environment`. These depict trees, rocks, grass, plants, crops, soil rows and a log stack.

## Medieval Village

Work: **Medieval Village**, by **iedalton**. Source: [original Sketchfab model](https://sketchfab.com/3d-models/medieval-village-0e7dd1fd2cd64f828b625021e30704d4). License: [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), with [legal code](https://creativecommons.org/licenses/by/4.0/legalcode).

The original combined source is `Assets/Art/ThirdParty/Sketchfab/MedievalVillage/AllAssets.fbx`. Its SHA-256 is `7e1c3807d0fb5b8d74080b678f6d061f493624568f918f3e39f1639e5e144461`. The original FBX is retained unchanged. The supplied source archive did not contain a license file; the project attribution records the creator and license shown on the model listing. The model listing identifies iedalton and CC Attribution; the retained acquisition notice specifies CC BY 4.0.

Changes for Living Empires include extraction of building regions from the combined mesh, ground/pivot and scale normalization, mesh combination, source material colors converted to an opaque URP palette, gameplay selection colliders, and mapping to game building roles. Derived meshes/materials are in `Assets/Art/ImportedGenerated/SketchfabVillage`. The `house`, `warehouse`, `lumberyard` and `toolsmith` prefabs in `Assets/Resources/ImportedArt/Buildings` use these derivatives.

Retain the work title, creator, source and license links, and change notice when distributing this material or its derivatives. The complete project attribution is available beside the FBX and in [MedievalVillage-iedalton-ATTRIBUTION.txt](Licenses/Art/MedievalVillage-iedalton-ATTRIBUTION.txt). This credit does not imply endorsement by iedalton or Sketchfab.

## Liberation Sans and TextMesh Pro resources

`Assets/TextMesh Pro/Fonts/LiberationSans.ttf` and its font-derived SDF resources use the **SIL Open Font License 1.1**. The retained copyright notice credits Google Corporation (2010) and Red Hat, Inc. (2012), and records the reserved font names. Keep the full [Liberation Sans OFL notice](Licenses/LiberationSans-OFL.txt), also supplied in `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`, with redistributions of the font material. Font-specific terms remain separate from Unity shader and resource terms.

The remaining `Assets/TextMesh Pro` shaders, shader includes, style/settings assets and line-breaking resources come from Unity's TextMesh Pro essential resources, supplied with uGUI. The [uGUI package notice](Licenses/Packages/com.unity.ugui/LICENSE.md) identifies Unity Technologies and the [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license). These are Unity components, not project-authored assets. Package third-party notices continue to apply to separately identified components.

## Unity package dependencies

`Packages/manifest.json` and `Packages/packages-lock.json` identify the project's requested and resolved dependencies. The Unity Editor and registry package cache are not vendored in this community source project; contributors obtain them through Unity under their applicable terms.

[Licenses/Packages](Licenses/Packages) retains the available package license and third-party notice files, including Editor-only and historical development dependencies. [NOTICE_INVENTORY.json](Licenses/NOTICE_INVENTORY.json) records the copied notices and their source package versions. Inclusion of a notice is not a claim that its package is part of the current player or current dependency graph.

The retained licenses include Unity Companion licenses, the Unity Package Distribution License, and component-specific licenses such as the MIT notices for the IDE integrations. They must be read per component. In particular, the [Unity Package Distribution License](https://unity.com/legal/licenses/unity-package-distribution-license) does not grant a general right to republish package source. Package dependency references and copied notices do not relicense Unity or third-party source.

## Narrative media scope

The community source copy omits the earlier prerecorded narration WAVs and generated character/chapter PNG illustrations. Their account-specific generation records and local source manifests are also omitted. `Assets/Resources/Narrative/story.json` retains authored story text, fictional speaker identifiers and resource keys. References to absent media are resource names, not a grant of rights to those recordings or images. Runtime synthesized effects and project-authored geometry are separate from the third-party assets documented above.
