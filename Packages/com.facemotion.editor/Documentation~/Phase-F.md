# Phase F: AnimationClip Export

Phase F exports only the selected FaceMotion animation to a Unity `.anim` asset. It does not create or modify FX controllers, parameters, expression menus, Modular Avatar components, or any other Phase G integration asset.

Export samples `CanonicalMotionEvaluator` at the `MotionSampler` frame grid: zero, every `1 / FrameRate` sample through the duration, and the exact duration when it is not frame-aligned. This preserves preview/export equivalence at every emitted key. Blend shapes use `SkinnedMeshRenderer` `blendShape.<name>` curves; local position and scale use three component curves; local rotation uses four shortest-path quaternion component curves with sign continuity.

The Export panel accepts only `.anim` destinations under `Assets/`. It validates the selected timeline and all enabled tracks before changing assets. Blocking diagnostics display a stable code, explanation, and suggested fix, and disable export. An existing AnimationClip is overwritten in place so its GUID and references remain stable. The output frame rate and loop setting are taken from the timeline on every export. Export reads authored data only; it does not normalize, snap, or otherwise mutate the source timeline.
