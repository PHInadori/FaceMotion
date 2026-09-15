# VPM Distribution (Phase I.3)

This document describes how FaceMotion is distributed as a VPM package: the package
repository, the separate listing repository, the release artifact format, checksums,
and how VCC / ALCOM add the repository. Everything here is **preparation only** —
no repository has been created and nothing has been published.

Companion docs: [Compatibility.md](Compatibility.md) (persisted-data contract),
[Schema-Migration.md](Schema-Migration.md) (migration internals).

## Package repository

Planned: **`PHInadori/FaceMotion`** (GitHub). It is the source of the package and hosts
release artifacts. The package itself lives at `Packages/com.facemotion.editor/`.

This repository is intentionally **not used for the VPM listing**; the listing lives in
a separate repository (below), so the two roles never mix:

| Concern | Package repo (`PHInadori/FaceMotion`) | Listing repo (`PHInadori/<VPM listing>`) |
|---|---|---|
| Source of `Packages/com.facemotion.editor` | yes | no |
| GitHub Releases with `.zip` + checksum | yes | no |
| `source.json` (listing source) | no | yes |
| Generated `index.json` on GitHub Pages | no | yes |
| VCC/ALCOM "add repository" URL | no | yes (points at `index.json`) |

## Release artifact

- Format: ZIP with the **package root at the archive root**. The archive contains `package.json`,
  `Editor/`, `Documentation~/`, `Tests/`, `README.md`, `CHANGELOG.md`, `LICENSE.md`,
  `THIRD-PARTY-NOTICES.md`, and the corresponding `.meta` files. `package.json` must not be
  nested under `Packages/com.facemotion.editor/` inside the ZIP.
- Naming is consistent: `com.facemotion.editor-<version>.zip` (for example
  `com.facemotion.editor-0.1.0.zip`).
- SHA-256: computed over the generated ZIP and emitted into the release metadata
  (`release-info.json`). The checksum is stored in the **listing**, never in `package.json`.

Local preparation script: `Tools/create-vpm-release.ps1` at the repository root. Run it
before the release workflow uploads; the GitHub Actions workflow `vpm-release.yml` runs the
same script on a version tag.

Local validation (manual, when a VPM CLI is available):

```powershell
vpm check package "path\to\Packages\com.facemotion.editor"   # manifest + resolve check
```

(`vpm` is the official `vrchat.vpm.cli` .NET global tool. It is not installed in this
workspace, so Phase I.3 validation used JSON/SemVer checks plus a local ZIP inspection
instead.)

## GitHub Actions (release only)

`.github/workflows/vpm-release.yml` (repository root) runs on a `v*` tag push or manual
dispatch:

1. Path to the package is selected (default `Packages/com.facemotion.editor`).
2. `pwsh Tools/create-vpm-release.ps1` builds `com.facemotion.editor-<version>.zip` and
   `release-info.json` (including `zipSHA256`).
3. Uploads both to the GitHub Release created for the tag.

No pushes, releases, or Pages are performed in Phase I.3 — the workflow files are prepared
locally only.

Source validation is separate in `.github/workflows/ci.yml`; see [CI.md](CI.md). The release
workflow currently does not itself require a completed CI run before a tag release.

## Package manifest vs release listing

- `package.json` is the **Unity package manifest** (name, version, displayName, unity,
  author, license, description, keywords, `vpmDependencies`). It never contains the ZIP URL,
  checksum, or changelog URL.
- The **release listing** is a `source.json` → GitHub Pages `index.json`. Each package
  version embeds the full package manifest **plus** the listing-only fields that VPM uses:
  `url` (release ZIP URL) and `zipSHA256`. `changelogUrl` and `infoLink` are optional
  listing metadata.

## VPM repository format

An `index.json` repo listing looks like:

```json
{
  "name": "<listing display name>",
  "id": "<listing repo id>",
  "url": "https://<user>.github.io/<listing-repo>/index.json",
  "author": "<contact email>",
  "packages": {
    "com.facemotion.editor": {
      "versions": {
        "0.1.0": {
          "name": "com.facemotion.editor",
          "url": "https://github.com/PHInadori/FaceMotion/releases/download/v0.1.0/com.facemotion.editor-0.1.0.zip",
          "zipSHA256": "<sha256 of the zip>",
          "version": "0.1.0",
          "displayName": "FaceMotion",
          "description": "Unity Editor tooling for authoring animated facial motion for VRChat avatars...",
          "unity": "2022.3",
          "author": { "name": "PHInadori" },
          "license": "MIT",
          "keywords": ["vrchat", "avatar", "animation", "blendshape", "facial-animation", "editor", "editor-tool"],
          "vpmDependencies": { "com.vrchat.avatars": "3.10.5" }
        }
      }
    }
  }
}
```

The recommended, official way to generate this is the VRChat **template-package-listing**
repository: edit its `source.json` (fill in the listing `name`, `id`, `url`, `author`, and
point `githubRepos` at `PHInadori/FaceMotion`), enable GitHub Pages on a GitHub Actions
source, and run the built-in **Build Repo Listing** action. It produces `index.json` and a
landing page automatically. A local candidate `source.json` is prepared at
`Tools/vpm-listing-template/source.json`.

## Listing repository strategy

Planned: a **separate listing repository**, `PHInadori/PHInadori-VPM`, so the
package repository stays clean. Its listing `id` is `com.phinadori.vpm`, which
is separate from the package ID.

The planned Pages URL is
`https://phinadori.github.io/PHInadori-VPM/index.json`. It remains non-live
until the listing repository is created and GitHub Pages is enabled.

## Package ID

`com.facemotion.editor` is retained for the first public release (see the Phase I.3 final
report). Changing it after first public release costs existing users a package migration, so
the decision is locked before publication.

## Adding to VCC / ALCOM

Once the listing repository is public:

- **VCC**: Manage Packages → + Add repository → paste the Pages URL ending in `index.json`.
- **ALCOM**: Settings → add repository → paste the same URL.

Until then nothing points at a live URL and no URLs are hard-coded into the package.

## Do-not-publish reminder

Phase I.3 creates local files only. Creating the GitHub repos, pushing, publishing releases,
Pages, or listing registrations, and editing `package.json` after the public release are out
of scope for this phase.
