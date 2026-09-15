# ADR-012: Mapping Profile Separation

- Status: Accepted
- Date: 2026-09-14

## Context

Timeline tracks carry a project-scoped binding today, but face motion generally needs a
per-avatar binding table so the same logical target ("mouth.smile") can point at different
concrete elements on different avatars. That table is avatar-specific knowledge. Storing
it inside `FaceMotionProject` would couple the project graph to a single avatar and would
force shares/presets to carry avatar detail.

## Decision

### An independent ScriptableObject

`AvatarMappingProfile` is its own ScriptableObject, separate from `FaceMotionProject` and
from presets:

- `profileId` (stable ID), `schemaVersion` (`MappingProfileSchemaVersion = 1`),
  `avatarFingerprint` (see ADR-011), `displayName`, and `List<BindingMappingEntry>`.
- Each `BindingMappingEntry` has `entryId` (stable ID), a logical target ID such as
  `"mouth.smile"`, a `LogicalTargetKind` (BlendShape or Transform today, additive later),
  and exactly one populated SDK-neutral binding (see ADR-010). Hints ride along but are
  never identity.
- The profile binds to an avatar through `SetAvatarFingerprint`, which is the record of
  which avatar structure it was built for.

### Clone vs Duplicate

- `CreateClone` preserves every ID (profile id, entry ids, fingerprint) and is the
  internal transactional copy.
- `CreateDuplicate` issues a fresh profile id and fresh entry ids while preserving the
  fingerprint, display name, and binding content. A duplicated profile is the same avatar,
  a new identity.

### Staleness never voids the profile

`AvatarMappingEvaluator` compares the profile's fingerprint with the fresh avatar's
fingerprint and then re-validates every binding against the fresh `AvatarIndex`:

- `Valid`: fingerprint matches and every binding resolves.
- `StaleButValid`: fingerprint differs but every binding still resolves exactly.
- `StaleWithMissing`: fingerprint differs and at least one binding is missing or ambiguous.
- `Invalid`: a binding is structurally illegal, or the fingerprint matches yet a binding
  is missing/ambiguous (a corruption that contradicts the fingerprint).

This is the remapping foundation: it always reports per-binding outcomes for the old
profile against the new avatar. Automatic fuzzy remapping is deliberately out of Phase B.

### Validation

`AvatarMappingProfileValidator` treats structural problems as blocking in the same
spirit as `ProjectValidator`: invalid profile id, invalid/uninitialized/future schema,
invalid entry id, duplicate entry id, missing logical target id, duplicate logical target,
uninitialized-kind/missing binding, and malformed fingerprint. A `NormalizeStructure` pass
recreates a missing list and removes null entries. Nothing here mutates profile data.

## Consequences

- The project graph stays avatar-agnostic and profiles compose with presets without
  circular ownership.
- A profile survives avatar edits: staleness is a warning state with the full per-binding
  evidence the UI needs to re-validate.
- Serialization round trips through the Unity serializer are covered by a permanent test
  proving ids, fingerprint, and bindings survive `SaveAssets -> CopyAsset -> LoadAssetAtPath`.
- Duplicate semantics mirror the Phase A project rules (clone keeps ids, duplicate is a
  new identity) so editors treat profiles consistently with animations.