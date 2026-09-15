# ADR-004: Canonical Evaluator

- Status: Accepted
- Date: 2026-09-14

## Context

The old implementation used different transform interpolation for preview and exported clips.

## Decision

Use one stateless typed evaluator for float, position, scale, and rotation. Preview and generators call it directly. Export samples it on the configured frame grid before writing curves. Initial rotation mode is shortest-path quaternion interpolation; 360-degree spin is unsupported.

## Consequences

Preview and export can be compared at frame sample times. Exported curves may contain more keys until a separately tested simplification stage is added. Future Euler-continuous rotation is a new explicit mode, not an implicit behavior change.
