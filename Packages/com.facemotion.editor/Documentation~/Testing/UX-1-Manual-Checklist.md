# UX-1 Manual Checklist

1. Open FaceMotion at the minimum 900x700 window size; verify preview, timeline, and the full key inspector remain visible.
2. Select a blend shape key and confirm the inspector shows its time, value, and interpolation.
3. Drag that selected key, release it, and confirm the inspector immediately shows the new time without reselection.
4. Undo and redo the drag; confirm the inspector reloads each restored value.
5. Edit an inspector field, then select a different key; confirm the pending edit is discarded and the newly selected key loads.
6. Confirm Apply and Revert are on the first inspector button row and Add Key, Select All, and Delete are on the second.
7. Hover every inspector button and confirm its tooltip states the action.
8. Drag a single key repeatedly to the same pointer location; confirm it stays at the same time rather than accumulating movement.
9. Select multiple keys on different tracks and drag them; confirm their relative offsets remain unchanged.
10. Enable snapping and drag a key between frame boundaries; confirm the final time lands on the selected animation frame grid.
11. Disable snapping and repeat; confirm the final time follows the exact pointer position.
12. Pan or scroll the timeline, then drag a key; confirm its pointer anchor has no label-column or scroll offset.
13. Hover a key marker; confirm its highlight, hand cursor, and time tooltip appear, and click its visible marker to select it.
14. Reopen the window and confirm Japanese is the default for toolbar, panels, timeline, inspector, menus, dialogs, and diagnostics.
15. Verify an explicitly configured English fallback shows English labels where the host/editor integration requests it.
16. Trigger a validation error and an export or integration diagnostic; confirm the diagnostic code remains visible and the localized message is understandable.
17. At 900x700, drag both splitters to their extremes; confirm the left panel, preview, timeline, and inspector never overlap or collapse below their minimum usable areas.
18. Audit the Project, Avatar, Animations, Tracks, AnimationClip Export, VRChat Direct Integration, Diagnostics, Preview, and key inspector panels: labels, buttons, help text, menus, dialogs, and tooltips are Japanese by default.
19. Trigger an unlisted diagnostic (for example, a write failure); confirm the Japanese UI shows its stable code with Japanese guidance and does not surface the raw English detail or suggested fix.
20. With the English fallback explicitly requested by the host/editor integration, confirm the same panel keys, diagnostic detail, and suggested fix render in English.
