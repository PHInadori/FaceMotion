# FaceMotion Schema Migration

Technical documentation for the schema versioning and migration implementation (Phase I.2). Covers the persisted schema inventory, the migration pipeline, per-asset migration rules, transaction safety, automatic boundaries, diagnostics, and test guarantees.

User-facing contract: [Compatibility.md](Compatibility.md).

## Version ownership

All version constants live in `FaceMotion.Core/Versioning` (`FaceMotionVersions.cs`):

| Constant | Value |
|---|---|
| `ProjectSchemaVersion` | 1 |
| `PresetSchemaVersion` | 1 |
| `IntegrationManifestVersion` | 1 |
| `GeneratorAlgorithmVersion` | 1 |
| `IntegrationBackendVersion` | 1 |
| `MappingProfileSchemaVersion` | 1 |
| `AvatarFingerprintAlgorithmVersion` | 1 |
| `AvatarFingerprintFormatVersion` | 1 |
| `LegacySchemaVersion` | 0 |
| `LegacyGeneratorAlgorithmVersion` | 0 |

Rules:

- Version 0 is the legacy equivalent of "no version field was present" and is the minimum supported version.
- Negative versions mean uninitialized data and are refused, never migrated.
- Versions greater than current are future schemas and are refused, never migrated.
- `LegacyGeneratorAlgorithmVersion` (0) is recorded provenance that predates algorithm versions; it is **never lifted** during migration.

## Persisted schema inventory

Four ScriptableObject roots are persisted:

```
FaceMotionProject
  [SerializeField] string     _projectId
  [SerializeField] int        _schemaVersion
  [SerializeField] FaceMotionAnimationData[]          _animations
  [SerializeField] GenerationRecord[]                 _generations

FaceMotionAnimationData          (inline child)
  [SerializeField] string           _animationId
  [SerializeField] string           _displayName
  [SerializeField] FaceTimelineData _timeline

FaceTimelineData                  (inline child)
  [SerializeField] float         _duration
  [SerializeField] float         _frameRate
  [SerializeField] FaceTrackData[]  _tracks

FaceTrackData                    (inline child)
  [SerializeField] string                      _trackId
  [SerializeField] TrackKind                   _kind
  [SerializeField] bool                        _enabled
  [SerializeField] int                         _displayOrder
  [SerializeField] BlendShapeTrackPayload      _blendShape   (BlendShape kind)
  [SerializeField] TransformTrackPayload       _transform    (transform kinds)

GenerationRecord                 (inline child)
  [SerializeField] string          _generationId
  [SerializeField] string          _animationId
  [SerializeField] GeneratorType   _generatorType
  [SerializeField] string          _sourcePresetId
  [SerializeField] int             _algorithmVersion
  [SerializeField] string          _settingsHash
  [SerializeField] string          _settingsSnapshot   (JSON)

AvatarMappingProfile
  [SerializeField] string                _profileId
  [SerializeField] int                   _schemaVersion
  [SerializeField] AvatarFingerprint     _avatarFingerprint
  [SerializeField] string                _displayName
  [SerializeField] BindingMappingEntry[] _mappings

DirectIntegrationManifest
  [SerializeField] VRCAvatarDescriptor   Avatar
  [SerializeField] RuntimeAnimatorController  OriginalFx
  [SerializeField] VRCExpressionParameters    OriginalParameters
  [SerializeField] VRCExpressionsMenu         OriginalMenu
  [SerializeField] AnimatorController         GeneratedFx
  [SerializeField] VRCExpressionParameters    GeneratedParameters
  [SerializeField] VRCExpressionsMenu         GeneratedMenu
  [SerializeField] VRCExpressionsMenu         GeneratedSubMenu
  [SerializeField] string                     ParameterName
  [SerializeField] string[]                   OwnedAssetPaths
  // Phase I.2 compatibility fields:
  [SerializeField] int                SchemaVersion
  [SerializeField] string             IntegrationId
  [SerializeField] string             BackendId
  [SerializeField] string             AvatarFingerprint
  [SerializeField] string             AnimationId
  [SerializeField] IntegrationState   State

ModularAvatarIntegrationManifest
  [SerializeField] VRCAvatarDescriptor  Avatar
  [SerializeField] string               AvatarGlobalId
  [SerializeField] string               IntegrationObjectName
  [SerializeField] string               IntegrationObjectGlobalId
  [SerializeField] string               ParameterName
  [SerializeField] string[]             OwnedAssetPaths
  // Phase I.2 compatibility fields (same shape as Direct):
  [SerializeField] int                SchemaVersion
  [SerializeField] string             IntegrationId
  [SerializeField] string             BackendId
  [SerializeField] string             AvatarFingerprint
  [SerializeField] string             AnimationId
  [SerializeField] IntegrationState   State
```

