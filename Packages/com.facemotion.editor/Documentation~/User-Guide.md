# User Guide

## Window と Project

FaceMotion window は **Tools/FaceMotion/FaceMotion ウィンドウを開く** から開きます。上部の workflow guidance は、avatar → animation → track → key → preview/integrate の次の操作を示します。`FaceMotion Project` は animation、track、keyframe を保存する asset です。`アニメーション` の **+ 新規アニメーション** で編集対象を作り、timeline settings の `長さ`、`フレームレート`、`ループ` を設定します。左下の shortcut help では timeline、preview camera、key 操作を確認できます。

選択中の Animation には `アニメーション名` フィールドがあります。ここで編集した名前は VRChat の menu label と parameter 名にそのまま使われ、空文字は設定できません。Animation list の各項目の checkbox で複数選択し、Modular Avatar の **VRChatへ反映** で選択状態を VRChat 側へ同期できます。

## Avatar と Track

avatar root を選択すると binding 候補を取得できます。`トラック` では次を追加できます。

| Track | 対象 |
| --- | --- |
| BlendShape | SkinnedMeshRenderer の BlendShape weight |
| Position | avatar root 基準の Transform local position |
| Rotation | authored Euler rotation |
| Scale | Transform local scale |

`+ ブレンドシェイプ` は Face / Hair / Body / Clothes / Other、Eye / Blink / Brow / Mouth のカテゴリボタン、search、confidence 表示から候補を選びます。VRChat Blink、LipSync、FX、Modular Avatar の conflict は key を追加する前に確認できます。BlendShape Track を追加すると、scene 内 avatar の現在の BlendShape weight を 0 秒の初期 key として自動生成します（avatar 未選択や読み取り不能な場合は 0）。`+ ブレンドシェイプ` と `+ トランスフォーム` で候補が曖昧な場合は推測せず、`詳細な手動バインディング` で renderer/transform path を確認してください。

## Keyframe と interpolation

timeline で再生ヘッドを置き、key を追加して Inspector で値と `補間` を編集します。現在の interpolation は outgoing key（左側の key）の設定です。

`キーインスペクター` では Time、BlendShape value または Transform X/Y/Z、補間を編集できます。**適用**で選択 key を更新し、**戻す**で読み直します。key 未選択時は入力した time / value で **キーを追加** できます（BlendShape の既定値は 100）。選択 key は Delete / Backspace で削除でき、追加操作は現在の track と playhead を使用します。

**Quick Key** は現在の時間に設定値（0-100、EditorPrefs に保存）で BlendShape key を追加し、同じ時間に key がある場合は更新します。**Key Actions** メニューから Copy / Paste / Duplicate / Nudge（±1 / ±5 フレーム）も実行できます。複数 key・複数 track をまとめて copy / paste / move でき、選択済みの key を modifier なしで drag すると選択全体が移動します。Paste は key をコピーした同じ animation 内でのみ有効です。native Shortcut Manager actions は既定の binding なしで登録されるため、必要な場合は **Edit > Shortcuts** の `FaceMotion/Timeline` から割り当ててください。

- Hold
- Linear
- Ease In
- Ease Out
- Ease In-Out
- Smooth

BlendShape、Position、Scale は補間値を評価します。Rotation は Euler として保存しますが、評価時は Quaternion shortest path を使用します。360 度を超える連続回転は current rotation mode では対象外です。

`スナップ` を有効にすると時間は nearest frame に丸められ、`0..長さ` に制限されます。timeline は scrub、key drag、Delete/Backspace、Ctrl/Cmd-A/C/V/D、Escape cancel、Home（time zero）、middle drag pan、Ctrl/Cmd-wheel zoom、`全体表示`、`縮小`、`拡大` を利用できます。

## Preview と Scene Apply

`プレビュー` は avatar clone を使うため、編集中のアニメーションを scene へ自動適用しません。play / pause / stop、loop、scrub は realtime に更新されます。Alt+left drag で orbit、middle drag で pan、wheel または Alt+right drag で zoom、F で focus できます。

**シーンに適用** は別の明示操作です。開始時に avatar baseline を取得し、各更新で baseline を復元してから enabled track の値を適用します。**シーン適用を停止**、window close、assembly reload、Play Mode transition では baseline を復元します。これは source FaceMotion clip binding の確認用であり、avatar の全状態を完全 reset する機能ではありません。

## VRChat 統合

通常の複数 Animation の統合では Modular Avatar を使い、`VRChat 統合` の checkbox と **VRChatへ反映** で現在の選択状態を同期します。FaceMotion 所有でない clip は上書きしません。`詳細設定` foldout には選択中の単一 Animation の **VRChatへ追加**（Export → Plan → Validate → Apply）と、個別の手動 Plan / Apply が残っています。詳細は [VRChat Integration](VRChat-Integration.md) を参照してください。

メニューに表示される label は Animation の `アニメーション名` です。MA の選択状態同期では、チェック済みの Animation を保持または追加し、チェックを外した以前の FaceMotion 管理対象は削除します。Direct Integration は別の `詳細設定` の個別 workflow です。過去の Direct Batch 公開 API と保存済み manifest は互換性のため保持していますが、現在の初心者向け checkbox 更新とは別です。

## Undo

Project、animation、track、key、scene apply に関わる editor 操作は Unity Undo の対象です。Apply 後も、まず Inspector と diagnostic を確認してから project を保存してください。
