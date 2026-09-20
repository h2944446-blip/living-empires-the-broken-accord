# Living Empires: The Broken Accord

**Build Riverhold. Keep your promises. Defend the crossing.**

**Community Edition v0.1.0 · Windows x64 · Offline · Unity 6 / URP**

Grow a river town, keep trade promises through winter, and command a militia in two playable strategy scenarios. This is an early prototype open to playtesting and community contributions.

![Riverhold's militia deployed beside the restored bridge in The Toll War](https://raw.githubusercontent.com/h2944446-blip/living-empires-the-broken-accord/main/Documentation/images/toll-war.png)

**[Download the Windows game — 39.2 MiB](https://github.com/h2944446-blip/living-empires-the-broken-accord/releases/download/v0.1.0-community/LivingEmpires-Community-v0.1.0-Windows-x64.zip)** · [Source and controls](https://github.com/h2944446-blip/living-empires-the-broken-accord) · [Join development](https://github.com/h2944446-blip/living-empires-the-broken-accord/issues)

Extract the entire ZIP, then run **LivingEmpiresCommunity.exe**. Keep its data, runtime files and license notices together. Unity is not required to play.

## Build a town that can survive winter

In **First Winter Delivery**, assign workers to farms and workshops, turn grain into bread, restore a ferry or bridge, and fulfill delivery commitments while preserving food for your own people.

![Developed settlement with production buildings, workers, river crossings and visible cargo](https://raw.githubusercontent.com/h2944446-blip/living-empires-the-broken-accord/main/Documentation/images/first-winter.png)

*A developed Riverhold performance fixture, with active production, workers and traveling cargo.*

## Make promises you can keep

Delivery offers show quantities, payments and deadlines. Negotiate terms, dispatch cargo and earn trust by delivering on time; missed commitments and winter setbacks have recovery paths.

![Delivery contract interface with Pinewatch and Ironvale offers, deadlines, payments and negotiation buttons](https://raw.githubusercontent.com/h2944446-blip/living-empires-the-broken-accord/main/Documentation/images/delivery-contracts.png)

*The actual contract interface in the founding chapter.*

## Recruit and command your militia

**The Toll War** begins with a prepared settlement and no standing army. Recruit spearmen and archers, cover wages and rations, and command up to six squads against three finite raiding parties or their camp.

![Selected squads beside the barracks with Move, Attack, Patrol, Escort, Rally, Hold position and Retreat controls](https://raw.githubusercontent.com/h2944446-blip/living-empires-the-broken-accord/main/Documentation/images/army-commands.png)

*Mouse selection and the working army command bar. The military chapter has its own save.*

All four images are unretouched Windows gameplay captures at 1440 × 900. The two overview scenes use seeded performance fixtures; the contract and army-interface views come from runtime tests. They show the current prototype, not earned campaign progress. [Capture details](https://github.com/h2944446-blip/living-empires-the-broken-accord/blob/main/Documentation/images/README.md).

## Included in this release

- Two separate scenarios, mouse camera controls, selection, markets, council transcripts, settings and chapter saves.
- Credited art, MIT project source, contribution guidance, a roadmap and six starter tasks.
- Offline gameplay and a separate Community Edition save directory. Earlier voice recordings and generated portraits are excluded; authored council text remains playable.

Source development uses **Unity 6000.6.0f1** with **URP 17.7.0**. Project-owned work uses MIT; third-party assets retain their own licenses and attribution requirements.

<details>
<summary><strong>Test results, performance and current limits</strong></summary>

1,216 economy, 243 military-model, 50 narrative, 46 interface, 74 asset, 41 navigation and 39 military-runtime checks passed. The extracted ZIP passed its interface smoke test.

On the tested Intel UHD laptop at **1440 × 900 Balanced**, 30-second samples averaged **59.94 FPS** for the developed settlement and **59.85 FPS** for the military fixture, with a **60 FPS cap**. See the [validation record](https://github.com/h2944446-blip/living-empires-the-broken-accord/blob/main/Documentation/VALIDATION.md) for settings and scope.

Current limits: two separate scenarios, shared squad health, finite raids, no full campaign, multiplayer, territorial conquest, research tree, fog of war or siege system. Named characters use text rather than bespoke cinematic models. One supplied TextMesh Pro shader produces a deprecated-pragma build warning; no compilation errors remained. Windows is the only tested platform.

</details>

**Help shape the next version:** [report a bug or pick a starter task](https://github.com/h2944446-blip/living-empires-the-broken-accord/issues). Include your scenario, steps, expected result and actual result. Read [CONTRIBUTING](https://github.com/h2944446-blip/living-empires-the-broken-accord/blob/main/CONTRIBUTING.md) before submitting changes.
