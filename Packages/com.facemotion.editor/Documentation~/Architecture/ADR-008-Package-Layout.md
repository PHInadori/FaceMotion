# ADR-008: Package Layout

- Status: Accepted for development; package ID retained for first public release
- Date: 2026-09-14

## Context

FaceMotion must be installable through VPM clients and must never write generated user assets into PackageCache or an immutable installed package.

## Decision

Develop as an embedded local package under `Packages/com.facemotion.editor`. Keep source, tests, documentation, and future samples in the package. Store user projects, presets, clips, controllers, menus, parameters, and manifests under `Assets/FaceMotion` or a user-selected Assets path.

The package ID is `com.facemotion.editor`. It was retained for the first public release after publisher and repository naming were decided.

## Consequences

The package is directly editable during development and VPM-ready in shape. Renaming the package after assets are published is disruptive, so the final ID must be approved before the first public artifact.
