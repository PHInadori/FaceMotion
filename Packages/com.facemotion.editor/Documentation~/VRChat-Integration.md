# VRChat Integration

VRChat 統合は `VRChat 統合` panel で行います。必ず **統合を計画して検証** を実行し、blocking diagnostic がないことを確認してから Apply してください。

| | Direct Integration | Modular Avatar Integration |
| --- | --- | --- |
| Dependency | VRChat SDK のみ | Modular Avatar が任意で必要 |
| Descriptor | FX / menu / parameters 参照を copy-on-write で更新 | Descriptor 参照を直接変更しない |
| 生成物 | FX、menu、parameters、reset clip、manifest | integration root、Merge Animator、Parameters、Menu Installer、generated assets、manifest |
| Remove | current UI に専用 button はない。managed reapply は既存 integration を rollback して再作成 | Remove button が hierarchy を detach し、assets / manifest は保持 |

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
