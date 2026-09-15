# Phase E.3 Implementation Inventory

## Implemented

| Requirement | Evidence |
| --- | --- |
| Eight fixed multi-target avatar-neutral built-ins with required/optional target contracts and 0.00/0.10/0.25 second pulses | `FaceMotionBuiltins`, `PhaseEGenerators.CreatePreset`, and `GenerationPanel` |
| Unique-only candidate suggestions with explicit persisted confirmation | `LogicalMappingSuggestions`, `LogicalMappingConfirmation`, and `AvatarMappingProfile` |
| Explicit UI application of built-ins, blink, random blend-shape, and random rotation | `GenerationPanel` |
| Seeded deterministic generated motion | `PhaseEGenerators` |
| Generated-motion intermediate model, provenance, single Undo application, and conservative collision merge | `GeneratedMotion`, `GeneratedMotionUndoService`, `GeneratedMotionApplier` |
| Settings snapshot, hash, and pure regeneration | `GenerationSettingsSnapshot` and `GeneratedMotionRegenerator` |
| EditMode mapping, snapshot, merge, undo, blink/random, optional-overlay, and UI-reachability contracts | `PhaseEGenerationTests`, `PhaseE3PresetContractTests`, and `Testing/Phase-E-Manual-Checklist.md` |
| Phase E design decision | `ADR-021-Phase-E-Generation-Application.md` |

## Not Implemented By Design

| Requirement | Status |
| --- | --- |
| Animation clip export, FX layers, parameters, menus, and Modular Avatar assets | Phase F work; excluded from Phase E.1. |
