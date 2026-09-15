# Phase H.1: Modular Avatar Backend

FaceMotion supports Modular Avatar 1.18.7 through `FaceMotion.Editor.ModularAvatar`, an Editor-only asmdef gated by the `nadena.dev.modular-avatar` package version. Every Modular Avatar type reference is confined to that assembly. Core, VRChat, and UI use MA-free request/result contracts; the UI discovers the optional implementation at runtime. Direct remains the default backend and does not depend on MA.

Planning is read-only. It validates avatar, clip, output folder, parameter length, existing FaceMotion MA manifests, MA parameter-name collisions, MA merge-animator binding collisions, and collisions against the avatar FX controller. Blocking diagnostics leave the scene and assets unchanged.

Apply creates one FaceMotion-owned child object containing the public MA 1.18.7 `ModularAvatarMergeAnimator`, `ModularAvatarParameters`, and `ModularAvatarMenuInstaller` components. It creates an owned FX controller and menu asset under `FaceMotionMA_<name>`; it never edits a user FX controller, expression-parameter asset, or expression menu. Reapply handles an attached owned object or reconnects retained detached assets. Remove deletes only the manifest's child hierarchy and retains generated `Assets/` paths. No MA internal APIs and no Phase I behavior are used.

### Generated Asset Lifecycle

Generated assets and the manifest are owned exclusively through `OwnedAssetPaths`; no folder name, asset name, or prefix justifies deletion. Three operations are defined:

- **Remove Integration** deletes only the manifest's integration root object (the FaceMotion child hierarchy). It retains the generated `FX.controller`, `Menu.asset`, `Reset.anim`, and the manifest. A manifest without its integration object is the formal *detached integration* state.
- **Reapply** reconnects a manifest and its retained generated assets. After `Remove`, reapply reuses the same manifest folder and regeneration replaces the owned assets in place at their existing paths; it never creates a second generated folder (no `FaceMotionMA_<name> 1`). For an attached integration, reapply removes the prior facemotion child object before regeneration.
- **Tampered ownership** is blocked: if a manifest lists a path that is outside its folder or has an unrecognized generated filename, or if a non-owned object already occupies the integration root name, reapply fails with `FM-OWNERSHIP-TAMPERED` and mutates nothing. Foreign assets beside the generated ones always survive remove and reapply.

The Japanese-default UI offers plan, apply/reapply, and remove actions for Modular Avatar and localizes their labels. If MA is absent or cannot be loaded, it retains the metadata diagnostics and Direct stays available.
