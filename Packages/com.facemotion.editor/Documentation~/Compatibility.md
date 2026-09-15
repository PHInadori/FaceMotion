# FaceMotion Persisted Data Compatibility

This document describes the compatibility contract for every FaceMotion-persisted asset. It is written for users, reviewers, and release staff: what the tool reads, what it upgrades automatically, what it refuses, and what to expect when older data is opened.

The developer-oriented technical counterpart is [Schema-Migration.md](Schema-Migration.md).

## Supported persisted data

The tool persists the following ScriptableObject assets:

| Asset | Role |
|---|---|
| `FaceMotionProject` | Root project asset: animation list, timelines, tracks, keys, and the project-level generation registry. |
| `AvatarMappingProfile` | Avatar-specific binding table (blend shape / transform bindings), separately stored from projects. |
| `DirectIntegrationManifest` | Ownership and rollback data for a direct (copy-on-write) VRChat integration. |
| `ModularAvatarIntegrationManifest` | Ownership and rollback data for a Modular Avatar integration. |

Presets and fingerprints use their own version fields; this document covers the four assets above.

## Current schema version

Every persisted asset carries an integer schema version:

| Asset | Current schema version |
|---|---|
| `FaceMotionProject` | 1 |
| `AvatarMappingProfile` | 1 |
| `DirectIntegrationManifest` | 1 |
| `ModularAvatarIntegrationManifest` | 1 |

These versions are the ones this tool understands. A schema number changes only when the persisted meaning or shape of the data changes.

## Minimum supported schema version

The minimum supported schema version is **0** (legacy). Version 0 is the equivalent of "the version field was not present," i.e. data written before schema versioning existed. The tool recognizes version 0 automatically and upgrades it in place.

A project whose version is **uninitialized (a negative number)** is not the same as legacy data and is refused (see "Read-only / blocking policy" below).

## Future-schema behavior

Data stamped with a schema **greater than current** was written by a newer version of the tool. The tool does not understand it and must not guess.

- The asset is **blocked** (read-only for migration purposes).
- It is **never modified, rewritten, normalized, or repaired**.
- The operation that triggered the load is refused with a blocking `FM-MIG-FUTURE-VERSION` diagnostic; the user is told to upgrade the tool.
- The same refusal applies at every automatic migration boundary (project load/save, manifest use, profile load).

This guarantees a downgrade can never partially rewrite data it does not understand.

## Automatic migration policy

Migration runs automatically at explicit, well-defined boundaries:

| Boundary | What is migrated |
|---|---|
| Project opened in the editor | `FaceMotionProject` upgraded from legacy, then committed to the asset. |
| Project saved | The active project is re-checked just before save. |
| Direct integration Apply / Rollback | `DirectIntegrationManifest` upgraded, then used. |
| Modular Avatar Apply / Remove | `ModularAvatarIntegrationManifest` upgraded, then used. |
| Mapping profile load/use | `AvatarMappingProfile` upgraded, then committed to the asset. |

Rules that always hold:

- Migration is **non-destructive**: user content is never deleted or guessed.
- Migration is **deterministic**: the same input produces the same output.
- Migration is **idempotent**: running it again on already-migrated data is a no-op.
- A successful migration is committed to the asset; an unsafe one is not.
- The source asset is never rewritten by an in-memory check that failed or was only a content no-op.

## Stable ID preservation and repair

Stable IDs (project, animation, track, key, generation, mapping entry, integration) are 32-character lowercase GUID strings.

- A **valid existing ID is never changed**, during migration or anywhere else.
- A **missing or malformed ID** is regenerated with a stable ID and reported (`FM-MIG-ID-REPAIRED`).
- **Duplicate IDs** are resolved deterministically: the first occurrence in list order keeps its ID, later duplicates receive new IDs and are reported (`FM-MIG-DUPLICATE-ID`).
- IDs are opaque and are compared ordinally; they carry no meaning.

## Malformed and ambiguous data handling

Malformed data is handled one of two ways:

- **Recoverable** problems are repaired and reported as warnings (`FM-MIG-PARTIAL`). Examples: null timeline replaced with a default timeline, null track/key/mapping entries removed, non-finite or non-positive duration/frame rate reset to safe defaults, non-finite key time/value reset to zero, null payload replaced with an empty payload.
- **Unsafe** problems are blocking (`FM-MIG-MALFORMED`). The affected data is **left untouched** exactly as found. Examples: a track kind that is not supported, a rotation mode that is not supported. Nothing is silently corrected or destroyed.

