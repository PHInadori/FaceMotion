# Phase J4 Manual Verification

Status: COMPLETE

## Environment

- Unity 2022.3.22f1
- Package: com.facemotion.editor
- Package version: 0.1.2
- Real avatar project: Komane.v2-FaceMotion
- Komane local file dependency used for verification
- Modular Avatar installed

## Automated Validation

- J4 targeted: 23/23 PASS
- MA: 662 passed / 2 skipped / 0 failed / 664 total
- no-MA: 594 passed / 0 failed / 0 skipped
- Compile errors: 0
- FaceMotion warnings: 0

## Manual Real-Avatar Verification

14/14 PASS

1. One-click **VRChatへ追加** runs Export → Plan → Validate → Apply.
2. Auto-export works without manual export.
3. Manual AnimationClip assignment is not required in the normal workflow.
4. Modular Avatar is selected by default when available.
5. Plan/Validate runs automatically.
6. Progress UI appears and always clears.
7. Blocking diagnostics prevent Apply.
8. A foreign AnimationClip is never overwritten.
9. Advanced foldout state persists.
10. Re-run is idempotent; no duplicate integration/objects.
11. AnimationClip GUID is preserved on overwrite.
12. The generated integration object/manifest can be selected/pinged.
13. Explicit backend selection is respected and persisted.
14. Remove / re-Apply remain functional; no unexpected console exceptions or mutations.

## J4 Implementation Guarantees

- Owned clip is overwritten in place.
- Foreign assets are preserved.
- Shared J1 backend selection logic is used.
- Preflight before Apply is write-free.
- `FaceMotionDiagnosticPresentation` is reused.
- Cross-backend warning: `FM-J4-CROSS-BACKEND`.
- Reapply info: `FM-J4-REAPPLIED`.
- Rollback/no partial state info: `FM-J4-NO-PARTIAL-STATE`.
- Diagnostic registry total: 147 codes.
- Info diagnostics: 14.
- Single selected animation only in J4 v1.
- `ClearProgressBar()` is guaranteed in `finally`.
- No duplicate parameter/layer/managed root on re-run.

## Known Expected Limitation

- `FM-AVT-0007` may block Modular Avatar validation when duplicate relative paths exist.
- This is expected safe behavior, not a J4 failure.

## Frozen Post-J4 Baseline

- J1 PASS
- J2 PASS
- J3 COMPLETE
- J4 COMPLETE
- Package version remains 0.1.2
- MA baseline: 664 total / 662 pass / 2 skip / 0 fail
- no-MA baseline: 594 pass
- Compile errors: 0
- FaceMotion warnings: 0
- Komane local dependency remains active
- No public repo/tag/release/VPM changes

## Important

- Documentation only.
- Package version is unchanged.
- Production code is unchanged.
- Tests are unchanged.
- No public repo sync.
- No tag/release/VPM publish.
