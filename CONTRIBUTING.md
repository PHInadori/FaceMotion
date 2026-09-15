# Contributing

## Environment

- Unity `2022.3.22f1` only. Do not open or convert the project in Unity 6.
- VRChat SDK - Avatars remains pinned to `3.10.5`.
- Modular Avatar is optional. Changes must preserve the no-MA compile boundary.

## Before a PR

1. Do not commit `Library`, `Temp`, `Logs`, `TestResults`, IDE files, generated test assets, or third-party avatar assets.
2. Keep package changes under `Packages/com.facemotion.editor` and use repo-relative tooling under `Tools/`.
3. Run the applicable EditMode tests. The established invocation uses `VRC_TEST_PROTOCOL=0` and omits `-quit`.
4. Run `Tools/ci/Validate-Package.ps1` and artifact validation when changing package/release metadata.
5. Describe behavior changes, migration impact, and MA/no-MA impact in the PR.

## Style and Compatibility

Prefer small changes, preserve Japanese-first UI text, and add tests for changed behavior. The external public API is not stable during 0.x. Serialized data compatibility is a separate contract; see `Packages/com.facemotion.editor/Documentation~/Compatibility.md`.
