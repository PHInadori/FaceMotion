# FaceMotion Architecture Baseline

## Status

> Historical architecture record. Current user-facing behavior is documented in
> `../README.md`; current compatibility and migration contracts are
> `../Compatibility.md` and `../Schema-Migration.md`.

Phase A complete and Phase A.1 hardening accepted on 2026-09-14. The working domain model, serialization, migration, Undo commands, canonical evaluator, and provenance contracts are implemented and covered by permanent EditMode tests. Phase B (avatar index / binding / mapping foundation) is complete: the SDK-neutral AvatarIndex, avatar fingerprint, mapping profile, exact resolver, and per-track binding validator are implemented and covered by permanent EditMode tests. Phase C is complete: the EditorWindow, session, avatar candidate pickers, timeline interaction, timeline settings, snap toggle, key editing, diagnostics, and avatar binding visibility are implemented. The code-level completion inventory is `Phase-C-Implementation-Inventory.md`.

Phase D preview foundations use an isolated hidden avatar clone rendered through `PreviewRenderUtility`, the canonical evaluator, reversible scene apply, and window/reload/play-mode cleanup. Phase E.1 exposes built-ins, unique-only mapping suggestions, and deterministic generated motion through explicit one-step Undoable editor actions. Export, Direct Integration, and the optional Modular Avatar Integration were completed in later phases; see the current user documentation rather than interpreting earlier phase scope as current product limits.

## Frozen Environment

- Unity: 2022.3.22f1
- VRChat SDK - Avatars: 3.10.5
- VRChat SDK - Base: 3.10.5
- Unity Test Framework: 1.1.33
- Package ID: `com.facemotion.editor`

The public package ID must use a publisher-owned reverse-DNS prefix. Renaming is not permitted after public release without an explicit package migration plan.

## Assembly Boundaries

```text
FaceMotion.Editor.Core
        ^
FaceMotion.Editor.Unity
        ^
FaceMotion.Editor.VRChat
        ^
FaceMotion.Editor.UI

FaceMotion.Editor.Tests -> Core, Unity, VRChat, UI
```

`Core` may use `UnityEngine` so serialized data can use `ScriptableObject`, `Vector3`, and `Quaternion`. It must not reference `UnityEditor` or any VRCSDK namespace. `Unity`, `VRChat`, and `UI` are Editor-only adapters with dependencies flowing upward through the graph. No lower assembly may reference a higher assembly.

## Namespace Rules

Serialized domain types use stable namespaces under `FaceMotion`:

- `FaceMotion.Data`
- `FaceMotion.Timeline`
- `FaceMotion.Animation`
- `FaceMotion.Presets`
- `FaceMotion.RandomMotion`
- `FaceMotion.Diagnostics`
- `FaceMotion.Serialization`
- `FaceMotion.Versioning`

Editor-only adapters use:

- `FaceMotion.Editor`
- `FaceMotion.Editor.Avatar`
- `FaceMotion.Editor.Preview`
- `FaceMotion.Editor.Export`
- `FaceMotion.Editor.VRChat`
- `FaceMotion.Editor.UI`
- `FaceMotion.Editor.Tests`

Namespaces use PascalCase nouns. Implementation folders may be more granular without creating a namespace for every folder. Serialized type namespaces and assembly names are compatibility contracts and require migration review before they can change.

## Serialization Model

The Phase A persistent graph will be:

```text
FaceMotionProject : ScriptableObject
  FaceMotionAnimationData[]
    FaceTimelineData
      FaceTrackData[]
        BlendShapePayload
        TransformPayload
```

`FaceTrackData` is a composition envelope. It stores common track metadata, a stable kind value, and explicit serializable payload fields. Exactly one payload is active. Load validation reports missing, duplicate, or kind-mismatched payloads. `SerializeReference` is not the primary persistence mechanism.

Blend shapes use `FloatKeyframeData`. Position, scale, and authored Euler rotation use `Vector3KeyframeData`. Unity object references and scene component references are not stored in the project graph.

## Stable IDs

Project, animation, track, key, generation, preset, and integration identities are lowercase 32-character GUID strings without separators. IDs are generated at object creation and are never derived from display names or list indexes.

- Duplicate operations create new IDs.
- Internal transactional copies preserve IDs.
- Paste creates new key IDs.
- Migration preserves existing valid IDs and creates IDs only where the old schema had none.
- IDs are compared ordinally and treated as opaque values.

## Version Rules

- Tool version follows SemVer and matches `package.json` for releases.
- Schema and algorithm versions are positive monotonic integers.
- Version zero means legacy or uninitialized data only.
- Serialized enum values are explicitly assigned and never reordered or reused.
- Removing an enum value leaves a reserved numeric gap.
- A schema number changes only when persisted meaning or shape changes.
- Generator algorithm version changes whenever identical settings and seed may produce different output.
- Integration backend version changes whenever generated VRChat asset structure or ownership semantics change.

## Migration Contract

`IProjectMigrator<TProject>` represents one forward step. A migration pipeline selects exactly one migrator for each `FromVersion`, works on a detached copy, applies steps sequentially, normalizes and validates the result, and commits only after every step succeeds. Failure returns diagnostics and leaves the source asset unchanged.

Migration is not performed from `OnAfterDeserialize`. Unity asset I/O, Undo registration, backup, and commit belong to the Editor persistence service.

