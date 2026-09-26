# FaceMotion

FaceMotion は、VRChat アバター向けの表情・Transform アニメーションを Unity 上で作成する Editor package です。タイムライン編集、分離プレビュー、AnimationClip export、VRChat への Direct Integration、任意の Modular Avatar Integration を提供します。

<!-- Screenshot will be added before public release. Do not add mock or generated UI images. -->

正式な screenshot はまだありません。実画面を正確に示せるまで、mock image や壊れた image link は掲載しません。

## 主な機能

- BlendShape、Position、Rotation、Scale のタイムライン編集（Ctrl/Cmd+ホイールで zoom）
- 分離された Preview、再生コントロール、scene-style camera、明示操作による可逆的な Scene Apply
- BlendShape browser（カテゴリ分類ボタン、検索、VRChat / MA conflict の事前表示）
- 編集可能な Animation 名（VRChat menu label にもそのまま使用）
- 複数 Animation の一括選択と Batch Integration
- AnimationClip export
- VRChat へのワンクリック統合（Export → Plan → Validate → Apply）、Direct Integration、任意の Modular Avatar Integration
- 次の操作を示す workflow guidance、empty state、tooltip、shortcut help
- Quick Key、Copy / Paste / Duplicate / Nudge による key 作成・編集の高速化

## 対応環境

- Unity `2022.3.22f1`
- VRChat SDK - Avatars `3.10.5`
- Modular Avatar `1.18.7` は任意（導入時のみ MA backend が有効）
- NDMF `1.14.8` は Modular Avatar の推移的依存
- Unity 6 は未対応
- lilToon は FaceMotion の依存ではありません

## インストール

VCC / ALCOM では `https://phinadori.github.io/PHInadori-VPM/index.json` を custom VPM repository として追加し、最新の公開版を導入できます。開発・検証用途では、VRChat SDK が導入済みの Unity project で package folder の `package.json` を Package Manager の **Add package from disk** から選択できます。

詳細: [Installation](Documentation~/Installation.md)

## Quick Start

1. Unity menu **Tools/FaceMotion/FaceMotion ウィンドウを開く** を開きます。
2. scene 内の VRChat avatar を選択し、FaceMotion Project と Animation を作成します。Animation 名は編集でき、VRChat の menu label にそのまま使われます。
3. BlendShape または Transform Track を追加し、Keyframe を編集します。BlendShape Track 追加時は現在のアバターの BlendShape 値を 0 秒の初期 key として自動生成します。
4. Preview の再生コントロールと camera 操作で確認し、必要なら AnimationClip を export します。
5. VRChat 統合では **VRChatへ追加** のワンクリックフローを使うか、複数 Animation をまとめて一括統合（Batch Integration）できます。Plan の diagnostic を確認してから Direct または Modular Avatar backend を Apply します。
6. scene を保存し、VRChat SDK の Build & Test で動作を確認します。

詳細: [Getting Started](Documentation~/Getting-Started.md)

## Key editing

- **Quick Key** は playhead の位置に設定値（0-100、EditorPrefs に保存）で BlendShape key を追加します。同じ位置に key がある場合は更新します。
- **Key Actions** メニュー（Key Inspector）から Copy / Paste / Duplicate / Nudge（±1 / ±5 フレーム）を実行できます。timeline 上では Ctrl/Cmd+A / C / V / D も利用できます。
- Paste は key をコピーした同じ animation 内でのみ有効で、playhead を基準に相対間隔を保ちます。同じ track・時刻に既にある key は value と補間だけを更新します（key identity は維持、重複 timestamp は作られません）。
- 複数 key・複数 track はまとめて移動でき、選択済みの key を modifier なしで drag すると選択全体が移動します。
- Native Shortcut Manager actions（Quick Key / Copy / Paste / Duplicate / Nudge）は既定の binding なしで登録されます。**Edit > Shortcuts** の `FaceMotion/Timeline` から割り当ててください。

## Direct と Modular Avatar

| Backend | 選ぶ場面 | 主な動作 |
| --- | --- | --- |
| Direct Integration | MA を使わない構成 | FX / menu / parameters を copy-on-write で生成し、Descriptor 参照を更新 |
| Modular Avatar Integration | MA を使う構成 | Descriptor 参照を直接変更せず、avatar root 配下の MA integration root を生成 |

Direct は既存 FX の Write Defaults が ON の場合、安全のため block します。MA は任意依存であり、未導入でも Direct workflow は使用できます。MA 導入時は MA backend が既定で優先され、project 単位で変更できます。

詳細: [VRChat Integration](Documentation~/VRChat-Integration.md) / [Modular Avatar](Documentation~/Modular-Avatar.md)

## 安全性と生成アセット

- Preview は scene を自動変更しません。Scene Apply は明示操作で、停止・window close・reload 時に baseline へ戻します。
- Integration 前に Plan の diagnostic を確認してください。
- FaceMotion は manifest で所有を確認できる生成物だけを自動処理します。foreign asset や曖昧な状態は自動修復せず block します。
- ワンクリック統合は FaceMotion 所有でない AnimationClip を上書きせず、再実行時は既存の owned clip GUID を維持します。
- Apply 前には version control または project backup を推奨します。

## Documentation

- [Documentation index](Documentation~/README.md)
- [User Guide](Documentation~/User-Guide.md)
- [Export](Documentation~/Export.md)
- [Troubleshooting](Documentation~/Troubleshooting.md)
- [Compatibility](Documentation~/Compatibility.md)
- [Schema Migration (developer)](Documentation~/Schema-Migration.md)
- [CI (developer)](Documentation~/CI.md)

## License

[MIT](LICENSE.md)

## Author

PHInadori  
Contact: phinadori@gmail.com
