# Phase G Manual Checklist

1. Select a descriptor with a custom FX controller, expression assets, and a free root-menu slot.
2. Export a FaceMotion clip, assign it in Direct Integration, then plan. Confirm no asset changes occur.
3. Apply and confirm the descriptor points at copied FX, parameter, and menu assets and the manifest is selected.
4. Confirm the copied FX has a FaceMotion layer with WD disabled Off/On states, `Reset.anim` assigned to Off, the exported clip assigned to On, and Bool transitions.
5. Confirm the copied menu has a FaceMotion submenu and Toggle, and the copied parameter costs one bit.
6. Reapply, then verify the prior owned set is replaced without changing original assets.
7. Select the manifest and invoke `DirectVRChatIntegration.Rollback`; confirm original descriptor references return and only owned assets disappear.
8. Confirm Planning shows the proposed parameter, FX layer, and asset stem. Planning must reject a pre-existing generated folder unless it belongs to this avatar's manifest.
9. If a manifest has been manually edited, confirm rollback reports and skips any owned path outside its generated folder.
10. Build & Test the avatar. With the Toggle OFF, confirm the Apply-time baseline pose; turn it ON and confirm FaceMotion plays; turn it OFF and confirm the baseline returns. Repeat several ON/OFF cycles and confirm Console Error is 0.
