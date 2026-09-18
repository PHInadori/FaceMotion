# Modular Avatar Integration

Modular Avatar backend は任意です。`nadena.dev.modular-avatar` `1.18.7` が導入されている場合にだけ `Modular Avatar (任意)` を選択できます。未導入なら Direct Integration を使用できます。MA が利用可能な場合、ワンクリック統合の既定 backend は MA です。project 単位の明示選択はこの既定より優先されます。

## Workflow

1. Modular Avatar を project に導入します。
2. FaceMotion の `VRChat 統合` で Avatar と Animation を選択し、**VRChatへ追加** を押します。通常は AnimationClip と output folder の手動選択は不要です。
3. backend を `Modular Avatar (任意)` にし、必要なら `詳細設定` から **統合を計画して検証** を実行します。
4. diagnostic を確認して **Modular Avatar 統合を適用** を押すか、ワンクリックフローの Apply 完了を確認します。
5. scene を保存し、VRChat SDK の Build & Test と parameter toggle で確認します。
6. 不要になった場合は **Modular Avatar 統合を削除** を押します。
7. 再適用すると retained manifest / generated assets を検証し、利用可能なら再利用します。

複数 Animation をまとめて統合する場合は、Animation list で複数の checkbox を選択して Batch Integration を使います。MA batch は Animation ごとに manifest を持つ integration root を作成し、再実行時は既存の matching item に idempotent に再適用します。

## 生成物と Remove

Apply は avatar root の下に FaceMotion-owned integration root を作り、MA Merge Animator、Parameters、Menu Installer を追加します。avatar root 基準の binding を維持して animation を接続します。Descriptor の FX / menu / parameter 参照は直接変更しません。

**Modular Avatar 統合を削除** は owned hierarchy を detach します。generated assets と manifest は意図的に保持されます。これにより Reapply は retained assets を再利用でき、duplicate folder を作らないよう ownership を検証します。

tampered / ambiguous ownership、binding conflict、invalid output folder などは Apply 前に block されます。Remove 後も不要な generated asset を手動削除する前に、backup と manifest の状態を確認してください。
