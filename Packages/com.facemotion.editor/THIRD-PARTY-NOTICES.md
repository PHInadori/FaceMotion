# THIRD-PARTY NOTICES

FaceMotion itself does not copy, vendor, or redistribute any third-party source
code, libraries, assets, or documentation. All third-party packages below are
external dependencies loaded from their own records and remain their respective
owners' property. FaceMotion only references their public APIs.

## Required dependency

- **VRChat SDK - Avatars** (`com.vrchat.avatars` 3.10.5)
  - https://docs.vrchat.com/docs/avatars-30
  - Declared in `package.json` `vpmDependencies`. Brought in transitively:
  - **VRChat SDK - Base** (`com.vrchat.base`)

## Development/test-only dependency

- **Unity Test Framework** (`com.unity.test-framework` 1.1.33)
  - Used by the package's `Tests` assembly fixtures. It is not a runtime or VPM installation dependency of FaceMotion.

## Optional dependencies

- **Modular Avatar** (`nadena.dev.modular-avatar` 1.18.7)
  - Optional. The Modular Avatar backend is assembled only when this package is
    present. It is deliberately *not* listed in `vpmDependencies`.
  - Brought in transitively:
  - **NDMF** (`nadena.dev.ndmf`) - used by Modular Avatar for its build processing.

## Test-avatar only

- **lilToon** (`jp.lilxyzw.liltoon`)
  - Required only by the development project's third-party test avatars
    (e.g. the `Yumeka` avatar under `Assets/`). It is not bundled with and not
    required by this package and does not appear in any FaceMotion code.

## License

The FaceMotion package itself is distributed under the MIT License; see
[LICENSE.md](LICENSE.md) for the full text.
