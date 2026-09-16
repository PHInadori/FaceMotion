# Changelog

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
