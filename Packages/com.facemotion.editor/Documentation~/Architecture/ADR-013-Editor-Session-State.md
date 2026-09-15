# ADR-013: Editor Session State

- Status: Accepted
- Date: 2026-09-14

## Context

`FaceMotionProject` is persistent authored data. Window selection, playback position,
timeline geometry, clipboard content, and live-avatar caches are editor interaction state.
Persisting those details in a project would make shared projects carry one user's transient
UI state and would serialize scene-object-dependent information.

## Decision

`FaceMotionEditorSession` owns the active project and avatar context, diagnostics cache,
and all transient editing state. It does not serialize state into `FaceMotionProject`.

- `SelectedAnimationId`, `SelectedTrackId`, and `TimelineSelection` identify authored data
  by stable IDs. `TimelineSelection` stores selected key IDs, never list indexes.
- `TimelineViewState` owns `CurrentTime`, `Zoom`, `ScrollTime`, `SnapEnabled`, playback,
  hover, and drag mode.
- `TimelineClipboard` owns copied key payloads and relative times only in editor memory.
- `AvatarIndexDirty` records hierarchy staleness. The live `AvatarIndex`, object cache, and
  candidate snapshot are session-only and are rebuilt explicitly.

Stable IDs prevent selection from changing meaning when key lists sort, tracks are removed,
or Undo/Redo restores a prior list shape. `RefreshAfterUndo` revalidates animation, track,
and key selections and prunes IDs that no longer exist.

### Domain reload

`FaceMotionSessionStateStore` uses `EditorPrefs`, not project data, to restore only project
asset path, selected animation ID, current time, and zoom. Selected track/key IDs, scroll,
snap, clipboard, avatar cache, and dirty state are deliberately reconstructed or reset on
reload. This keeps reload persistence small and avoids restoring stale scene references.

## Consequences

- Projects remain portable and deterministic regardless of the last editor window state.
- UI code can react to `FaceMotionEditorSession.Changed` without adding serialized fields to
  the domain model.
- Consumers must validate session selection after project mutation and Undo/Redo.
