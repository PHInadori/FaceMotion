# ADR-010: Avatar Index and Binding Identity

- Status: Accepted
- Date: 2026-09-14

## Context

Phase B needs an SDK-neutral identity for the avatar elements that a timeline track or a
mapping entry can bind to, plus a runtime hierarchy snapshot that resolution, validation,
and future preview/export can all share. VRChat avatars are unstable: instances change,
blend shape indices depend on import time, and paths can collide when hierarchy names
repeat.

## Decision

### Binding identity is exact and path-based

- A blend shape binding is the pair **RendererPath + BlendShapeName**. The renderer's
  blend shape index is runtime cache data and is never persistent identity.
- A transform binding is **TransformPath**. Both paths are relative to the avatar root
  (`VRCAvatarDescriptor.gameObject`).
- Orientation hints (renderer name, mesh name, transform name) are non-authoritative
  assistance for diagnostics. They never participate in identity, equality, or hashing.
- Matching is exact and ordinal case-sensitive. Fuzzy, partial, or keyword matching is
  deliberately out of Phase B; it belongs to the future Preset phase.

### AvatarIndex is an immutable, SDK-neutral snapshot

`AvatarIndex` lives in Core (`FaceMotion.Avatar`) and is rebuilt when the avatar changes,
never rescanned per fame. It stores:

- `TransformIndexEntry`: relative path (`""` for the root), name, depth, parent path.
- `RendererIndexEntry`: relative path, renderer name, `HasMesh`, `BlendShapeCount`, mesh
  name (diagnostic only).
- `BlendShapeIndexEntry`: renderer path, renderer name, blend shape name, current index,
  current weight (both runtime-only).

No Unity object reference is stored in the index. Live object resolution is the job of the
Unity-side `UnityAvatarObjectCache`. The index is a runtime editor cache and is never
persisted into a FaceMotionProject.

### Unified relative-path calculation

`RelativePathUtility.GetRelativePath` is the single source of truth and is shared by
scanning, binding, validation, and future export/preview. Root = `""`, separator = `/`.
Inactive descendants are scanned (`GetComponentsInChildren(..., true)`); renderers whose
shared mesh is null, and meshes with no blend shapes, are safe and still listed.

### Ambiguity is explicit, never guessed

- Duplicate transform sibling paths are detected at scan time and reported as a blocking
  diagnostic (`FM-AVT-0007`), and the identity is resolved as `Ambiguous` anywhere a
  lookup happens. The resolver and the object cache return `false`/`Ambiguous` instead of
  picking an arbitrary match.
- The same blend shape name on different renderers is not ambiguous: identity is
  `RendererPath + BlendShapeName`, so `Face/Smile` and `Cheek/Smile` are distinct.
- A blend shape name repeated within one mesh is reported with a warning diagnostic;
  its identity stays ambiguous until the mesh is fixed.

## Consequences

- Binding validation is deterministic and never silent: every binding resolves to
  `Unique`, `Ambiguous`, or a specific "missing" status.
- Because identity is exact, manual repair is straightforward: the diagnostic names the
  exact path/name that no longer exists.
- The scanner, fingerprint, resolver, and object cache all rotate around one identity
  model, so a binding validated against the index resolves to the same element the cache
  would return.