Ambiguous ownership is also blocking (`FM-MIG-INTEGRATION-AMBIGUOUS`): when an integration's avatar cannot be resolved, or an object with the FaceMotion integration name exists but is not owned by the manifest, the operation is refused until the user resolves the conflict.

The guiding principle: never silently over-correct. If the tool cannot be sure, it refuses and explains.

## Read-only / blocking policy

A load or use is refused (nothing is written) when:

| Condition | Diagnostic |
|---|---|
| Future schema (> current) for any asset | `FM-MIG-FUTURE-VERSION` |
| Uninitialized (negative) project or profile schema | `FM-MIG-0002` / `FM-MAP-0004` |
| A required migration step is missing | `FM-MIG-0003` |
| The object is null / missing | `FM-MIG-0004` / `FM-G-MANIFEST` / `FM-H-MA-MANIFEST` |
| Malformed unsafe data | `FM-MIG-MALFORMED` |
| Ambiguous integration ownership / unresolvable avatar | `FM-MIG-INTEGRATION-AMBIGUOUS` |
| Migrated data fails validation | `FM-MIG-0005` |

When refused, the source asset keeps its original schema and content.

## Direct integration compatibility

A `DirectIntegrationManifest` records the original and generated FX controller, expression parameters, and expression menu, plus the generated parameter name and owned asset paths.

- An existing integration is found on the avatar and reused across reapplies.
- The **integration ID is generated once and preserved across reapplies**; a rollback ends the integration, and a subsequent fresh apply starts a new ID.
- Reapplying migrates the legacy manifest, rolls back the previous generated assets, then rebuilds a fresh copy-on-write integration carrying over the recorded `AnimationId` and `AvatarFingerprint`.
- Rollback restores the original FX/parameters/menu and deletes only FaceMotion-generated files inside the manifest folder. Foreign content in the generated folder is retained and reported.
- A manifest whose avatar reference cannot be resolved is blocked (`FM-MIG-INTEGRATION-AMBIGUOUS`).

## Modular Avatar Attached / Detached compatibility

The MA integration state is recorded on the manifest as `Attached` or `Detached`:

- **Attached**: the avatar currently has the owned integration object (merge animator, parameters, menu installer) resolved from the manifest.
- **Detached**: the generated assets and manifest are retained, but the integration object is gone (for example the user removed the generated hierarchy).
- **Unknown** is the legacy default and is resolved lazily by the migration services from the live hierarchy.

A **recorded state is never recomputed** from the live hierarchy just because a migration ran; runtime ownership resolution governs each operation. The MA backend recognizes and manages **both** attached and detached manifests: a detached integration is reconnected on the next apply, reusing its retained generated folder and assets (`FM-H-MA-DETACHED`), while a remove or apply on a manifest whose owned object is missing blocks rather than deleting.

## Legacy MA Detached recognition

Legacy (version 0) MA manifests were written before the attrs/state fields existed. The tool recognizes a detached legacy integration by:

1. Resolving the recorded avatar (live reference, then `AvatarGlobalId`),
2. Falling back to the recorded `IntegrationObjectName` under that avatar,
3. Requiring the full ownership check (merge animator, parameters, menu installer, owned assets) before treating an object as owned.

A legacy detached manifest is therefore still found for Remove/Apply, and a reapply reconnects it using its retained generated assets instead of treating it as a foreign, unmanaged integration.

## Migration idempotency

Migration is built to be safe to run repeatedly:

- Migrating already-current data returns a **no-op** (`NoMigrationNeeded` for projects; `Applied = false` for profiles).
- A second load of an already-migrated asset does **not** rewrite the asset.
- Content (IDs, values, provenance, snapshots) is identical after any number of passes.

## Serialization roundtrip expectations

- Persisted data survives a serialize/deserialize round trip with all IDs, provenance, and content intact (the services round-trip through `EditorJsonUtility`).
- A success commit is written through Undo + `SetDirty` + `SaveAssets` at the explicit boundary only.
- After a legacy project is migrated and committed, a subsequent load is a no-op with identical content — the migrated data is stable across sessions.

## Scope of this contract

This contract describes **current** behavior only. Unreleased or planned schema changes, package version bumps, and future-phase features are intentionally not described here. See the technical counterpart [Schema-Migration.md](Schema-Migration.md) for implementation details, and `Architecture/README.md` for the overall architecture baseline.