# Modular Avatar Integration

Modular Avatar backend は任意です。`nadena.dev.modular-avatar` `1.18.7` が導入されている場合にだけ `Modular Avatar (任意)` を選択できます。複数 checkbox の **VRChatへ反映** には MA backend が必要です。未導入なら `詳細設定` の Direct Integration を使用できます。

## Workflow

1. Modular Avatar を project に導入します。
2. FaceMotion の `VRChat 統合` で Avatar と FaceMotion Project を選択し、反映したい Animation に checkbox を付けます。Direct backend を選んでいた場合は `詳細設定` で MA に切り替えます。通常は AnimationClip と output folder の手動選択は不要です。
3. **VRChatへ反映** を押します。選択状態を既存の FaceMotion 管理対象と照合し、追加・維持・削除を検証してから実行します。チェックを外した既存の管理対象も削除候補になります。
4. diagnostic を確認し、scene を保存して VRChat SDK の Build & Test と parameter toggle で確認します。
5. 単一 Animation の **VRChatへ追加** や手動の **統合を計画して検証** / **Modular Avatar 統合を適用** は `詳細設定` から使用できます。
6. 不要になった統合は checkbox を外して **VRChatへ反映** するか、明示的な **Modular Avatar 統合を削除** で取り除けます。
7. hierarchy だけを手動で取り外した場合は、残された manifest / generated assets を所有確認したうえで再適用時に再接続できます。

複数 Animation の選択状態を **VRChatへ反映** で同期します。MA の統合は Animation ごとに manifest を持つ integration root を作成し、再実行時は一致する管理済み item を保持して必要な変更だけを適用します。これは旧 Direct Batch の manifest とは別の資産です。

## 生成物と Remove

Apply は avatar root の下に FaceMotion-owned integration root を作り、MA Merge Animator、Parameters、Menu Installer を追加します。avatar root 基準の binding を維持して animation を接続します。Descriptor の FX / menu / parameter 参照は直接変更しません。

checkbox の解除と **VRChatへ反映**、または明示的な **Modular Avatar 統合を削除** は、所有を確認した hierarchy、generated assets、manifest を削除します。一方、利用者が hierarchy だけを手動で取り外した detached 状態では assets と manifest が残り、所有を検証できれば再適用時に再接続できます。

tampered / ambiguous ownership、binding conflict、invalid output folder などは Apply 前に block されます。手動で hierarchy を取り外した detached 状態の generated asset を削除する前に、backup と manifest の状態を確認してください。
