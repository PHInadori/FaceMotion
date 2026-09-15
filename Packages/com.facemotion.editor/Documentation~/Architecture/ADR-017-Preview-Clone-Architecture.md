# ADR-017: Preview Clone Architecture

- Status: Accepted
- Date: 2026-09-14

## Decision

Preview uses a hidden `Object.Instantiate` clone owned by `PreviewSession`, never the scene
avatar. `PreviewRenderUtility` owns drawing through `AddSingleGO`, and clone/cache objects
are disposed with the session. Preview data does not alter `FaceMotionProject`.

## Consequences

Source scene objects are not used as preview targets. Clone behavior components are disabled
and hierarchy objects use `HideAndDontSave`; failure destroys an incomplete clone.
