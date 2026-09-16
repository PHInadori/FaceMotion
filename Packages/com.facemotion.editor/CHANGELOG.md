# Changelog

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
