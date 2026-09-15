# Phase F Manual Checklist

1. Open a FaceMotion project, select an animation, and set a positive duration and frame rate.
2. Add blend shape, position, rotation, and scale tracks with keys; enable looping.
3. Export to `Assets/FaceMotion/Exports/Test.anim` from AnimationClip Export.
4. Inspect the clip: it has blendShape curves, local P/R/S component curves, the selected frame rate, and loop enabled.
5. Scrub each emitted frame and compare the clip values with FaceMotion preview.
6. Export to the same path again and confirm references to the clip remain assigned (the asset GUID is unchanged).
7. Enter an outside-Assets path, a non-`.anim` extension, an invalid duration, or an enabled empty track and confirm export is disabled with diagnostics.
8. Confirm this workflow creates no FX controller, parameter, menu, or Modular Avatar asset.
