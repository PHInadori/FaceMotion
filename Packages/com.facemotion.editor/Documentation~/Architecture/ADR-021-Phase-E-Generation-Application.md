# ADR-021: Phase E Generation Application

## Status

Historical, removed from the current product. Accepted 2026-09-14; preset and random-generation authoring was subsequently removed. Serialized models remain for compatibility and migration only.

## Decision

Phase E.2 presents eight built-ins, unique-only mapping candidates, blink, random blend-shape, and random rotation in the editor window. A candidate must be explicitly confirmed into a persisted `AvatarMappingProfile` before a logical built-in can resolve it. A generator first creates the SDK-neutral `GeneratedMotion` intermediate model. The UI applies that model only after an explicit button click through `GeneratedMotionUndoService`, making each application one Unity Undo transaction.

Logical built-ins resolve only through confirmed profile bindings. An absent or ambiguous target disables that built-in rather than guessing a binding. Generated keys never overwrite a same-time key; manual collisions are retained and reported. The generation registry stores canonical JSON settings and its SHA-256 digest, allowing `GeneratedMotionRegenerator` to reproduce pure motion.

## Consequences

Generation remains avatar-scoped at application time but scene-safe: it edits only the selected FaceMotion project. Preview and scene-apply retain their Phase D lifecycle. Animation export, FX controllers, parameters, menus, and Modular Avatar integration remain explicitly outside Phase E.
