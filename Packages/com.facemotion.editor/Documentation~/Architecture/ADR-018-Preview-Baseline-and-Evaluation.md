# ADR-018: Preview Baseline and Evaluation

- Status: Accepted
- Date: 2026-09-14

`PreviewBaseline` captures original blend-shape and local transform values lazily. Every
evaluation restores that baseline before applying enabled tracks. `PreviewMotionApplier` uses
only `CanonicalMotionEvaluator.Instance` for float, position, rotation, and scale values.
Missing bindings are skipped without changing project data.
