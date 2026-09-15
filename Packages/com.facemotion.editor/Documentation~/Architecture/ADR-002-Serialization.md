# ADR-002: Serialization

- Status: Accepted
- Date: 2026-09-14

## Context

Projects must survive editor restarts and future schema changes while supporting Unity Undo.

## Decision

Store one `FaceMotionProject` ScriptableObject with inline serializable animation, timeline, track, key, mapping-reference, and generation-registry data. Presets and integration manifests are separate root assets. Scene objects and resolved component caches are never persisted in the project.

## Consequences

Project edits can record one root object. Track and key CRUD avoid AssetDatabase operations. Large projects make complete-object Undo snapshots more expensive, which must be measured before optimizing the persistence boundary.
