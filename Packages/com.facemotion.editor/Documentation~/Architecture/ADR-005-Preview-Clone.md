# ADR-005: Preview Clone

- Status: Accepted
- Date: 2026-09-14

## Context

Facial motion must be previewed without leaving scene or prefab changes and without allowing independent preview modes to fight over values.

## Decision

Create one hidden, non-saving clone of `VRCAvatarDescriptor.gameObject` owned by a disposable preview session and registered with `PreviewRenderUtility`. Timeline, preset, and random preview are mutually exclusive modes using the canonical evaluator. Scene-avatar application is a separate explicit opt-in mode.

## Consequences

The preview owner must disable unsafe clone behaviors, cache bindings, restore scene values, and clean up on avatar replacement, window disable, assembly reload, and play mode changes. Clone lifecycle and script side effects require dedicated Phase D tests.
