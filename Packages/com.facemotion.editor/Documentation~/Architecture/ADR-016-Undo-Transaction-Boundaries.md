# ADR-016: Undo Transaction Boundaries

- Status: Accepted
- Date: 2026-09-14

## Context

Timeline edits frequently affect many keys and emit repeated IMGUI events. Recording every
intermediate drag update as an Undo action makes Undo unusable and risks partial mutation.

## Decision

One user action maps to one Undo group.

- `UICommandRunner.Run` calls `Undo.IncrementCurrentGroup` at a command boundary.
- `RunBatch` validates all commands first, then executes multi-move in one transaction.
- Paste and Delete Selected are each one command/transaction.
- `DragUndoScope` registers one complete-object snapshot when the drag begins, mutates that
  record during drag updates, and flushes it on mouse-up.
- Escape/cancel reverts the isolated drag group rather than leaving previewed key positions
  authored.
- Dirty state is applied at command commit or drag commit, never every `OnGUI` pass.
- `Undo.undoRedoPerformed` invokes `FaceMotionEditorSession.RefreshAfterUndo`, which rebuilds
  diagnostics and validates stable-ID selection.

Inspector Apply builds Move, Value, and Interpolation commands and submits them through
`RunBatch`, so one Apply is one Undo transaction. Inspector Revert reloads the temporary
buffer and does not mutate the project.

## Consequences

- Drag, multi-move, paste, and delete behave as coherent Undo operations.
- Cancellation is non-destructive.
- Any future composite UI action must use `RunBatch` or an equivalent single transaction.
