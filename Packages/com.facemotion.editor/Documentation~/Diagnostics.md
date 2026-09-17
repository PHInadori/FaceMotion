# Diagnostics

FaceMotion は検証・生成・統合の結果を **diagnostic**（診断レコード）として UI に表示します。
診断には **severity**（ログの重大度）と **action level**（ユーザーが何をすべきか）の2つの軸があり、
コード `FM-<AREA>-<NUMBER>` で一意に識別されます。コードは安定しており、公開後は変更されません。

## 見方

### Severity（重大度）

| Severity | 意味 |
| --- | --- |
| `Info` | 正常に完了。変更や注意の必要はありません。 |
| `Warning` | 動作は続行できますが、品質・互換性のリスクがあります。 |
| `Error` | 処理が中断、または大幅に劣化した可能性があります。 |

Severity は発生時のログの重大度であり、**必ずしも対応が必要かどうかを意味しません**。

### Action level（推奨される対応）

Severity とは独立に、ユーザーへ「何をすべきか」を提示します。

| Action level | 意味 |
| --- | --- |
| **対処が必要 (Required)** | FaceMotion が確実に動作するには、この問題を解決する必要があります。identity が曖昧、export / integration が block された場合など。 |
| **推奨 (Recommended)** | 動作は続行できますが、機能の一部が劣化する、またはリスクがあります。 |
| **情報 (Info)** | 通知のみ。ユーザーの操作は不要です。 |

対応レベルは次の順で決まります。

1. **Blocking** なら `Required`（生成や統合を安全に続行できない状態）。
2. コードに対応する規定（`DiagnosticDefinition`）があれば、その既定レベル（`Recommended` または `Info`）。
3. エラーや警告でも既知でなければ `Recommended`、情報（Info）なら `Info`。

`Required` は常に Blocking から導出されます。詳細メッセージに「Cannot …（できません）」「block されました」と出ている場合は、解消まで適用・生成は安全のため停止されます。

### 言語と代替表示

表示テキストは UI の言語設定（`Application.systemLanguage` = Japanese なら日本語、それ以外は英語）で選ばれます。
未翻訳の項目はもう一方の言語、さらに未設定なら次の汎用メッセージへ段階的にフォールバックします。

| 項目 | フォールバック先 |
| --- | --- |
| タイトル | 「FaceMotion から診断が報告されました」 |
| 概要 | 診断の生メッセージ |
| 解決策 | 診断の Suggested Fix |

未知のコードは翻訳できませんが、コード名は常に表示され、症状は生メッセージで確認できます。

### 選択ボタンの安全性

詳細ビューに **選択** ボタンがある診断は、Hierarchy の該当オブジェクトをハイライトできます。
このボタンは対象が一意に確定できるときだけ表示されます。同一オブジェクトが複数存在して曖昧な場合は、重複のいずれかを勝手に選ばず、一意に決まる親や Avatar Root を選びます。それも決まらない場合はボタン自体を非表示にします。

## 例: 同じ名前のオブジェクト

FaceMotion は顔オブジェクトを hierarchy path（例: `Body/Face`）で特定します。同名のオブジェクトが複数あると **FM-AVT-0007**（`DuplicateTransformPath`）が発生し、blocking として action level は **対処が必要（Required）** になります。

- 原因: 階層内に同じ相対パスが複数存在する
- 影響: どのオブジェクトを指すのか一意に確定できず、操作が安全に停止する
- 解決策: 一方を rename するか、編集対象から外す
- 選択ボタン: 重複のどれも選ばず、一意な親オブジェクト（または Avatar Root）をハイライト

## Modular Avatar binding conflict

**FM-H-MA-BINDING-CONFLICT** は、FaceMotionが統合するAnimationClipと既存のModular Avatar Merge Animatorが同じbindingをアニメーションする場合に、blockingとして表示されます。診断カードには競合object、hierarchy path、Animator Controller、AnimationClip、binding path / property / typeが表示され、**選択**は既存のMerge Animator objectのみをHierarchyで選択します。Sceneやassetは変更しません。

