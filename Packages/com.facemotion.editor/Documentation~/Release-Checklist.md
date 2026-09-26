# Release Checklist

## Before Tag

- [ ] Full MA EditMode tests are green.
- [ ] Isolated no-MA EditMode tests are green.
- [ ] `Tools/ci/Validate-Package.ps1` passes.
- [ ] Generate the release ZIP and run `Tools/ci/Validate-VpmArtifact.ps1`.
- [ ] Confirm documentation links, version consistency, and CHANGELOG.
- [ ] Confirm the Git worktree is clean and GitHub CI is green.

## Before Release

- [ ] Create and verify tag `v0.6.0`.
- [ ] Confirm artifact filename and SHA-256 from `dist/release-info.json`.
- [ ] Create the GitHub Release with the validated ZIP and `release-info.json`.
- [ ] Update the VPM listing repository and publish its Pages build.

## After Release

- [ ] Install through VCC or ALCOM in a clean project.
- [ ] Run a clean install and VRChat Build & Test check.
- [ ] Confirm documentation links and the GitHub release page.
- [ ] Confirm VPM index visibility and package installation.
