# VPM Listing Repository Template (local candidate)

This folder is the local candidate for the **separate** VPM listing repository for
FaceMotion. It is based on the official VRChat **template-package-listing** repository.

> These files belong in the *listing* repository, never inside the FaceMotion package
> repository or the package itself (see `Documentation~/VPM-Distribution.md`).
> They are prepared locally in Phase I.3 and are NOT published.

## Configured local identity

The prepared listing identity is PHInadori / `PHInadori-VPM` /
`com.phinadori.vpm`, with `phinadori@gmail.com` as the contact. The URL is the
planned GitHub Pages URL; it is not live until the repositories and Pages site
are created.

## Steps (after decisions)

1. Create the listing repository from `vrchat-community/template-package-listing`.
2. Copy `source.json` and point `githubRepos` at `PHInadori/FaceMotion`.
3. Create the package repository `PHInadori/FaceMotion` (package source + releases).
4. Push a `v0.1.0` tag; its release workflow attaches
   `com.facemotion.editor-0.1.0.zip` and `release-info.json` (with the real `zipSHA256`).
5. Run the listing repo's **Build Repo Listing** action. It generates `index.json` with the
   full per-version manifest (including `url` and `zipSHA256`) and publishes Pages.
6. Users add `https://phinadori.github.io/PHInadori-VPM/index.json` in VCC or ALCOM.

The release and Pages URLs remain planned until the repositories are created and
published. These local templates do not publish anything.