同じ相対pathを持つavatar objectが原因の場合は、Merge Animator conflictではなく **FM-AVT-0007** と紐付く別の診断として表示されます。対象pathを一意にしてから、再度「統合を計画して検証」を実行してください。

## コード一覧

`FM-AVT-0007` のように Blocking 運用されるコードは、通常時は `Recommended` として登録されています。

### Data（データ整合性）– 31 code

| Code | 既定レベル | 分類 |
| --- | --- | --- |
| `FM-DATA-0001` | Recommended | data |
| `FM-DATA-0002` | Recommended | data |
| `FM-ANIM-0001` | Recommended | timeline |
| `FM-ANIM-0002` | Recommended | timeline |
| `FM-ANIM-0003` | Recommended | timeline |
| `FM-ANIM-0004` | Recommended | timeline |
| `FM-TIM-0001` | Recommended | timeline |
| `FM-TIM-0002` | Recommended | timeline |
| `FM-TRK-0001` | Recommended | timeline |
| `FM-TRK-0002` | Recommended | timeline |
| `FM-TRK-0003` | Recommended | timeline |
| `FM-TRK-0004` | Recommended | timeline |
| `FM-TRK-0005` | Recommended | timeline |
| `FM-TRK-0006` | Recommended | timeline |
| `FM-TRK-0007` | Recommended | timeline |
| `FM-KEY-0001` | Recommended | timeline |
| `FM-KEY-0002` | Recommended | timeline |
| `FM-KEY-0003` | Recommended | timeline |
| `FM-KEY-0004` | Recommended | timeline |
| `FM-KEY-0005` | Recommended | timeline |
| `FM-KEY-0006` | Recommended | timeline |
| `FM-KEY-0007` | Recommended | timeline |
| `FM-KEY-0008` | Recommended | timeline |
| `FM-KEY-0009` | Recommended | timeline |
| `FM-KEY-0010` | Recommended | timeline |
| `FM-GEN-0001` | Recommended | generation |
| `FM-GEN-0002` | Recommended | generation |
| `FM-GEN-0003` | Recommended | generation |
| `FM-GEN-0004` | Recommended | generation |
| `FM-OWNERSHIP-FOREIGN-CONTENT` | Recommended | ownership |
| `FM-OWNERSHIP-TAMPERED` | Recommended | ownership |

### Migration（保存形式の移行）– 12 code

| Code | 既定レベル | 分類 |
| --- | --- | --- |
| `FM-MIG-0001` | Recommended | migration |
| `FM-MIG-0002` | Recommended | migration |
| `FM-MIG-0003` | Recommended | migration |
| `FM-MIG-0004` | Recommended | migration |
| `FM-MIG-0005` | Recommended | migration |
| `FM-MIG-UPGRADED` | Info | migration |
| `FM-MIG-FUTURE-VERSION` | Recommended | migration |
| `FM-MIG-MALFORMED` | Recommended | migration |
| `FM-MIG-ID-REPAIRED` | Info | migration |
| `FM-MIG-DUPLICATE-ID` | Recommended | migration |
| `FM-MIG-PARTIAL` | Recommended | migration |
| `FM-MIG-INTEGRATION-AMBIGUOUS` | Recommended | migration |

### Avatar（アバター検証）– 10 code

| Code | 既定レベル | 分類 | 選択対象 |
| --- | --- | --- | --- |
| `FM-AVT-0001` | Recommended | avatar | – |
| `FM-AVT-0002` | Recommended | avatar | Avatar Root |
| `FM-AVT-0003` | Recommended | avatar | Avatar Root |
| `FM-AVT-0004` | Recommended | avatar | Avatar Root |
| `FM-AVT-0005` | Recommended | avatar | Avatar Root |
| `FM-AVT-0006` | Recommended | avatar | Avatar Root |
| `FM-AVT-0007` | Recommended | avatar | 一意な親（曖昧時は Avatar Root） |
| `FM-AVT-0008` | Recommended | avatar | Renderer（相対パス） |
| `FM-AVT-0009` | Recommended | avatar | Renderer の親 |
| `FM-AVT-0010` | Recommended | avatar | Avatar Root |

