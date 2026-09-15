# ADR-020: Preview Lifecycle and Cleanup

- Status: Accepted
- Date: 2026-09-14

Preview cleanup is idempotent. Window disable, assembly reload, play-mode transitions, and
avatar replacement dispose scene apply before preview clone/render resources. Preview drawing
pairs `BeginPreview` and `EndPreview` through `try/finally`.
