# CI / Automated Validation

## Scope

`.github/workflows/ci.yml` validates pull requests, pushes to `main`, and manual
dispatches. It is independent from `vpm-release.yml`: CI validates source and
artifacts; the release workflow only creates a GitHub Release after a version tag.

## Jobs

| Job | Runner | Checks |
| --- | --- | --- |
| `package-validation` | Ubuntu | package metadata, required/forbidden content, schema constants, listing metadata, license, changelog, and local Markdown links |
| `vpm-artifact-validation` | Windows | runs `Tools/create-vpm-release.ps1`, validates ZIP layout/content/extraction/SHA-256/release metadata |
| `unity-editmode-tests` | Ubuntu + GameCI | Unity 2022.3.22f1, full EditMode suite with Modular Avatar installed |
| `no-ma-compile-smoke` | Ubuntu + GameCI | disposable project without MA/NDMF; Direct/main tests compile and MA assemblies remain gated out |

The Unity jobs use `VRC_TEST_PROTOCOL=0`, matching the local test invocation. Do
not add `-quit` to the GameCI test invocation; the test runner manages editor exit.

## Unity License

The Unity jobs use GameCI's documented Unity Personal activation and require three
repository secrets: `UNITY_LICENSE` (the contents of the manually activated `.ulf`
file), `UNITY_EMAIL`, and `UNITY_PASSWORD` (the Unity account that owns that
license). The workflow does not use a paid serial. GitHub does not expose secrets to
pull requests from forks, so Unity jobs are skipped for fork PRs while static package
and artifact validation still runs. A maintainer must validate the Unity jobs before
merging a fork contribution.

## Artifacts and Failures

Unity result XML and Unity logs are uploaded for seven days, including failures. The
job summary is generated from result XML and reports dynamic Total/Passed/Failed/
Skipped counts; zero tests, any compile error, any FaceMotion/Assets CS warning, or
any failed test fails the job. Third-party warning lines do not trigger the
FaceMotion warning gate.

`no-ma-compile-smoke` copies only `Assets`, `Packages`, and `ProjectSettings` into
`.ci/no-ma-smoke`, removes embedded MA and NDMF plus their lock/VPM records, and
never mutates the checked-out source project. It also fails if a MA assembly appears
in the Unity log.

## Local Reproduction

```powershell
pwsh ./Tools/ci/Validate-Package.ps1
pwsh ./Tools/create-vpm-release.ps1
pwsh ./Tools/ci/Validate-VpmArtifact.ps1
pwsh ./Tools/ci/New-NoMaSmokeProject.ps1
```

For local Unity runs, use Unity 2022.3.22f1 and set `VRC_TEST_PROTOCOL=0`. The
project's established EditMode command must omit `-quit`; see the saved test logs
under `TestResults/` for the current invocation.

## Action and Cache Policy

The workflow uses stable major action tags (`actions/checkout@v4`,
`actions/upload-artifact@v4`, and `game-ci/unity-test-runner@v4`) rather than
floating branch names, and pins GameCI's downloaded CLI to `v0.1.65`. A commit SHA
pin is a stricter supply-chain option when this repository is published. No Unity
Library cache is configured initially: the cache is large and fragile for embedded
VRCSDK/MA packages. Jobs have a 30-minute Unity timeout and per-ref concurrency
cancellation.
