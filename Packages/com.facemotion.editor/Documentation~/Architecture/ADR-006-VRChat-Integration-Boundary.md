# ADR-006: VRChat Integration Boundary

- Status: Accepted
- Date: 2026-09-14

## Context

Direct dependencies on VRCSDK types throughout the domain would prevent isolated tests and make integration changes unsafe.

## Decision

Keep all VRCSDK types in `FaceMotion.Editor.VRChat`. Use SDK-neutral requests and diagnostics across the Core boundary. Separate avatar adaptation, planning, validation, backend execution, and manifest persistence. Support direct and future Modular Avatar backends behind the backend boundary.

## Consequences

Core cannot inspect or mutate `VRCAvatarDescriptor`, controllers, parameters, or menus. The VRChat assembly owns conversion and asset semantics. Copy-on-write is the provisional direct-backend default and must be validated in Phase G rather than assumed safe.