Serialized roots are ScriptableObjects; children are inline (`[SerializeField]`, not `SerializeReference`). Presets and avatar fingerprints carry their own version/format fields and are outside the project graph (see `Architecture/ADR-002-Serialization.md`).

A list field is never null after Unity deserialization, but the migration path treats a null list defensively as an empty list (`CreateClone` guards), and `NormalizeStructure` recreates a missing list.

## Migration pipeline architecture

`ProjectMigrationPipeline` (`FaceMotion.Core/Serialization`) coordinates project migration:

```
Migrate(source)
  null source                         -> MigrationFailed (blocking FM-MIG-0004)
  schemaVersion < 0                   -> Uninitialized   (blocking FM-MIG-0002)
  schemaVersion > target              -> FutureSchema    (blocking FM-MIG-0001)
  schemaVersion == target             -> CompleteCurrentSchema (clone + normalize + validate)
  otherwise                           -> build chain 0 -> 1, clone, migrate, normalize, validate, commit
```

- Chain building requires exactly one migrator per `FromVersion` and each migrator must advance exactly one schema step, otherwise `VersionGap` (blocking `FM-MIG-0003`).
- Work always happens on a detached deep clone (`FaceMotionProject.CreateClone`), never on the source.
- After migration the clone is normalized (`ProjectNormalizer.Normalize`) and validated (`ProjectValidator.Validate`); validation failures cancel with `ValidationFailed` (blocking `FM-MIG-0005`).
- A `Migrated` result prepends an `FM-MIG-UPGRADED` info diagnostic.
- The source is never mutated; a success result carries the detached migrated clone.

`IProjectMigrator<TProject>` is one forward step; `ProjectLegacyMigrator` implements `0 -> 1` for `FaceMotionProject`.

## FaceMotionProject migration

`ProjectLegacyMigrator.TryMigrate` runs three repair passes on the detached clone:

1. **Project ID**: missing/malformed `_projectId` is regenerated (`FM-MIG-ID-REPAIRED`).
2. **Animations** (per entry):
   - Null entry: removed (`FM-MIG-PARTIAL`).
   - Animation ID repaired/duplicated via `MigrationIds.RepairUnique`(first-in-list keeps the canonical ID; `FM-MIG-ID-REPAIRED` / `FM-MIG-DUPLICATE-ID`).
   - Null timeline: replaced with a default timeline (`FM-MIG-PARTIAL`).
   - Timeline duration non-finite or <= 0 -> `1f`, frame rate non-finite or <= 0 -> `60f` (`FM-MIG-PARTIAL`).
3. **Generation records** (see section below).

`MigrationIds` semantics:

- `Repair(existing)`: new stable ID when missing/malformed, otherwise unchanged.
- `RepairUnique(existing, seen)`: same, plus deduplication across the traversal; the first valid occurrence is always preserved.

### Timeline / Track / Key migration

Inside each track:

- Null track entry: removed (`FM-MIG-PARTIAL`).
- **Unsupported track kind** (`Enum.IsDefined` fails or not a known kind): **blocking** `FM-MIG-MALFORMED`, the track is left untouched.
- Track ID repaired/duplicated (first keeps canonical).
- BlendShape kind: null payload -> empty payload (`FM-MIG-PARTIAL`); then per-key repair.
- Transform kind: null payload -> empty payload (`FM-MIG-PARTIAL`); `TransformRotation` with a rotation mode other than `ShortestQuaternion` is **blocking** `FM-MIG-MALFORMED` and left untouched; otherwise per-key repair.

