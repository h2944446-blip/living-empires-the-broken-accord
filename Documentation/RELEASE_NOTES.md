# Living Empires Community Edition v0.1.0

First public Windows prerelease of **Living Empires: The Broken Accord**. This is a playable prototype for feedback and open development.

- **First Winter Delivery:** build a settlement, run production, trade through visible shipments, fulfill contracts and recover from winter setbacks.
- **The Toll War:** recruit spearmen and archers, fund their upkeep, command six squads and defeat finite raids or their camp.
- Mouse camera controls, selection, markets, council transcripts, settings and separate chapter saves.
- Included credited art, MIT project source, contribution guide, roadmap and six starter issues.

Download **LivingEmpires-Community-v0.1.0-Windows-x64.zip**, extract the whole archive, and run **LivingEmpiresCommunity.exe** inside its folder. Keep the accompanying data, runtime files and notices together. Unity is not required to play the Windows build. Source development uses Unity **6000.6.0f1** with URP **17.7.0**.

The Community Edition has its own save directory. Earlier voice recordings and generated portraits are excluded; authored council text remains playable. Ordinary gameplay is offline.

Validation: 1,216 economy, 243 military-model, 50 narrative, 46 interface, 74 asset, 41 navigation and 39 military-runtime checks passed. The extracted ZIP passed its interface smoke test. On the tested Intel UHD laptop at 1440 × 900 Balanced, 30-second samples averaged 59.94 FPS for the developed settlement and 59.85 FPS for the military fixture, with a 60 FPS cap. See the [validation record](https://github.com/h2944446-blip/living-empires-the-broken-accord/blob/main/Documentation/VALIDATION.md) for scope, settings and limits.

Known limits: two separate scenarios, shared squad health, finite raids, no full campaign, multiplayer, territorial conquest, research tree, fog of war or siege system. Named characters use text rather than bespoke cinematic models. One supplied TextMesh Pro shader produces a deprecated-pragma build warning; no compilation errors remained. Windows is the only tested platform.

Please report your scenario, steps, expected result and actual result in [Issues](https://github.com/h2944446-blip/living-empires-the-broken-accord/issues). Read [CONTRIBUTING](https://github.com/h2944446-blip/living-empires-the-broken-accord/blob/main/CONTRIBUTING.md) before submitting changes. Project-owned work uses MIT; included third-party assets keep their own licenses and attribution requirements.
