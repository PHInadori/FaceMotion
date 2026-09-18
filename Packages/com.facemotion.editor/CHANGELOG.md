# Changelog

## [0.3.0] - 2026-09-18

### Added

- Batch VRChat integration: check multiple animations and export, plan, validate, and apply them together per animation.
- Direct batch integration with one copy-on-write asset set and rollback on failure.
- Modular Avatar batch integration with per-animation merge animator manifests and idempotent reruns.
- Batch preflight and per-animation result summaries with partial-apply diagnostics.
- User-editable animation names that are used directly as parameter and VRChat menu labels; generic "Play" labels are removed.
- Automatic selection of the avatar's current BlendShape value as the initial 0-second keyframe when a BlendShape track is created.
- Category-button BlendShape browser navigation with face-subcategory buttons alongside search, safety, and conflict display.

### Changed / Improved

- Hover Preview responsiveness, including immediate repaint on window entry and corrected enter delay.
- Idle-performance: reduced EditorApplication.update subscriptions and confined preview rendering to repaints with a playback-update gate.
- Timeline GUIContent allocation reduction.
- BlendShape browser navigation and filtering.
- Animation authoring workflow and VRChat menu clarity.

### Removed

- Preset generation UI (standard preset application).
- Random BlendShape and random rotation generation UI.
- Active Phase E generation implementation and generation-only tests.
- Unused preset/random generation UI strings and current-state documentation.

### Compatibility

- Legacy serialized generation records (`_generations`, `GenerationRecord`, `GeneratorType`, key provenance) remain readable; older FaceMotionProject assets load without a schema migration.
- No schema, migration, or integration-backend version bump is required.

## [0.2.0] - 2026-09-17

### Added

- Workflow guidance strip that always shows the next required step (avatar → animation → track → key → preview/integrate).
- One-click VRChat integration: Export → Plan → Validate → Apply with automatic clip export and clip linking.
- Modular Avatar-first backend selection by default when Modular Avatar is available, with a persisted per-project override.
- BlendShape browser tree with Face / Hair / Body / Clothes / Other categories, Eye / Blink / Brow / Mouth subcategories, confidence classification, and search.
- Pre-display of VRChat Blink, LipSync, FX, and Modular Avatar conflicts for the selected BlendShape before authoring.
- Preview playback (play / pause / stop) with loop and playhead synchronization.
- Scene-style preview camera navigation: Alt+Left orbit, Middle pan, Wheel / Alt+Right zoom, F to focus.
- Shortcut help panel in the window.
- Contextual tooltips and inline descriptions for primary controls and integration backends.
- Guidance and empty-state messages for the animation list, track list, and keyframe inspector.

### Changed / Improved

- Timeline zoom with Ctrl/Cmd + mouse wheel.
- The avatar selection is restored when the window is reopened or after a domain reload.
- Keyframe Inspector Backspace/Delete handling, playhead and keyframe match on add, and Add Key now use the current selection.
- The BlendShape browser and other panels use a resizable splitter.
- Integration diagnostics now cover the one-click flow with step-specific success and failure feedback.
- Consistent Japanese/English terminology and improved accessibility and narrow/large window layout behavior.

### Fixed / Safety

- Re-running the one-click integration is idempotent and preserves exported AnimationClip GUIDs.
- FaceMotion never overwrites an AnimationClip it does not own; a foreign clip at the export destination stops the export with a blocking diagnostic.
- A cross-backend warning is shown when switching between Direct and Modular Avatar.
- On failure the flow stops without leaving a partial applied state; replacement and rollback information is shown.
- Advanced/manual integration controls remain available alongside the one-click flow.

## [0.1.2] - 2026-09-16

### Fixed / Safety

- Fixed an `ArgumentOutOfRangeException` that could occur when the animation list changed during IMGUI rendering.
- Added blocking, non-mutating exception boundaries for Direct and Modular Avatar integration planning; raw exception details remain available in Technical details.
- Improved `FM-H-MA-BINDING-CONFLICT` diagnostics with conflict object, hierarchy path, Merge Animator component, Animator Controller, AnimationClip, and binding details.
- Added safe, non-destructive Hierarchy selection for conflicting Modular Avatar Merge Animator objects; unavailable objects cannot be selected.
- Kept duplicate avatar relative paths separate as `FM-AVT-0007` with specific resolution guidance.
- Fixed invalid `.meta` formatting in three package files.

### Changed / Improved

- Integration diagnostics now use the unified diagnostic presentation, localization, and context pipeline.
- Modular Avatar binding conflicts distinguish Merge Animator conflicts from ambiguous avatar relative paths.

## [0.1.1] - 2026-09-16

### Changed / Improved

- Improved user-facing diagnostic UX.
- Added localized Japanese and English explanations for all known diagnostics.
- Added action levels indicating whether an issue requires resolution.
- Added cause, impact, resolution, caution, and context details.
- Added safe Hierarchy navigation for supported diagnostics.
- Added a Technical details foldout for raw developer diagnostics.
- Improved `FM-AVT-0007` DuplicateTransformPath guidance.
- Added diagnostic documentation.

### Fixed / Safety

- Ambiguous object identities are never resolved by choosing an arbitrary duplicate.
- Unknown diagnostics preserve their raw message, suggested fix, context, and code.

## [0.1.0] - 2026-09-15

- Timeline: animated facial motion authoring for blend shapes and transforms (position, scale, rotation).
- Preview: isolated avatar clone with canonical timeline evaluation and reversible scene apply.
- Presets / random motion: logical built-in presets, conservative deterministic blink and random motion generation with provenance.
- Transform support: eased position/scale and shortest-path quaternion rotation.
- AnimationClip export: sampled AnimationClip output on a configured frame grid.
- Direct integration: copy-on-write FX controller, expression parameters, and expression menu generation with manifest-backed Apply and Rollback.
- Modular Avatar optional integration: MA backend gated by package availability; optional, never required.
- Reset baseline restore: generated `Reset.anim` restores the avatar baseline for owned bindings.
- Schema migration foundation: versioned persisted schemas, legacy upgrade, stable ID repair, and transaction-safe automatic migration for projects, mapping profiles, and integration manifests.
- Japanese-first UI.
- Distributed as a VPM package (`com.facemotion.editor` 0.1.0) under the MIT License.
