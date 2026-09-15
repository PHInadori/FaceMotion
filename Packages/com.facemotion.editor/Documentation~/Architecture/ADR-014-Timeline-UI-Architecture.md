# ADR-014: Timeline UI Architecture

- Status: Accepted
- Date: 2026-09-14

## Context

The editor needs an interactive timeline without coupling screen coordinates, IMGUI events,
or repaint policy to the serialized motion model. A single window class would otherwise
become responsible for rendering, hit testing, mutation, Undo, and diagnostics.

## Decision

The timeline is separated into focused UI types:

- `TimelineView` composes one frame: it builds lane layout, routes the current event, and
  invokes rendering.
- `TimelineViewState` carries viewport and interaction state.
- `TimelineGeometry` is the shared time/pixel authority: `TimeToPixel`, `PixelToTime`,
  visible duration, scroll clamp, ruler steps, fit zoom, and anchored zoom.
- `TimelineLayoutBuilder` creates track lanes and `TimelineHitTest` resolves rows/keys.
- `TimelineRenderer` draws the ruler, grid, lanes, key markers, selection, and cursor.
- `TimelineInputHandler` maps IMGUI events to scrub, selection, drag, pan, zoom, and
  keyboard actions.
- Controllers own validation, commands, Undo boundaries, collision policy, and mutation.

The timeline header binds its Snap toggle directly to editor-only `TimelineViewState` and
shows the selected animation's frame rate. Animation timeline settings are presented by the
animation panel but still commit only through `AnimationController`.

Zoom is constrained by `TimelineViewState.ClampZoom`. Scroll is expressed as the time at the
left edge and is clamped from duration and visible width. Fit derives zoom from duration and
plot width. The geometry layer is independent of the renderer's visual style.

IMGUI is used because this package is an EditorWindow workflow with immediate event routing
and existing Unity Editor integration. A future UI Toolkit migration replaces the view,
renderer, and event adapter at the boundary; session state, controllers, selection,
clipboard, geometry, and layout remain reusable.

## Consequences

- `FaceMotionWindow` remains a composition and lifecycle host rather than a timeline god
  class.
- Timeline calculations have permanent EditMode coverage without opening a graphics-backed
  window.
- Rendering can change without changing project commands or domain data.