Per-key repair (both `FloatKeyframeData` and `Vector3KeyframeData`):

- Null key entry: removed (`FM-MIG-PARTIAL`).
- Key ID repaired/duplicated (first keeps canonical).
- Time non-finite or negative -> `0f`.
- Float value non-finite -> `0f`; Vector3 value non-finite in any component -> `Vector3.zero`.

Optional, consistency-level checks that produce warnings only (never destructive): key time beyond the timeline duration (`FM-KEY-0007`), overlapping key times (`FM-KEY-0004`), unsorted keys (stable-sorted by normalizer).

## AvatarMappingProfile migration

`AvatarMappingProfileMigration.TryMigrate` (`FaceMotion.Core/Avatar`):

- `null` profile -> blocked `FM-MAP-0002`.
- `SchemaVersion < 0` -> blocked `FM-MAP-0004` (uninitialized).
- `SchemaVersion > 1` -> blocked `FM-MAP-0003` (future).
- Otherwise clones, then if not already current:
  - Profile ID repaired (`FM-MIG-ID-REPAIRED`).
  - Entry IDs repaired/deduplicated per `RepairUnique` (first keeps canonical).
  - Null mapping entries removed (`FM-MIG-PARTIAL`).
  - `NormalizeStructure` recreates a missing list and null-checks entries.
  - A malformed stored `AvatarFingerprint` is preserved as-is and flagged with `FM-MIG-PARTIAL`; valid fingerprints/binding paths are never changed.
  - Schema set to `MappingProfileSchemaVersion` and `Applied = true`.
- No-op returns `Applied = false` on the clone (never applied in memory).

## GenerationRecord migration

Project-level generation registry entries are migrated as follows:

- Null record entry: removed (`FM-MIG-PARTIAL`).
- Generation ID repaired/deduplicated (first keeps canonical).
- `AlgorithmVersion == LegacyGeneratorAlgorithmVersion (0)`: warning `FM-MIG-PARTIAL`, value **never lifted**.
- Empty settings snapshot: warning `FM-MIG-PARTIAL`, record retained.
- Snapshot JSON that fails to parse or reports `FormatVersion != 1`: warning `FM-MIG-PARTIAL`, record retained.

Generated provenance attached to keys (`KeyOrigin.Kind` + `GenerationId`) survives migration untouched. During normalization, built-in preset IDs in the record and in the JSON snapshot are normalized (`FaceMotionBuiltins.NormalizeId`); the record's `SourcePresetId` and snapshot JSON may therefore be rewritten to the canonical preset id form. This is expected and is not a loss of provenance.

## DirectIntegrationManifest migration

`DirectIntegrationManifestMigration.TryMigrateOnUse` runs in memory at the use boundary (Apply / Rollback). Returns `MigrationResult { Applied, Blocked, Diagnostics }`:

- `null` manifest -> blocked `FM-G-MANIFEST`.
- `SchemaVersion > IntegrationManifestVersion` -> blocked `FM-MIG-FUTURE-VERSION`.
- `Avatar == null` -> blocked `FM-MIG-INTEGRATION-AMBIGUOUS`.
- Otherwise additive upgrades on the same object:
  - `SchemaVersion` set to current.
  - `BackendId` set to `direct-vrchat-sdk3` when missing/different (`FM-MIG-PARTIAL`).
  - `IntegrationId` repaired when missing/malformed (`FM-MIG-ID-REPAIRED`).
  - `State == Unknown` -> `EvaluateState` from the live avatar references (FX / parameters / menu point at the generated assets -> `Attached`, else `Detached`).
  - Any applied field -> `FM-MIG-UPGRADED` info.

A **recorded `State` is left as-is**; state reconstruction happens only for `Unknown`/legacy.

## ModularAvatarIntegrationManifest migration

