# ADR-001: Track Composition

- Status: Accepted
- Date: 2026-09-14

## Context

The timeline must keep an ordered heterogeneous list of blend shape, position, rotation, and scale tracks in Unity 2022 serialization.

## Decision

Use a serializable track envelope with common metadata, an explicitly numbered track kind, and explicit blend shape and transform payload fields. Validation enforces exactly one active payload. Do not use inheritance or `SerializeReference` as the primary project format.

## Consequences

Unity-native serialization, root-object Undo, cloning, and migration remain predictable. Adding a track kind requires an additive payload and schema migration. Some inactive fields exist in YAML, but the compatibility cost is lower than managed-reference type identity risk.
