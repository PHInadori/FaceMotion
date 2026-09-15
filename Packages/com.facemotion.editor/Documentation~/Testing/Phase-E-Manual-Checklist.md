# Phase E Manual Presets And Generation Checklist

| Check | PASS / FAIL | Notes |
| --- | --- | --- |
| Open a project, select an animation, select an avatar, and rebuild its index |  |  |
| The eight E.3 built-ins appear: Smile, Wink Left, Wink Right, Blink, Angry, Sad, Surprise, and Embarrassed |  |  |
| Create a mapping profile and explicitly confirm a candidate; it persists after reopening the asset |  |  |
| A duplicated matching blend-shape name is not suggested or offered by the affected built-in |  |  |
| Applying a confirmed built-in adds one key with generated provenance and is undone in one Undo action |  |  |
| Verify each resolved preset target pulses 0 -> 100 -> 0 at its documented stagger; missing optional targets do not disable or add tracks |  |  |
| Generate Blink and random generators add keys only after their button is pressed |  |  |
| Repeating a generator with identical settings and seed gives identical generated values/times on an empty animation |  |  |
| A generation record stores a non-empty JSON settings snapshot and regenerates identical pure motion |  |  |
| A manual key at a generated time remains unchanged and the result reports it as protected |  |  |
| Preview remains isolated and the selected scene avatar is not changed by generation alone |  |  |
| Scene Apply restores the original avatar on stop, avatar replacement, window close, reload, and play-mode transition |  |  |
| No animation clips, FX controllers, parameters, menus, or Modular Avatar objects are created |  |  |
| Console Error is 0 |  |  |
