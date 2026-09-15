# ADR-015: Timeline Editing and Collision Policy

- Status: Accepted
- Date: 2026-09-14

## Context

Timeline editing must preserve authored data through add, update, drag, multi-move, and
paste. Duplicate times can otherwise cause ambiguous curve behavior or tempt the editor to
discard data silently.

## Decision

- Add Key creates a fresh stable key ID. Adding at an existing same-time key updates that
  key's value rather than inserting another same-time key.
- Key moves, including drag and multi-move, use the shared `KeyMovePlanner` and
  `TrackKeyMover`. Keys are sorted after their times change.
- Snap uses the timeline frame rate when enabled. Times are clamped to timeline duration.
- A move collision is resolved by nudging to a nearby free frame; it never merges or deletes
  a key.
- Paste stores relative times and creates fresh key IDs. A target-time collision is skipped
  and reported as a diagnostic. A missing target track or kind mismatch rejects the paste
  safely.

The governing rule is: **the editor must not delete keys automatically to resolve a
collision.** Data loss requires an explicit delete operation.

## Consequences

- Reordering is deterministic because every mutation re-sorts keys by time.
- Users retain every selected key during a collision, even when the final time is nudged.
- Paste behavior is predictable: valid, non-colliding items are pasted; skipped collisions
  are visible to the user; missing targets do not create partial data.
