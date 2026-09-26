# Changelog

## [0.6.0] - 2026-09-26

### Added

- Quick Key: add a key at the playhead with a configurable BlendShape value (0-100) that persists across sessions. Quick Key updates the existing key at the same time instead of creating a duplicate.
- Key Actions menu in the Key Inspector: Copy, Paste, Duplicate, and Nudge (±1 / ±5 frames) in one place.
- Native Shortcut Manager actions for Quick Key, Copy, Paste, Duplicate, and Nudge (±1 / ±5 frames, earlier / later). They are registered without default bindings; assign keys in **Edit > Shortcuts**.

### Improved

- Timeline duration editing: the area beyond the animation length is greyed out, auto-fit includes long animations, and multi-key drags move as one group within the duration bounds.
- Timeline settings (length, frame rate, loop) are editable from the animation list and stay synchronized while authoring.
- Paste updates an existing key at the same track and time in place (value and interpolation, identity preserved) instead of skipping it; mixed insert and update happen as one atomic, undoable operation, and an invalid paste plan is rejected without changing the project.
- Paste is limited to the animation the keys were copied from, keeps the copied group's relative spacing, and fits the group inside the duration.
- Multi-track copy/paste and Duplicate now work across tracks; Duplicate keeps the group shape, snaps to frames, and rejects the whole operation if a destination collides with an unrelated key.
- Grabbing an already-selected key with a plain mouse drag moves the whole selection, and a plain click without dragging keeps the multi-selection. Ctrl/Cmd toggle and Shift range selection are unchanged.
- The Key Inspector's new-key default value for BlendShape tracks is 100, and text/numeric field focus is released when the selection or animation changes so shortcuts stay available.

### Fixed

- Quick Key after Timeline scrub / playhead movement, and background-click focus release.
- Avatar selection restore when reopening the FaceMotion window, including avatars in unsaved scenes and interrupted restores.
- Long-duration Timeline fit limitation and several selection, focus, and layout edge cases.

### Compatibility

- Existing projects remain supported; no project, mapping, or integration schema change is required.

## [0.5.1] - 2026-09-26

### Fixed / Improved

- Fixed fresh-project Modular Avatar integration path handling and default output-root lifecycle.
- Fixed exact non-loop playback endpoint evaluation and held-scrub responsiveness.
- Improved beginner workflow guidance, hover preview responsiveness, and localized Preview control wrapping.
- Fixed Shared FaceMotion BlendShape integration, including partner regeneration after add, remove, and re-add.
- Improved foreign FX binding, desired-state, and preflight diagnostics.
- Hidden true zero-effect BlendShapes from the candidate browser while preserving existing bindings.
- Improved Transform target hierarchy presentation and adjusted Middle Mouse Preview pan direction.

### Compatibility

- Existing projects remain supported; no project, mapping, or integration schema change is required.

## [0.5.0] - 2026-09-25

### Added / Improved

- Redesigned the beginner-friendly VRChat integration workflow: select the animations to use and update VRChat with one button.
- Removing all FaceMotion integrations is supported by unchecking all animations, and existing integrations are synchronized to the selected state.
- Improved Modular Avatar ownership, cleanup, remove/re-add behavior, and unsaved-scene safety.
- Added transactional rollback protection for Modular Avatar integration failures.
- AvatarIndex refresh is handled automatically during normal integration and preview workflows.
- Improved preview scrubbing responsiveness, including fast held scrubbing at timeline boundaries.
- Improved visibility of the currently edited animation and grouped technical/manual operations under Advanced.
- Improved animation export path defaults, duplicate-name handling, and Modular Avatar removal/reintegration lifecycle.

### Compatibility

- Unity 2022.3.22f1, VRChat SDK, and Modular Avatar are supported.
- Existing v0.4.0 users can update normally.
- No project schema or serialization schema change is required.

## [0.4.0] - 2026-09-21

### Added

- Timeline and editor performance and responsiveness improvements: faster playback and preview rendering, lower GUI allocation overhead, and more responsive hover feedback.
- Batch VRChat Integration: process multiple animations together with per-animation export → plan → validate → apply, plus preflight and per-animation result summaries.
- Authoring simplification: BlendShape category buttons, user-editable animation names used as parameter / menu label, clearer menu labels, and a new-track initial key that uses the avatar's current live value.
- Multi-Key Timeline Editing: Ctrl/Cmd+Click multi-select toggle, Shift+Click same-track range select (Shift takes precedence), blank-plot click to clear selection, drag to move selected keys, multi-key delete, Ctrl/Cmd+C / Ctrl/Cmd+V copy / paste, and Ctrl/Cmd+D duplicate (+1 frame).

### Changed

- Key Inspector is read-only with a selection count when multiple keys are selected.

### Removed

- Active Preset / Random Motion feature stack; retained only passive serialized compatibility where applicable.

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
