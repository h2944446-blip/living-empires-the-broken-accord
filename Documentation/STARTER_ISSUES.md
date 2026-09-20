# Starter issue candidates

These initial tasks are published as [issues 1–6](https://github.com/h2944446-blip/living-empires-the-broken-accord/issues). They are unassigned. Check the issue's current status and comment before starting, then reference it in one focused pull request. Keep the acceptance criteria in the pull request and record the checks you actually performed.

## 1. Add a one-page control reference

**Area:** documentation. **Suggested label:** `good first issue`.

New players need a compact reference that distinguishes settlement gestures from army gestures. Add `Documentation/CONTROLS.md` and link it from the README.

Acceptance criteria:

- Lists implemented camera, placement, selection, army order, pause, and save/load controls in plain language.
- Explains right-click versus right-drag, Shift selection, chapter restrictions, and Escape behavior without claiming queued orders exist.
- Includes one short practice sequence for each chapter, checked in the pinned Unity version.
- Uses relative links and contains no machine-specific paths, private saves, or unverified screenshots.

## 2. Record a clean-clone and desktop smoke-test checklist

**Area:** testing and documentation. **Suggested labels:** `good first issue`, `testing`.

Create a reusable manual checklist that another contributor can follow without the original development machine.

Acceptance criteria:

- Covers a fresh Unity import, opening the main scene, starting both chapters, pausing, switching modes, and separate save/load behavior.
- Covers HUD and menu readability at 1280×720 and 1920×1080, recording overflow or inaccessible controls as findings rather than silently calling a check passed.
- Records the revision, operating system, Unity version, and actual result for each executed check; unexecuted checks stay clearly marked.
- Uses disposable test saves and explains how to avoid replacing a player's existing saves.

## 3. Add a keyboard shortcut for Select all squads

**Area:** input. **Suggested label:** `enhancement`.

The Army panel has a Select all squads button. Add a documented shortcut that invokes the same selection behavior.

Acceptance criteria:

- Works while Army mode has gameplay focus in The Toll War and selects the same squads as the existing button.
- Does not issue orders or affect First Winter Delivery, menus, text entry, or an unfocused game window.
- Does not conflict with current pan, camera, save, or pause controls; the chosen shortcut is shown in the control reference or interface.
- Includes a focused input check or reproducible manual check for zero squads, several squads, and a blocked gameplay context.

## 4. Distinguish friendly and hostile squads without color alone

**Area:** accessibility and presentation. **Suggested label:** `enhancement`.

Friendly and hostile world indicators use different colors. Add a lightweight shape or symbol cue that remains legible when color differences are hard to see.

Acceptance criteria:

- Friendly and hostile squads have distinct visible cues at a normal gameplay zoom, including when neither is selected.
- Selection and health remain understandable; the additional cue does not intercept clicks or change simulation state.
- Provides comparison screenshots at a recorded resolution, including a grayscale view or other clear check that the cue survives loss of hue.
- Uses original geometry or an asset with documented redistribution terms, and adds no external art dependency.

## 5. Add short help for army command buttons

**Area:** interface. **Suggested label:** `enhancement`.

Explain the target and effect of each command before a player commits to it. Keep this change limited to command help rather than redesigning the Army panel.

Acceptance criteria:

- Provides brief help for Move, Attack, Patrol, Escort, Rally, Hold position, Retreat, and Demobilize.
- Explains that Escort targets a traveling caravan and that demobilization releases workers only after returning to the yard.
- Help works with pointer and keyboard focus, stays inside the viewport, and does not cause a world selection or order.
- Existing command behavior and cancellation remain unchanged; checks include a small desktop resolution and opening/closing the Army panel.

## 6. Show occupied army capacity beside recruitment

**Area:** interface. **Suggested label:** `good first issue`.

The recruitment panel explains the six-squad cap in static text. Add a live count so a player can see how much capacity is already reserved.

Acceptance criteria:

- Shows a concise count such as "4 / 6 squad slots used" beside recruitment.
- Uses the same rules as recruitment: queued recruits and wounded squads occupy slots, while hostile squads do not.
- Updates after recruitment, cancellation, completed training, demobilization, and loading a save, without changing resource or worker accounting.
- Verifies the display with zero, five, and six occupied slots, including a mixture of field squads, queued recruits, and a recovering squad.