`ModularAvatarIntegrationManifestMigration.TryMigrateOnUse` runs in memory at the use boundary (Apply / Remove):

- `null` manifest -> blocked `FM-H-MA-MANIFEST`.
- `SchemaVersion > IntegrationManifestVersion` -> blocked `FM-MIG-FUTURE-VERSION`.
- Additive upgrades identical in shape to Direct (`BackendId` set to `modular-avatar`).
- `State == Unknown` -> `TryEvaluateState`:
  - Owner = `operationAvatar`, else `ResolveAvatar(manifest)` (live reference, then `GlobalObjectId` from `AvatarGlobalId`).
  - Owner unresolvable -> blocked `FM-MIG-INTEGRATION-AMBIGUOUS`.
  - Owned integration object resolves (full ownership check) -> `Attached`.
  - No owned object but a child has the recorded `IntegrationObjectName` -> blocked `FM-MIG-INTEGRATION-AMBIGUOUS`.
  - Otherwise -> `Detached` (assets and manifest retained).

Recorded states are never recomputed.

### IntegrationId / BackendId / AvatarFingerprint / AnimationId

- `IntegrationId`: generated once (`StableId.New()`); valid IDs are preserved across reapplies; missing/malformed is repaired at migration. Backends capture the value **before** the old manifest is destroyed during a reapply and reuse it on the rebuilt manifest.
- `BackendId`: constant identity of the backend — `direct-vrchat-sdk3` for Direct, `modular-avatar` for MA. Stamped at apply; migration restores it from the manifest type when missing.
- `AvatarFingerprint` / `AnimationId`: recorded provenance carried forward across reapplies. Both backends capture these fields from the old manifest before rollback/detach and copy them onto the rebuilt manifest; a fresh apply records empty strings.

## Attached / Detached state derivation

`IntegrationState`: `Unknown = 0`, `Attached = 1`, `Detached = 2`.

- Direct: state is derived from live avatar references — the FX layer and expression parameters/menu currently equal the generated assets.
- MA: state is derived from the ownership check against the recorded hierarchy and the resolved avatar; a missing owned object with no name collision yields `Detached`.
- State is only derived when the migrated manifest recorded `Unknown` (the legacy default). Once recorded, it is preserved verbatim.

## Legacy migration rules

- Version 0 means "no version field": automatically upgraded to 1 at every boundary.
- `LegacyGeneratorAlgorithmVersion` (0) is preserved and never lifted.
- Legacy MA manifests are recognized while detached via `FindRemovalCandidateManifest`: match when the recorded avatar resolves to the given avatar **or** a child under that avatar matches the recorded `IntegrationObjectName`; ownership is then confirmed by the full component/asset check (merge animator, parameters, menu installer, owned asset paths).
- Legacy/Unknown integr state is lazily derived on first use.

## Malformed / null recovery rules

Recoverable (warning `FM-MIG-PARTIAL`, repaired):

- Null project/profile list (defensive empty), null animation/track/key/mapping/generation entries (removed).
- Null payload -> empty payload; null timeline -> default timeline.
- Non-finite / non-positive duration and frame rate -> defaults (`1f`, `60f`).
- Non-finite / negative key time -> `0f`; non-finite key values -> zero.
- Missing/malformed stable IDs -> regenerated.
- Malformed stored avatar fingerprint -> preserved, flagged.
- Empty/unknown settings snapshot -> record retained, flagged.

Unsafe (blocking `FM-MIG-MALFORMED` / `FM-MIG-INTEGRATION-AMBIGUOUS`, untouched):

- Unsupported track kind, unsupported rotation mode.
- Unresolvable avatar ownership.
- A child object with the recorded integration name that is not owned by the manifest.

Nothing is ever silently over-corrected.

## Future-version blocking

- Project `SchemaVersion > ProjectSchemaVersion` -> `FM-MIG-0001`, read-only.
- Profile `SchemaVersion > MappingProfileSchemaVersion` -> `FM-MAP-0003`, read-only.
- Manifests `SchemaVersion > IntegrationManifestVersion` -> `FM-MIG-FUTURE-VERSION`, read-only; neither Apply nor Rollback/Remove proceeds.