## Provenance

Each key stores only:

- `OriginKind`
- `GenerationId`

Manual keys use `OriginKind.Manual` and an empty generation ID. Full generated metadata is stored once in a project-level generation registry:

- Generation ID
- Animation ID
- Generator type
- Source preset ID
- Algorithm version
- Settings hash
- Optional settings snapshot version and payload

This avoids repeating long metadata on every key. A generated key with a missing registry entry is retained but reported as orphaned provenance. Generated motion must not overwrite a manual key at the same time by default.

## Canonical Evaluator Contract

Phase A defines a stateless `IMotionEvaluator` with typed operations for:

- Float curves
- Position `Vector3` curves
- Scale `Vector3` curves
- Rotation curves returning `Quaternion`

Inputs are read-only keyframe sources plus time. Evaluation does not access scenes, assets, preview state, or VRCSDK types. Interpolation is defined by the outgoing key. Position and scale use eased `Vector3.Lerp`. Initial rotation mode is `ShortestQuaternion`, using eased `Quaternion.Slerp` and quaternion sign continuity. `EulerContinuous` is a reserved future mode; 360-degree spin is not supported initially.

Preview, preset base-value lookup, random preview, and export sampling must call this evaluator. They may cache prepared curve data but cannot implement alternate interpolation mathematics.

## Export Pipeline

```text
Canonical Evaluator
  -> Frame Sampler
  -> Optional Sample Processor
  -> Track-specific Curve Writer
  -> AnimationClip
```

The optional sample processor is initially a no-op and is the insertion point for future curve simplification. Initial export prioritizes sampled accuracy. Writers are Unity Editor infrastructure and are not part of Core.

## Avatar Mapping

The avatar root is `VRCAvatarDescriptor.gameObject`. Phase B implements the foundation:

- `UnityAvatarScanner` builds an SDK-neutral `AvatarIndex` (transforms, renderers, blend
  shapes) and reports duplicate-path/shared-mesh diagnostics. Inactive descendants are
  included; `sharedMesh == null` is safe.
- `RelativePathUtility` is the single relative-path authority (root `""`, separator `/`).
- `AvatarFingerprint` is a deterministic SHA-256 over sorted `T|/R|/B|` structural lines;
  weights, materials, instance IDs, and root names never participate (ADR-011).
- Binding identity is exact and ordinal: `RendererPath + BlendShapeName` for blend shapes,
  `TransformPath` for transforms. Ambiguity is explicit, never guessed (ADR-010).
- `AvatarMappingProfile` is a ScriptableObject separate from projects and presets, validated
  and evaluated against a fresh index without ever being voided by staleness (ADR-012).
- `UnityAvatarObjectCache` maps identities back to live objects with a revision; the cache
  and the index are runtime-only and never persisted into the project.
- Blend shape index is cache data, not persistent identity.

## Diagnostics

All modules use `FaceMotionDiagnostic` with code, severity, message, context ID, blocking state, and suggested fix. Codes follow `FM-<AREA>-<NUMBER>`, for example `FM-VRC-0001`. Codes and their meanings are stable once released.

Blocking is independent of severity. A warning may block an unsafe export while an error from an optional track may be non-blocking in a diagnostic-only operation.

## Editor Session State

Current time, selection, zoom, scroll, hover, drag, clipboard, and preview state belong to `FaceMotionEditorSession` and `TimelineViewState`, not project data. Optional domain-reload persistence uses `EditorPrefs` in the UI layer only, and currently restores project path, animation ID, current time, and zoom.

## VRChat Integration Boundary

```text
Domain integration request
  -> VRChat avatar adapter
  -> integration planner
  -> validator
  -> backend
  -> executor
  -> integration manifest
```

Core owns SDK-neutral requests, plans, ownership IDs, and diagnostics. `FaceMotion.Editor.VRChat` owns all VRCSDK types. The backend is replaceable: direct integration and Modular Avatar integration must not leak implementation-specific objects into Core.

Copy-on-write is the current direct-integration safety default, not an irreversible architecture constraint. Phase G must revalidate copy, Undo, rollback, shared-asset, and coexistence behavior with fixtures before the policy becomes a release guarantee.

## Test Strategy

Unity Test Framework EditMode tests are permanent package tests. Phase A covers domain, serialization, migration, Undo, and evaluation. Later phases add mapping, preview, export, and VRChat integration fixtures. Temporary validation scripts do not replace committed tests.

## Decision Records

- ADR-001: Track Composition
- ADR-002: Serialization
- ADR-003: Stable IDs
- ADR-004: Canonical Evaluator
- ADR-005: Preview Clone
- ADR-006: VRChat Integration Boundary
- ADR-007: Test Strategy
- ADR-008: Package Layout
- ADR-009: Phase A.1 — Enum Stability, Hold/Interpolation Spec, Duplicate Provenance, and the GeneratedMotion Model
- ADR-010: Avatar Index and Binding Identity
- ADR-011: Avatar Fingerprint
- ADR-012: Mapping Profile Separation
- ADR-013: Editor Session State
- ADR-014: Timeline UI Architecture
- ADR-015: Timeline Editing and Collision Policy
- ADR-016: Undo Transaction Boundaries
- ADR-021: Phase E Generation Application
