# VRChat Integration

VRChat 統合は `VRChat 統合` panel で行います。通常の複数 Animation 更新は Modular Avatar を使用し、checkbox の選択状態を **VRChatへ反映** で同期します。単一 Animation のワンクリック統合や Direct Integration は `詳細設定` にあります。手動 workflow では、必ず **統合を計画して検証** を実行し、blocking diagnostic がないことを確認してから Apply してください。

| | Direct Integration | Modular Avatar Integration |
| --- | --- | --- |
| Dependency | VRChat SDK のみ | Modular Avatar が任意で必要 |
| Descriptor | FX / menu / parameters 参照を copy-on-write で更新 | Descriptor 参照を直接変更しない |
| 生成物 | FX、menu、parameters、reset clip、manifest | integration root、Merge Animator、Parameters、Menu Installer、generated assets、manifest |
| Remove | current UI に専用 button はない。managed reapply は既存 integration を rollback して再作成 | checkbox から外して **VRChatへ反映**、または明示的な Remove で proven-owned hierarchy / assets / manifest を削除。hierarchy だけを手動で取り外した場合の detached manifest は再適用時に再認識 |

## 選択状態を VRChat 側へ同期（Modular Avatar）

1. Modular Avatar を導入し、scene Avatar と FaceMotion Project を選択します。複数 checkbox の更新には MA backend の選択が必要です（Direct を選んでいた場合は `詳細設定` で切り替えます）。
2. `VRChat 統合` の Animation checkbox で反映したい Animation を選びます。チェック済みは保持または追加、以前に統合してチェックを外した FaceMotion 管理対象は削除の候補です。すべて外すと proven-owned の統合を削除できます。
3. **VRChatへ反映** を押します。FaceMotion は選択状態と管理済みの統合を照合し、追加・削除・維持を計画して検証してから適用します。必要な clip の export と diagnostic もこの操作で処理します。
4. blocking diagnostic がある場合は変更を止めます。FaceMotion 所有でない AnimationClip は上書きされません。成功したら scene を保存し、VRChat SDK の Build & Test で確認します。

### Modular Avatar の管理単位

MA は Animation ごとに FaceMotion-owned integration root と `ModularAvatarIntegrationManifest` を持ちます。再実行時は一致する管理済み対象を維持し、必要な変更だけを適用します。未導入時や Direct backend 選択時には複数 checkbox の **VRChatへ反映** は使用できません。

## 詳細設定の単一 Animation workflow

`詳細設定` の **VRChatへ追加** は現在選択中の単一 Animation のワンクリックフローです（Export → Plan → Validate → Apply）。backend を Direct または MA から選ぶ個別操作、manual export path、AnimationClip、Plan/Validate、Apply も利用できます。複数 checkbox の MA 選択状態同期とは別の入口です。Direct と MA を切り替える場合は cross-backend warning を確認してください。

## Direct workflow（詳細設定）

1. scene Avatar、AnimationClip、`Assets` 配下の output folder を選択します。
2. backend を `直接統合 (Direct)` にします。
3. **統合を計画して検証** を押し、parameter、FX layer、generated asset 名、diagnostic を確認します。
4. **直接統合を適用** を押します。
5. scene を保存し、VRChat SDK の Build & Test で parameter toggle を確認します。

Direct は FX、Expression Parameters、Expressions Menu を clone して生成物を追加し、最後に AvatarDescriptor の参照を更新します。parameter name conflict、binding conflict、menu capacity、256-bit expression budget、asset path などの問題は Apply 前に block されます。

### Write Defaults

既存 FX の state に Write Defaults ON が一つでもあると `FM-G-WRITE-DEFAULTS` で Direct Apply を block します。FaceMotion は original FX を強制変更しません。Write Defaults を OFF にする影響を理解できない場合は、copy/controller または検証用 avatar で先に確認してください。

## 旧 Direct Batch 互換性

0.3.0 以降の旧 Direct Batch 公開 API（`DirectVRChatIntegration.PlanBatch` / `ApplyBatch` / `RollbackBatch`）と `DirectBatchIntegrationManifest` は互換性のため保持しています。旧 ApplyBatch を使った project には `<outputFolder>/FaceMotion_Batch/BatchManifest.asset` が存在する場合があります。保存済み manifest の読み込み、FaceMotion-owned clip / reset clip の所有判定、明示的な rollback を引き続きサポートします。現行 MA backend も旧 Direct Batch の所有情報を参照して binding conflict を判断します。これは現在の初心者向け checkbox workflow ではなく、通常の利用者が旧 API や manifest を手動で作成する必要はありません。

## Reset と rollback

Direct / MA の Apply は avatar baseline を capture し、OFF state 用の `Reset.anim` を生成します。OFF state は FaceMotion source AnimationClip の binding を baseline へ戻します。avatar の全 component / 全 parameter を完全 reset する意味ではありません。

Direct managed reapply は、前回 FaceMotion が管理した integration を rollback してから新しい生成物を適用します。rollback は original Descriptor references を戻し、FaceMotion-owned と確認できる生成物だけを削除します。foreign content がある generated folder は保持されます。

## Ownership safety

FaceMotion は manifest で所有を確認できる asset だけを自動削除対象にします。generated folder に利用者が追加した asset は foreign content として保持します。ownership が tampered または ambiguous な場合、FaceMotion は自動修復せず diagnostic を出して block します。

Apply 前に backup / version control を確保し、Plan の diagnostic と生成先 folder を確認してください。

MA-specific workflow は [Modular Avatar](Modular-Avatar.md) を参照してください。
