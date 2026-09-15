# Phase C Implementation Inventory

- Date reviewed: 2026-09-14
- Basis: implementation under `Editor/UI`, not intended design.

| Completion condition | Status | Code evidence / note |
| --- | --- | --- |
| Tools > FaceMotion window | Implemented | `FaceMotionWindow.OpenWindow` menu item |
| Project create/select/save | Implemented | `ProjectPanel`, `ProjectController` |
| VRCAvatarDescriptor select | Implemented | `AvatarPanel.ShowSceneSelector` |
| Avatar Index build/rebuild | Implemented | `AvatarController.BuildAvatar` / `RebuildIndex` |
| Animation CRUD | Implemented | `AnimationController`, `AnimationListPanel` |
| Duration / FPS / Loop | Implemented | `AnimationListPanel` buffers inputs and commits through `AnimationController` |
| Track list | Implemented | `TrackListPanel` |
| BlendShape track | Implemented | `TrackController.AddBlendShapeTrack` |
| Position / Rotation / Scale tracks | Implemented | Transform kind popup in `TrackListPanel` |
| Missing binding display | Implemented | Track `!`, timeline missing marker, diagnostics |
| Timeline / ruler / cursor | Implemented | `TimelineView`, `TimelineRenderer` |
| Zoom / scroll / fit | Implemented | Geometry + ctrl-wheel, middle pan, toolbar fit |
| Snap | Implemented | Timeline header toggle binds editor-only `TimelineViewState.SnapEnabled` |
| Key add / delete | Implemented | Inspector and input handler/controller |
| Key selection / multi-select | Implemented | click, ctrl/cmd click, ctrl/cmd+A |
| Drag / reorder / multi-move | Implemented | `KeyMovePlanner`, `TrackKeyMover`, `DragUndoScope` |
| Copy / paste | Implemented | `TimelineClipboard`, `PasteKeysCommand` |
| Inspector / interpolation | Implemented | Buffered fields apply through one `RunBatch` Undo transaction; Revert reloads the buffer |
| Undo / redo | Implemented | command groups, drag group, undo-redo refresh |
| Diagnostics | Implemented | session aggregation and `DiagnosticsPanel` |
| Avatar dirty / rebuild | Implemented | hierarchy dirty flag and explicit rebuild |
| Playback cursor | Implemented | `EditorApplication.update` advances `CurrentTime` |
| Candidate selection UI | Implemented | `TrackListPanel` filters the scan snapshot and uses candidate identities; manual entry is advanced fallback |

## Candidate selection review

`TrackListPanel` consumes the scan-owned candidate snapshot. It refreshes filter results only
when the search string or snapshot instance changes, shows `RendererPath / BlendShapeName`
or full transform paths, disables selection safely when no avatar is active, and passes the
candidate's exact binding identity to `TrackController`. Manual path entry remains an
advanced fallback rather than the normal flow.
