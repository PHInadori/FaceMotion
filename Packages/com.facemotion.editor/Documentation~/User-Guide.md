# User Guide

## Window と Project

FaceMotion window は **Tools/FaceMotion/FaceMotion ウィンドウを開く** から開きます。上部の workflow guidance は、avatar → animation → track → key → preview/integrate の次の操作を示します。`FaceMotion Project` は animation、track、keyframe、generation record を保存する asset です。`アニメーション` の **+ 新規アニメーション** で編集対象を作り、timeline settings の `長さ`、`フレームレート`、`ループ` を設定します。左下の shortcut help では timeline、preview camera、key 操作を確認できます。

## Avatar と Track

avatar root を選択すると binding 候補を取得できます。`トラック` では次を追加できます。

| Track | 対象 |
| --- | --- |
| BlendShape | SkinnedMeshRenderer の BlendShape weight |
| Position | avatar root 基準の Transform local position |
| Rotation | authored Euler rotation |
| Scale | Transform local scale |

`+ ブレンドシェイプ` は Face / Hair / Body / Clothes / Other、Eye / Blink / Brow / Mouth の tree、search、confidence 表示から候補を選びます。VRChat Blink、LipSync、FX、Modular Avatar の conflict は key を追加する前に確認できます。`+ ブレンドシェイプ` と `+ トランスフォーム` で候補が曖昧な場合は推測せず、`詳細な手動バインディング` で renderer/transform path を確認してください。

## Keyframe と interpolation

timeline で再生ヘッドを置き、key を追加して Inspector で値と `補間` を編集します。現在の interpolation は outgoing key（左側の key）の設定です。

`キーインスペクター` では Time、BlendShape value または Transform X/Y/Z、補間を編集できます。**適用**で選択 key を更新し、**戻す**で読み直します。key 未選択時は入力した time / value で **キーを追加** できます。選択 key は Delete / Backspace で削除でき、追加操作は現在の track と playhead を使用します。

- Hold
- Linear
- Ease In
- Ease Out
- Ease In-Out
- Smooth

BlendShape、Position、Scale は補間値を評価します。Rotation は Euler として保存しますが、評価時は Quaternion shortest path を使用します。360 度を超える連続回転は current rotation mode では対象外です。

`スナップ` を有効にすると時間は nearest frame に丸められ、`0..長さ` に制限されます。timeline は scrub、key drag、Delete/Backspace、Ctrl/Cmd-A/C/V、Escape cancel、Home（time zero）、middle drag pan、Ctrl/Cmd-wheel zoom、`全体表示`、`縮小`、`拡大` を利用できます。

## Preview と Scene Apply

`プレビュー` は avatar clone を使うため、生成モーションを scene へ自動適用しません。play / pause / stop、loop、scrub は realtime に更新されます。Alt+left drag で orbit、middle drag で pan、wheel または Alt+right drag で zoom、F で focus できます。

**シーンに適用** は別の明示操作です。開始時に avatar baseline を取得し、各更新で baseline を復元してから enabled track の値を適用します。**シーン適用を停止**、window close、assembly reload、Play Mode transition では baseline を復元します。これは source FaceMotion clip binding の確認用であり、avatar の全状態を完全 reset する機能ではありません。

## Preset と Random Motion

標準 preset は Smile、Wink Left、Wink Right、Blink、Angry、Sad、Surprise、Embarrassed です。logical target が一意に解決できる場合だけ適用できます。

Blink、Random Blend Shape、Random Rotation は seed を指定して生成します。同じ algorithm version、settings、seed、binding なら同じ生成結果を期待できます。algorithm version または settings を変えると結果も変わるため、既存の手動 key を確認してから regenerate してください。生成操作は manual key を保護します。

## VRChat 統合

通常は `VRChat 統合` の **VRChatへ追加** を使います。選択 animation の owned `.anim` を必要に応じて export し、Export → Plan → Validate → Apply を順に実行します。FaceMotion 所有でない clip は上書きせず、再実行は owned clip の GUID と managed integration を再利用します。詳細な手動設定は `詳細設定` foldout から使用できます。詳細は [VRChat Integration](VRChat-Integration.md) を参照してください。

## Undo

Project、animation、track、key、generation、scene apply に関わる editor 操作は Unity Undo の対象です。生成・Apply 後も、まず Inspector と diagnostic を確認してから project を保存してください。
