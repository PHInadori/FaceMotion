# FaceMotion

FaceMotion は、VRChat アバター向けの表情・Transform アニメーションを Unity 上で作成する Editor package です。タイムライン編集、分離プレビュー、AnimationClip export、VRChat への Direct Integration、任意の Modular Avatar Integration を提供します。

<!-- Screenshot will be added before public release. Do not add mock or generated UI images. -->

正式な screenshot はまだありません。実画面を正確に示せるまで、mock image や壊れた image link は掲載しません。

## 主な機能

- BlendShape、Position、Rotation、Scale のタイムライン編集
- 分離された Preview と、明示操作による可逆的な Scene Apply
- Smile、Wink、Blink、Angry、Sad、Surprise、Embarrassed の built-in preset
- seed を指定できる deterministic な Blink / Random motion generation
- AnimationClip export
- Direct Integration または任意の Modular Avatar Integration

## 対応環境

- Unity `2022.3.22f1`
- VRChat SDK - Avatars `3.10.5`
- Modular Avatar `1.18.7` は任意（導入時のみ MA backend が有効）
- NDMF `1.14.8` は Modular Avatar の推移的依存
- Unity 6 は未対応
- lilToon は FaceMotion の依存ではありません

## インストール

VCC / ALCOM に `https://phinadori.github.io/PHInadori-VPM/index.json` を追加して FaceMotion を導入できます。ローカル開発では、VRChat SDK が導入済みの Unity project で package folder の `package.json` を Package Manager の **Add package from disk** から選択する方法も利用できます。

詳細: [Installation](Documentation~/Installation.md)

## Quick Start

1. Unity menu **Tools/FaceMotion/FaceMotion ウィンドウを開く** を開きます。
2. scene 内の VRChat avatar を選択し、FaceMotion Project と Animation を作成します。
3. BlendShape または Transform Track を追加し、Keyframe を編集します。
4. Preview で確認し、必要なら AnimationClip を export します。
5. VRChat 統合では Plan の diagnostic を確認してから Direct または Modular Avatar backend を Apply します。
6. scene を保存し、VRChat SDK の Build & Test で動作を確認します。

詳細: [Getting Started](Documentation~/Getting-Started.md)

## Direct と Modular Avatar

| Backend | 選ぶ場面 | 主な動作 |
| --- | --- | --- |
| Direct Integration | MA を使わない構成 | FX / menu / parameters を copy-on-write で生成し、Descriptor 参照を更新 |
| Modular Avatar Integration | MA を使う構成 | Descriptor 参照を直接変更せず、avatar root 配下の MA integration root を生成 |

Direct は既存 FX の Write Defaults が ON の場合、安全のため block します。MA は任意依存であり、未導入でも Direct workflow は使用できます。

詳細: [VRChat Integration](Documentation~/VRChat-Integration.md) / [Modular Avatar](Documentation~/Modular-Avatar.md)

## 安全性と生成アセット

- Preview は scene を自動変更しません。Scene Apply は明示操作で、停止・window close・reload 時に baseline へ戻します。
- Integration 前に Plan の diagnostic を確認してください。
- FaceMotion は manifest で所有を確認できる生成物だけを自動処理します。foreign asset や曖昧な状態は自動修復せず block します。
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
