# Phase C Manual UI Checklist

Unity通常GUI環境で実行する。各行に `PASS` / `FAIL` と Notes を記録する。

| # | 確認項目 | PASS / FAIL | Notes |
| --- | --- | --- | --- |
| 1 | Tools > FaceMotion で Window が開く |  |  |
| 2 | レイアウトが崩れていない |  |  |
| 3 | Project を新規作成できる |  |  |
| 4 | VRCAvatarDescriptor を指定できる |  |  |
| 5 | Avatar Index を Rebuild できる |  |  |
| 6 | BlendShape candidate picker で検索・選択して Track を追加できる |  |  |
| 7 | 同名BlendShape候補が RendererPath で区別される |  |  |
| 8 | Transform candidate picker で検索・Position/Rotation/Scale を選択できる |  |  |
| 9 | Avatarなしでは candidate picker が安全に無効化される |  |  |
| 10 | Animation New / Duplicate / Rename / Delete が動作する |  |  |
| 11 | Duration / Frame Rate を Apply でき、無効値はcommitされない |  |  |
| 12 | Loop toggle が playback に反映される |  |  |
| 13 | Key を追加できる |  |  |
| 14 | Key を選択できる |  |  |
| 15 | Ctrl/Cmd で複数 Key を選択できる |  |  |
| 16 | 1 Key を Drag できる |  |  |
| 17 | 複数 Key を Drag できる |  |  |
| 18 | Ctrl/Cmd + Wheel で Zoom できる |  |  |
| 19 | Middle Mouse Drag で Scroll/Pan できる |  |  |
| 20 | Fit ボタンで表示を fit できる |  |  |
| 21 | Timeline header の Snap toggle と FPS表示が正しい |  |  |
| 22 | Snap 有効/無効で Add / Drag / Paste / Scrub が期待通り動く |  |  |
| 23 | Copy/Paste で相対時間を維持して貼り付けできる |  |  |
| 24 | Delete / Backspace で選択 Key を削除できる |  |  |
| 25 | Inspector で Time / Value / Interpolation を buffer編集できる |  |  |
| 26 | Inspector Apply は Ctrl/Cmd+Z 1回で全fieldを戻せる |  |  |
| 27 | Inspector Revert は未Apply値を破棄する |  |  |
| 28 | Ctrl/Cmd+Y または Unity Redo で戻した操作を再適用できる |  |  |
| 29 | Drag 中 Escape で元の位置へ戻る |  |  |
| 30 | Missing Binding が Track/Timeline/Diagnostics に表示される |  |  |
| 31 | Avatar hierarchy 変更後に Dirty 表示される |  |  |
| 32 | Rebuild 後に Dirty 表示が解消される |  |  |
| 33 | Playback で cursor が進み、Loop/終端停止が正しい |  |  |
| 34 | 狭い Window でも致命的に崩れない |  |  |
| 35 | Console Error が 0 件 |  |  |

## Manual verification focus

- Candidate picker filtering and duplicate-name path visibility require a real avatar scene.
- Duration shortening must leave out-of-range keys intact and show a diagnostic.
- `-nographics` EditMode tests verify contracts but cannot verify visual layout or pointer feel.

## Phase C.2 layout size checks

| Window width | Expected layout | PASS / FAIL | Notes |
| --- | --- | --- | --- |
| About 900px | Left panel remains readable at its minimum width; timeline retains usable width; no window-level horizontal scrollbar |  |  |
| About 1200px | Default proportional split keeps Project/Avatar/Track controls readable and timeline grows naturally |  |  |
| 1600px or wider | Left column remains at or below 40% of usable width; timeline uses remaining width; splitter can adjust the balance |  |  |

## Phase C.2 interaction checks

| Check | PASS / FAIL | Notes |
| --- | --- | --- |
| Drag the left/right splitter and verify both panes remain within usable limits |  |  |
| Reopen the Window and verify the splitter ratio is restored |  |  |
| Scroll long Diagnostics vertically without introducing a horizontal window scrollbar |  |  |
| Verify Select Avatar, Rebuild Index, Open Project, and New Project labels are fully visible |  |  |