### AvatarMapping（プロファイル結線）– 18 code

| Code | 既定レベル | 分類 |
| --- | --- | --- |
| `FM-MAP-0001` | Recommended | mapping |
| `FM-MAP-0002` | Recommended | mapping |
| `FM-MAP-0003` | Recommended | mapping |
| `FM-MAP-0004` | Recommended | mapping |
| `FM-MAP-0005` | Recommended | mapping |
| `FM-MAP-0006` | Recommended | mapping |
| `FM-MAP-0007` | Recommended | mapping |
| `FM-MAP-0008` | Recommended | mapping |
| `FM-MAP-0009` | Recommended | mapping |
| `FM-MAP-0010` | Recommended | mapping |
| `FM-MAP-0011` | Recommended | mapping |
| `FM-MAP-0012` | Recommended | mapping |
| `FM-MAP-0013` | Recommended | mapping |
| `FM-MAP-0014` | Recommended | mapping |
| `FM-MAP-0015` | Recommended | mapping |
| `FM-MAP-0016` | Recommended | mapping |
| `FM-MAP-0017` | Recommended | mapping |
| `FM-MAP-0018` | Recommended | mapping |

### Command / UI / Export – 23 code

| Code | 既定レベル | 分類 |
| --- | --- | --- |
| `FM-CMD-0001` | Recommended | command |
| `FM-CMD-0002` | Recommended | command |
| `FM-CMD-0003` | Recommended | command |
| `FM-CMD-0004` | Recommended | command |
| `FM-UI-0001` | Recommended | ui |
| `FM-UI-0002` | Recommended | ui |
| `FM-UI-0003` | Recommended | ui |
| `FM-UI-0004` | Recommended | ui |
| `FM-UI-0005` | Recommended | ui |
| `FM-UI-INFO` | Info | ui |
| `FM-EXPORT-CREATE-PARENT-FAILED` | Recommended | export |
| `FM-EXPORT-PATH-OCCUPIED` | Recommended | export |
| `FM-EXPORT-SUCCEEDED` | Info | export |
| `FM-EXPORT-WRITE-FAILED` | Recommended | export |
| `FM-EXPORT-NO-TIMELINE` | Recommended | export |
| `FM-EXPORT-INVALID-DURATION` | Recommended | export |
| `FM-EXPORT-INVALID-FRAMERATE` | Recommended | export |
| `FM-EXPORT-INVALID-PATH` | Recommended | export |
| `FM-EXPORT-NULL-TRACK` | Recommended | export |
| `FM-EXPORT-INVALID-BLENDSHAPE` | Recommended | export |
| `FM-EXPORT-INVALID-TRANSFORM` | Recommended | export |
| `FM-EXPORT-UNSUPPORTED-ROTATION` | Recommended | export |
| `FM-EXPORT-INVALID-KEY` | Recommended | export |

### Integration Direct（FM-G）– 22 code

| Code | 既定レベル | 分類 |
| --- | --- | --- |
| `FM-G-AVATAR` | Recommended | integration-direct |
| `FM-G-PREFAB-ASSET` | Recommended | integration-direct |
| `FM-G-CLIP` | Recommended | integration-direct |
| `FM-G-PATH` | Recommended | integration-direct |
| `FM-G-PARAMETER-NAME` | Recommended | integration-direct |
| `FM-G-OUTPUT-CONFLICT` | Recommended | integration-direct |
| `FM-G-PLAN` | Recommended | integration-direct |
| `FM-G-APPLIED` | Info | integration-direct |
| `FM-G-APPLY` | Recommended | integration-direct |
| `FM-G-ROLLBACK` | Recommended | integration-direct |
| `FM-G-OWNERSHIP` | Recommended | integration-direct |
| `FM-G-ROLLED-BACK` | Info | integration-direct |
| `FM-G-PARAMETER-CONFLICT` | Recommended | integration-direct |
| `FM-G-BUDGET` | Recommended | integration-direct |
| `FM-G-MENU-CAPACITY` | Recommended | integration-direct |
| `FM-G-FX` | Recommended | integration-direct |
| `FM-G-ANIMATOR-PARAMETER-CONFLICT` | Recommended | integration-direct |
| `FM-G-LAYER-CONFLICT` | Recommended | integration-direct |
| `FM-G-WRITE-DEFAULTS` | Recommended | integration-direct |
| `FM-G-BINDING-CONFLICT` | Recommended | integration-direct |
| `FM-G-MANIFEST` | Recommended | integration-direct |
| `FM-G-PLAN-UNEXPECTED` | Recommended | integration-direct |

