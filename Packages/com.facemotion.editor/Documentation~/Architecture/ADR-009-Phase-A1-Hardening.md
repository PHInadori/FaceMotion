# ADR-009: Phase A.1 — Enum Stability, Hold/Interpolation Spec, Duplicate Provenance, and the GeneratedMotion Model

- Status: Accepted
- Date: 2026-09-14

## Context

Phase A completed the first working domain. Before Phase B the following contracts needed to be locked down because they are serialized or cross-cutting:

1. Serialized enum values for `TrackKind` and `InterpolationType`.
2. The exact semantics of "Hold" interpolation and how easing coefficients are shared between all value kinds.
3. Provenance rules for Duplicate operations (animation-level and track-level).
4. A type-safe, generator-independent generated-motion model that stays decoupled from the project/timeline graph and from merge policy.

## Decision

### Transform Scale confirmed

`TrackKind.TransformScale` is a first-class kind with the same treatment as position and rotation: a `TransformTrackPayload` with `Vector3KeyframeData`, evaluator support (`TryEvaluateScale`), validator/normalizer coverage, and command/undo coverage. Final serialized values:

```text
BlendShape = 0
TransformPosition = 1
TransformRotation = 2
TransformScale = 3
```

### InterpolationType final layout

```text
Hold       = 0
Linear     = 1
EaseIn     = 2
EaseOut    = 3
EaseInOut  = 4
Smooth     = 5
```

These values are a release contract. They must never be reordered, renumbered, or reused once a schema is released. Removing one leaves a reserved numeric gap. (Phase A's tentative `Linear=0..Smooth=4` layout was replaced before any fixture was persisted.)

### Hold semantics

- The eased coefficient is constant `0`: `InterpolationEase.Evaluate(Hold, t) === 0f`.
- The segment holds the left key's value for its whole span.
- The switch happens exactly at the right key's time: segment search at a boundary returns the boundary key's own value, and the endpoint clamp `t >= lastKey.Time` returns the last key's value. Thus a segment `0 (Hold) -> 0.5 (Hold) -> 1 (Linear)` evaluates `0` up to `t=0.5` inclusive, stays `50` until `1f`, and returns `100` at the endpoint.

### Single easing function

`InterpolationEase.Evaluate(InterpolationType, normalizedT)` is the one canonical easing function. All value kinds use the same eased coefficient:

- Float: `LerpUnclamped(left, right, eased)`
- Position / Scale: `Vector3.LerpUnclamped(left, right, eased)`
- Rotation: `Quaternion.Slerp(left, right, eased)` with quaternion sign continuity

Preview, generators, and export must not implement alternate easing mathematics.

### Duplicate provenance

**Animation duplicate** (`FaceMotionProject.DuplicateAnimation`):

- Project id kept; animation, track, and key ids fresh.
- Manual keys stay Manual.
- Generated keys stay generated and are re-bound to fresh generation records that mirror the source animation's records exactly (generator type, preset id, algorithm version, settings hash/snapshot) under new generation ids.
- Only generation records belonging to the source animation are duplicated.
- `KeyOrigin.GenerationId` entries are remapped with the same dictionary, so originals and duplicates never share a generation id.
- A generated key whose record has no mapping (defensive fallback) degrades to Manual so a copy can never claim an original generation id.
- Original records are never mutated.

**Track duplicate** (`FaceTrackData.Duplicate`, no remap): fresh track id and key ids; generated keys convert to Manual. A single-track copy of part of a batch-generated animation cannot honestly carry a fresh, complete generation record, and fabricating one would break the generation-registry invariant that a generated key points at a real record.

### GeneratedMotion model

`GeneratedMotion` / `GeneratedTrackMotion` / `GeneratedFloatKey` / `GeneratedVector3Key` form a pure, allocator-friendly intermediate model with no reference to timelines, projects, scenes, or Unity objects. Value kinds are type-safe:

- A generated blend shape track carries `List<GeneratedFloatKey>`.
- A generated position/rotation/scale track carries `List<GeneratedVector3Key>`.
- `TrackKind` on the track motion is the single source of truth; `TryCreateTrackPayload` produces the matching project payload.

Merge policy (currently Merge Rules = reject-all) is a separate concern applied afterwards; the generated-motion model itself knows nothing about merging.

### Schema version policy

The schema version stays `1`. This is a pre-release semantic change and the package persists only runtime-generated temp test assets, so there is no fixture migration to perform. After public release, any change to persisted meaning or shape requires a schema bump plus a migrator.

## Consequences

- Serialized enum numeric values are now stable and explicitly tested in `DomainTests`.
- Hold, EaseIn/Out/InOut, and Smooth have explicit spec tests, and float/vector3/rotation share the eased coefficient (`EvaluationTests`).
- Duplicate provenance is tested end-to-end: fresh generation ids, untouched originals, and independent duplicate-side management (`ProvenanceTests`).
- Generated motion can be previewed in isolation and applied through a merge policy without the generator knowing how timelines work.