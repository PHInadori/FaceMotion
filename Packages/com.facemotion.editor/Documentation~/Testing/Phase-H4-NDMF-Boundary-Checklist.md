# Phase H.4 NDMF Boundary Checklist

Phase H.4 uses Modular Avatar 1.18.7 only through its public component API. FaceMotion does not call NDMF APIs, including NDMF internal or experimental APIs. Modular Avatar owns its normal NDMF build processing.

## Automated Boundary Checks

1. Run the complete EditMode suite. `PhaseH4ModularAvatarContractTests.InstalledModularAvatarPackage_IsExactly1187` pins the installed Modular Avatar package used by the live component tests to `1.18.7`.
2. Confirm `FaceMotion.Editor.ModularAvatar.asmdef` is Editor-only, package-gated, and references `nadena.dev.modular-avatar.core` rather than `nadena.dev.ndmf`.
3. Search FaceMotion source for `nadena.dev.ndmf`, `NDMFInternal`, and `NDMFExperimental`. There must be no FaceMotion production references.
4. Confirm the live tests create only `ModularAvatarMergeAnimator`, `ModularAvatarParameters`, and `ModularAvatarMenuInstaller`, then inspect their public serialized configuration and generated controller/menu assets.

## Manual Build Checklist

1. Open a scene avatar with Modular Avatar 1.18.7 installed and choose the Modular Avatar backend in FaceMotion.
2. Plan and apply a short exported clip. Verify the generated child contains only the three FaceMotion-owned MA components and that the avatar's existing FX controller, expression parameters, and expression menu references are unchanged.
3. Run NDMF preview/build using the normal Modular Avatar workflow. Verify the generated toggle drives the intended clip and no Write Defaults warning or competing binding is introduced.
4. Reapply the same animation, then remove it. Verify the generated child and `FaceMotionMA_<name>` assets are replaced/removed while user-owned assets remain.
5. Repeat with a prefab asset, an existing MA parameter collision, and an overlapping animated binding. Each plan must block before changing the scene or assets.
6. Record Unity, VRChat SDK, MA, and NDMF versions with the build result. This checklist is manual because NDMF preview/build execution is owned by NDMF and is intentionally outside FaceMotion's API boundary.
