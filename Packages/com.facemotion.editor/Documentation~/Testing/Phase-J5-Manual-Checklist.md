# Phase J5 Manual Verification

Status: COMPLETE

## Environment

- Unity 2022.3.22f1
- Package: com.facemotion.editor
- Package version: 0.1.2
- Real avatar project: Komane.v2-FaceMotion
- Komane local file dependency used for verification
- Modular Avatar installed

## Automated Validation

- J5 targeted: 20/20 PASS
- MA: 682 passed / 2 skipped / 0 failed / 684 total
- no-MA: 614 passed / 0 failed / 0 skipped
- Compile errors: 0
- FaceMotion warnings: 0

## Manual Real-Avatar Verification

17/17 PASS

1. First-run guidance reaches the preview/integration step at each stage.
2. Step hint advances: avatar → animation → track → key → preview/integration.
3. Empty animations list shows a clear empty state, not a blank area.
4. Empty track list shows a clear empty state, not a blank area.
5. A track with no keys shows the key empty state.
6. Primary action (VRChatへ追加) reads as the obvious next step.
7. Advanced / manual controls (manual export path, manual AnimationClip, Plan/Validate, manual bindings) are reachable but collapsed under 詳細設定.
8. Manual workflow still works end to end when the foldout is opened.
9. Backend dropdown shows a plain-language description and tooltip.
10. Modular Avatar is still the default when available.
11. One-click VRChatへ追加 still runs Export → Plan → Validate → Apply.
12. Success state shows a check, the backend, and the animation name.
13. Tooltips appear for backend, loop, frame rate, track, VRChatへ追加, and 詳細設定.
14. Unified JA terms are used (アニメーション / トラック / キー / プレビュー / エクスポート / 統合).
15. Shortcut help foldout lists Ctrl+ホイール zoom, Delete, preview camera, and F focus.
16. Layout is stable at 1366x768 and 1920x1080+ (no overlap/clipping).
17. Disabled actions explain why they are disabled; conflict status is not signaled by color alone.

## J5 Implementation Guarantees

- Guidance is advisory only; no control is hidden or forced.
- Guidance state is derived from session data, never stored separately.
- Advanced/manual workflow is preserved behind an existing persistent foldout.
- Empty states are localized in both Japanese (default) and English (fallback).
- `FaceMotionDiagnosticCodes` count is unchanged (147 codes; 14 Info).
- Success additions reuse the existing `OneClickIntegrationResult`.
- No workflow logic added to the window or panels.
- Package version remains 0.1.2.

## Frozen Post-J4 Baseline

- J1 PASS
- J2 PASS
- J3 COMPLETE
- J4 COMPLETE
- MA baseline: 664 total / 662 pass / 2 skip / 0 fail
- no-MA baseline: 594 pass
- Compile errors: 0
- FaceMotion warnings: 0
- Komane local dependency remains active
- No public repo/tag/release/VPM changes

## Important

- Documentation only.
- Package version is unchanged.
- No public repo sync.
- No tag/release/VPM publish.
