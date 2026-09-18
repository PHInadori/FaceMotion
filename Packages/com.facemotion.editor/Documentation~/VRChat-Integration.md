# VRChat Integration

VRChat 統合は `VRChat 統合` panel で行います。通常は **VRChatへ追加** のワンクリックフローを使います。詳細な手動 workflow では、必ず **統合を計画して検証** を実行し、blocking diagnostic がないことを確認してから Apply してください。

| | Direct Integration | Modular Avatar Integration |
| --- | --- | --- |
| Dependency | VRChat SDK のみ | Modular Avatar が任意で必要 |
| Descriptor | FX / menu / parameters 参照を copy-on-write で更新 | Descriptor 参照を直接変更しない |
| 生成物 | FX、menu、parameters、reset clip、manifest | integration root、Merge Animator、Parameters、Menu Installer、generated assets、manifest |
| Remove | current UI に専用 button はない。managed reapply は既存 integration を rollback して再作成 | Remove button が hierarchy を detach し、assets / manifest は保持 |

## One-click workflow

1. scene Avatar と FaceMotion Animation を選択します。
2. **VRChatへ追加** を押します。Modular Avatar が導入済みなら MA backend が既定で選ばれます。project 単位の明示選択は優先されます。
3. FaceMotion は Export → Plan → Validate → Apply を順に実行し、progress と diagnostic を表示します。
4. blocking diagnostic が出た場合は Apply せず停止します。FaceMotion 所有でない AnimationClip は上書きされません。
5. 成功時は backend と animation 名を確認し、scene を保存して Build & Test します。

再実行は owned AnimationClip の GUID と既存 managed integration を再利用します。Direct と MA を切り替える場合は cross-backend warning を確認してください。`詳細設定` foldout には manual export path、AnimationClip、backend、Plan/Validate、Apply の workflow が残されています。

## Batch Integration

複数の Animation をまとめて一括統合できます。Animation list で複数 Animation の checkbox を選択し、`VRChat 統合` panel から batch 統合を実行します。

- Batch Export: 選択した Animation をそれぞれ output path へ export します。複数 Animation が同じ export path に解決される場合は block します。
- Batch Plan / Apply: 選択した Animation ごとに Direct または MA の integration を適用します。
- Direct batch は copy-on-write の asset set を 1 組作成し、失敗時は全体を rollback します。
- MA batch は Animation ごとに manifest を持つ integration root を作成し、再実行は既存の matching item に idempotent に再適用します。
- 各 Animation の結果 summary と、部分失敗時の diagnostic を表示します。parameter 名は Animation 名から生成されるため、一意で 256 文字以下である必要があります。

## Direct workflow

1. scene Avatar、AnimationClip、`Assets` 配下の output folder を選択します。
2. backend を `直接統合 (Direct)` にします。
3. **統合を計画して検証** を押し、parameter、FX layer、generated asset 名、diagnostic を確認します。
4. **直接統合を適用** を押します。
5. scene を保存し、VRChat SDK の Build & Test で parameter toggle を確認します。

Direct は FX、Expression Parameters、Expressions Menu を clone して生成物を追加し、最後に AvatarDescriptor の参照を更新します。parameter name conflict、binding conflict、menu capacity、256-bit expression budget、asset path などの問題は Apply 前に block されます。

### Write Defaults

既存 FX の state に Write Defaults ON が一つでもあると `FM-G-WRITE-DEFAULTS` で Direct Apply を block します。FaceMotion は original FX を強制変更しません。Write Defaults を OFF にする影響を理解できない場合は、copy/controller または検証用 avatar で先に確認してください。

## Reset と rollback

Direct / MA の Apply は avatar baseline を capture し、OFF state 用の `Reset.anim` を生成します。OFF state は FaceMotion source AnimationClip の binding を baseline へ戻します。avatar の全 component / 全 parameter を完全 reset する意味ではありません。

Direct managed reapply は、前回 FaceMotion が管理した integration を rollback してから新しい生成物を適用します。rollback は original Descriptor references を戻し、FaceMotion-owned と確認できる生成物だけを削除します。foreign content がある generated folder は保持されます。

## Ownership safety

FaceMotion は manifest で所有を確認できる asset だけを自動削除対象にします。generated folder に利用者が追加した asset は foreign content として保持します。ownership が tampered または ambiguous な場合、FaceMotion は自動修復せず diagnostic を出して block します。

Apply 前に backup / version control を確保し、Plan の diagnostic と生成先 folder を確認してください。

MA-specific workflow は [Modular Avatar](Modular-Avatar.md) を参照してください。
