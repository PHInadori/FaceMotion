# ADR-011: Avatar Fingerprint

- Status: Accepted
- Date: 2026-09-14

## Context

A mapping profile is built against one avatar structure. When the avatar changes (new
blend shapes, renamed bones, swapped meshes) the profile goes stale, but detecting stale
still safely requires a cheap, deterministic comparison that survives editor restarts and
does not depend on Unity object identity.

## Decision

### Fingerprint contents

The fingerprint is a canonical, deterministic hash of the avatar **structure only**:

- For every transform: `T|<relativePath>`
- For every renderer: `R|<relativePath>`
- For every blend shape: `B|<rendererPath>|<blendShapeName>`

Excluded on purpose:

- The avatar root's display name (two identical avatars with different names match).
- Unity instance IDs, scene paths, or asset file IDs.
- Blend shape weights and materials (harmless cosmetic state).
- Renderer/mesh display names not already implied by identity lines.

### Canonical input format

Newline-joined, ordinally sorted (`StringComparer.Ordinal`), UTF-8 encoded, then hashed
with SHA-256. The digest is expressed as 64 lowercase hex characters. This contract is
documented so that any implementation can reproduce the hash:

```text
T|<path>
R|<rendererPath>
B|<rendererPath>|<blendShapeName>
```

### Versioning

- `AvatarFingerprintAlgorithmVersion = 1` protects the hash algorithm and line format.
- `AvatarFingerprintFormatVersion = 1` protects the serialized `AvatarFingerprint` DTO.
- `AvatarFingerprint` is a `[Serializable]` DTO with `IsValid` and reflexive `EqualsValue`,
  and is cloned rather than shared between profile and scanner.

### Where the fingerprint lives

- The fingerprint is stored in the `AvatarMappingProfile` (`AvatarFingerprint`), not in
  the `FaceMotionProject`.
- The Unity scanner computes it with `AvatarFingerprintBuilder.Compute` inside a try/catch;
  a computation failure yields a blocking `FM-AVT-0010` diagnostic, not an exception.
- `System.GetHashCode` is never used for the fingerprint hash.

## Consequences

- Two scans of the identical structure produce byte-equal fingerprints, so the evaluator
  knows the profile is fresh (`FingerprintMatches`).
- A structural change produces a different fingerprint but never voids the profile; the
  evaluator re-validates each binding individually (see ADR-012).
- Japanese/unicode names work because the canonical bytes are UTF-8 and sorting is ordinal
  and culture-independent.