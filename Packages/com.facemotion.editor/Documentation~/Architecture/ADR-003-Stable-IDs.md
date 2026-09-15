# ADR-003: Stable IDs

- Status: Accepted
- Date: 2026-09-14

## Context

Selection, copy/paste, provenance, integration ownership, and migration cannot safely depend on names or list positions.

## Decision

Use opaque lowercase 32-character GUID strings for project, animation, track, key, generation, preset, and integration IDs. Duplicate and paste operations create new identities; transactional copies and migration preserve identities.

## Consequences

Serialized files are larger, but references remain stable across reorder and rename. Load validation must detect empty, malformed, and duplicate IDs. Public APIs must not expose semantic behavior based on GUID formatting beyond validation and ordinal comparison.
