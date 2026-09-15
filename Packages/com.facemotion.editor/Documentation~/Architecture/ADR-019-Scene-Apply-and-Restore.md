# ADR-019: Scene Apply and Restore

- Status: Accepted
- Date: 2026-09-14

Scene application is explicit and separate from preview. `SceneApplySession` snapshots source
values once, applies canonical evaluation while active, and restores on disable, avatar
replacement, window cleanup, reload, or play-mode transition. It does not create per-frame
Undo records.
