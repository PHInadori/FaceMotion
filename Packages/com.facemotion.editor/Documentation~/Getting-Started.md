# Getting Started

## 必要環境

Unity `2022.3.22f1` と VRChat SDK - Avatars `3.10.5` が必要です。Modular Avatar は任意です。Unity 6 でこの project を開いたり conversion したりしないでください。詳細は [Installation](Installation.md) を参照してください。

## 最短の成功ルート

1. scene 内の VRChat avatar を開き、`VRCAvatarDescriptor` がある avatar root を選択します。
2. **Tools/FaceMotion/FaceMotion ウィンドウを開く** を選択します。
3. `プロジェクト` の **新規プロジェクト** で FaceMotion Project asset を作成し、`アニメーション` の **+ 新規アニメーション** で Animation を作成します。
4. avatar を選択した状態で `トラック` の **+ ブレンドシェイプ** または **+ トランスフォーム** を選びます。BlendShape browser のカテゴリ、検索、conflict 表示を確認し、必要なら `詳細な手動バインディング` を使います。
5. timeline の再生ヘッドを移動し、Keyframe を追加・編集します。`スナップ` を有効にすると current frame rate に丸められます。
6. `プレビュー` で **プレビューを開始** し、play / pause / stop、loop、scrub で確認します。Preview は scene を自動変更しません。Alt+左 drag で orbit、middle drag で pan、wheel または Alt+右 drag で zoom、F で focus します。
7. `AnimationClip エクスポート` で `Assets` 配下へ `.anim` を手動出力できます。通常の VRChat 統合ではこの手順は不要です。
8. `VRChat 統合` の **VRChatへ追加** は Export → Plan → Validate → Apply を順に実行します。blocking diagnostic がないことを確認します。詳細な手動設定では backend、AnimationClip、出力フォルダーを選び、**統合を計画して検証** してから Apply します。
9. scene を保存し、VRChat SDK の **Build & Test** で parameter toggle と OFF state を確認します。

## 次に読む文書

- 編集操作: [User Guide](User-Guide.md)
- export: [Export](Export.md)
- Direct Integration: [VRChat Integration](VRChat-Integration.md)
- Modular Avatar: [Modular Avatar](Modular-Avatar.md)
