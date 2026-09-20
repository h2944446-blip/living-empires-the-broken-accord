# [WIP] Living Empires: The Broken Accord — a Unity settlement and militia prototype

I'm opening development of **Living Empires: The Broken Accord**, a small offline strategy game about building a river town, keeping trade promises through winter, and supporting a local militia. It's built in **Unity 6000.6.0f1 with URP 17.7.0**, and I'd like feedback on whether its decisions and controls make sense to someone encountering it for the first time.

![The Toll War scenario in Living Empires](https://raw.githubusercontent.com/h2944446-blip/living-empires-the-broken-accord/main/Documentation/images/toll-war.png)

There are two playable scenarios:

- **First Winter Delivery:** assign workers, establish production, restore a river crossing, complete two deliveries, and preserve a food reserve before winter. Trade moves through shipments, so agreeing to a contract and delivering it are separate steps.
- **The Toll War:** start with a prepared settlement and no standing army. Recruit spearmen and archers, support their wages and rations, and command squads against three finite raiding parties or their camp. This is a separate scenario, with its own save, rather than a campaign continuation.

This is an early prototype. The army is capped at six squads; soldiers share squad health, and defeated militia withdraw to recover. There is no multiplayer, territorial conquest, siege system, research tree, fog of war, or full campaign. Council scenes currently use text. The art combines procedural work with credited Kenney and iedalton assets.

The most useful playtest feedback would be:

- **Camera and selection:** is it clear which gestures pan the world, select troops, or issue an order?
- **Economy feedback:** can you understand why production has stopped, where a shipment is, and how much food is safe to export?
- **Recruitment:** are squad capacity, training, wages, rations, and recovery understandable before you commit resources?

[Source, setup instructions and controls](https://github.com/h2944446-blip/living-empires-the-broken-accord) · [Community release v0.1.0](https://github.com/h2944446-blip/living-empires-the-broken-accord/releases/tag/v0.1.0-community)

Small pull requests, reproducible bug reports, accessibility suggestions, documentation fixes, and licensed art contributions are welcome. The repository has contribution guidance and starter tasks. Participation is voluntary, with no job, payment, or revenue-sharing offer. Original project work uses MIT; third-party assets retain their own licenses and credits. Please include the scenario and steps when reporting a problem.