### Integration Modular Avatar（FM-H-MA）– 21 code

| Code | 既定レベル | 分類 |
| --- | --- | --- |
| `FM-H-MA-METADATA-UNAVAILABLE` | Recommended | integration-modular-avatar |
| `FM-H-MA-NOT-INSTALLED` | Recommended | integration-modular-avatar |
| `FM-H-MA-VERSION-UNAVAILABLE` | Recommended | integration-modular-avatar |
| `FM-H-MA-NOT-IMPLEMENTED` | Recommended | integration-modular-avatar |
| `FM-H-MA-MANIFEST` | Recommended | integration-modular-avatar |
| `FM-H-MA-AVATAR` | Recommended | integration-modular-avatar |
| `FM-H-MA-PREFAB-ASSET` | Recommended | integration-modular-avatar |
| `FM-H-MA-CLIP` | Recommended | integration-modular-avatar |
| `FM-H-MA-PATH` | Recommended | integration-modular-avatar |
| `FM-H-MA-PARAMETER-NAME` | Recommended | integration-modular-avatar |
| `FM-H-MA-MANIFEST-CONFLICT` | Recommended | integration-modular-avatar |
| `FM-H-MA-PLAN` | Recommended | integration-modular-avatar |
| `FM-H-MA-DETACHED` | Info | integration-modular-avatar |
| `FM-H-MA-APPLIED` | Info | integration-modular-avatar |
| `FM-H-MA-APPLY` | Recommended | integration-modular-avatar |
| `FM-H-MA-OWNERSHIP` | Recommended | integration-modular-avatar |
| `FM-H-MA-REMOVED` | Info | integration-modular-avatar |
| `FM-H-MA-PARAMETER-CONFLICT` | Recommended | integration-modular-avatar |
| `FM-H-MA-BINDING-CONFLICT` | Recommended | integration-modular-avatar |
| `FM-H-MA-CROSS-BINDING-CONFLICT` | Recommended | integration-modular-avatar |
| `FM-H-MA-PLAN-UNEXPECTED` | Recommended | integration-modular-avatar |

### Integration One-Click（FM-J4）– 10 code

`VRChat 統合` のワンクリックフロー（Export → Plan → Validate → Apply）が返すコードです。停止系は blocking として **対処が必要** に、完了・再利用の通知は **情報** になります。

| Code | 既定レベル | 分類 |
| --- | --- | --- |
| `FM-J4-NO-ANIMATION` | Recommended | integration-one-click |
| `FM-J4-NO-AVATAR` | Recommended | integration-one-click |
| `FM-J4-FOREIGN-CLIP` | Recommended | integration-one-click |
| `FM-J4-BACKEND-UNAVAILABLE` | Recommended | integration-one-click |
| `FM-J4-CROSS-BACKEND` | Info | integration-one-click |
| `FM-J4-EXPORTED` | Info | integration-one-click |
| `FM-J4-REAPPLIED` | Info | integration-one-click |
| `FM-J4-SUCCEEDED` | Info | integration-one-click |
| `FM-J4-APPLY-STOPPED` | Recommended | integration-one-click |
| `FM-J4-NO-PARTIAL-STATE` | Info | integration-one-click |

計 **147** code。全 code の `CanAutoFix` は `false` です。自動修復は行わず、常に safety を優先します。

## 関連ドキュメント

- [Troubleshooting](Troubleshooting.md) – 症状から診断コードへの逆引き
- [VRChat Integration](VRChat-Integration.md) – Direct Integration の安全規則
- [Modular Avatar](Modular-Avatar.md) – MA backend の workflow
