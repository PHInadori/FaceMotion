# Release Checklist

Target release: `v0.7.0`

Development evidence already exists for the current candidate (focused / affected / full EditMode suites, package validation, fresh-project FIRST/FINAL, and the schema migration + asset-name retest). None of it counts as final release-candidate validation: every item below must be re-run or completed against the final release-preparation diff in M7-D, and nothing may be checked off early.

## Before Tag

- [x] Final MA-installed EditMode validation after the release-preparation diff.
- [x] Isolated no-MA EditMode validation.
- [x] Final fresh-project FIRST/FINAL import validation.
- [x] v0.6.0 → v0.7.0 upgrade compatibility smoke: open a project saved by v0.6.0, confirm it upgrades to project schema 2 with data and asset name preserved.
- [x] Unity manual smoke test of the release candidate.
- [x] VRChat SDK Build & Test.
- [x] Resolve the README real-screenshot requirement (see below).
- [x] `Tools/ci/Validate-Package.ps1` passes.
- [x] Generate the v0.7.0 release ZIP and run `Tools/ci/Validate-VpmArtifact.ps1`.
- [x] Confirm documentation links, version consistency, and CHANGELOG.
- [ ] Confirm the Git worktree is clean and GitHub CI is green on the pushed candidate.

## README Screenshot Requirement

- [x] README requirement for an actual screen screenshot of FaceMotion: satisfied by the real FaceMotion editor capture at `Documentation~/Images/FaceMotion-0.7.0-overview.png`, linked from `README.md`. The image is an actual screenshot of the running editor (no mock or generated image), and it remains a requirement that any future replacement is also a real UI capture.

## Before Release

- [x] Confirm artifact filename and SHA-256 from `dist/release-info.json`.
- [ ] Create and verify tag `v0.7.0`.
- [ ] Create the GitHub Release with the validated ZIP and `release-info.json`.
- [ ] Update the VPM listing repository and publish its Pages build.

## After Release

- [ ] Install through VCC or ALCOM in a clean project (post-release install verification).
- [ ] Run a clean install and VRChat Build & Test check.
- [ ] Confirm documentation links and the GitHub release page.
- [ ] Confirm VPM index visibility and package installation.
