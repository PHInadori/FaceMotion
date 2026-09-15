# ADR-007: Test Strategy

- Status: Accepted
- Date: 2026-09-14

## Context

Temporary validation scripts do not protect serialization, migration, Undo, or third-party integration across releases.

## Decision

Adopt Unity Test Framework 1.1.33 and permanent EditMode package tests. Tests are organized by architecture phase and use committed migration and integration fixtures when those formats exist.

## Consequences

Phase A cannot be complete without domain, serialization, migration, Undo, and evaluator tests. Later preview/export tests compare sampled Unity results against the canonical evaluator. VRChat SDK API assumptions are represented by contract smoke tests.