Blocking happens before any clone, mutation, Undo, or asset write.

## Migration transaction safety

- In-memory pipeline and profile/state migrations never touch the source asset.
- `ProjectMigrationService.TryMigrateOnLoad` commits only when the pipeline reports `Migrated` with a non-null project: `Undo.RegisterCompleteObjectUndo` -> `EditorJsonUtility.FromJsonOverwrite(ToJson(clone), asset)` -> `SetDirty` -> `SaveAssets`. No commit on refused, failed, or no-op results.
- `MappingProfileMigrationService.TryMigrateOnLoad` commits only when `Allowed && Applied`: same undo + `EditorJsonUtility` round-trip pattern.
- Manifest use-boundary migration is additive in memory; a blocked result returns diagnostics and no operation runs.
- All asset writes happen only at explicit boundaries after successful migration; a failed apply leaves the source assets unchanged.

## Automatic migration boundaries

- `ProjectController.LoadProject` and `ProjectController.SaveProject` -> `ProjectMigrationService.TryMigrateOnLoad`; refused statuses (`FutureSchema`, `Uninitialized`, `VersionGap`, `MigrationFailed`) surface the first blocking diagnostic and abort.
- `DirectVRChatIntegration.Apply` / `Rollback` -> `DirectIntegrationManifestMigration.TryMigrateOnUse`.
- `ModularAvatarIntegrationBackend.Apply` / `Remove` -> `ModularAvatarIntegrationManifestMigration.TryMigrateOnUse` (via `RemoveInternal`).
- Profile load/use -> `MappingProfileMigrationService.TryMigrateOnLoad`.

## Diagnostics

Phase I.2 migration diagnostics (stable codes in `FaceMotionDiagnosticCodes`):

| Code | Severity | Meaning |
|---|---|---|
| `FM-MIG-UPGRADED` | Info | Schema was upgraded to current. |
| `FM-MIG-FUTURE-VERSION` | Error/blocking | Manifest is newer than this tool. |
| `FM-MIG-MALFORMED` | Error/blocking | Unsafe malformed data; left untouched. |
| `FM-MIG-ID-REPAIRED` | Warning | Missing/malformed stable ID regenerated. |
| `FM-MIG-DUPLICATE-ID` | Warning | Duplicate ID replaced; first occurrence kept. |
| `FM-MIG-PARTIAL` | Warning | Recoverable damage repaired (or preserved with a flag). |
| `FM-MIG-INTEGRATION-AMBIGUOUS` | Error/blocking | Avatar/ownership cannot be resolved. |

Pre-existing pipeline codes remain: `FM-MIG-0001` (future project schema), `FM-MIG-0002` (uninitialized), `FM-MIG-0003` (missing step), `FM-MIG-0004` (migration failed), `FM-MIG-0005` (validation failed).

## Test coverage, idempotency, and round-trip guarantees

Permanent EditMode fixtures (`Tests/Editor/Migration/`):

- `MigrationTests` — pipeline mechanics: uninitialized vs. legacy (0 is legacy; -1 is uninitialized and blocking), version gaps, chained steps on detached clones only, future-schema blocking, validation-failure surfacing.
- `LegacyProjectMigrationTests` — project 0->1 migration; service-level asset transactions.
- `MappingProfileMigrationTests` — profile migration and service boundaries.
- `ManifestMigrationTests` — Direct and MA use-boundary migration, reapply preservation, detached reconnection, future-schema blocking, ambiguity blocking.

Guarantees exercised by the tests:

- Idempotency: a second pass over migrated output is a content no-op; a second service load does not rewrite the asset.
- Round trip: migrated content is identical after serialize/deserialize over `EditorJsonUtility`.
- Source immutability: the source object is never mutated; failures return no asset writes.
- First-occurrence canonical ID preservation across project, animation, track, key, profile, entry, and generation data.

Baseline at the time of writing: migration fixtures 73/73 PASS, full EditMode suite 504/504 PASS, compile errors 0.