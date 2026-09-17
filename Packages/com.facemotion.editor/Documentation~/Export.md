# AnimationClip Export

`AnimationClip エクスポート` は選択中の FaceMotion Animation を Unity の `.anim` へ出力します。

`VRChat 統合` の **VRChatへ追加** は、通常この export を自動で実行します。ここに記載する手順は clip を手動で確認・利用したい場合の workflow です。

## 手順

1. FaceMotion Project と Animation を選択します。
2. `AnimationClip エクスポート` で保存先を選びます。
3. `Assets` 配下の `.anim` path を指定して export します。
4. 出力 clip を Preview または VRChat Integration で使います。

## 制約と挙動

- output path は `Assets` 配下に限られます。`Packages` 配下には export できません。
- 親 folder がなければ作成されます。
- duration と frame rate は finite かつ positive である必要があります。
- enabled BlendShape track には binding name と key、Transform track には supported binding と key が必要です。
- BlendShape と Transform curve（Position / Rotation / Scale）を出力します。
- export は timeline の frame rate で sample し、loop 設定を clip へ引き継ぎます。
- FaceMotion が所有する既存の AnimationClip を同じ path に export すると内容を overwrite しますが、asset の `.meta` GUID は維持され、既存 reference を壊しません。
- FaceMotion 所有でない AnimationClip、または AnimationClip 以外の asset が同じ path にある場合は安全のため block します。

`Reset.anim` は VRChat Integration が OFF state 用に生成する asset です。通常の AnimationClip export の出力とは別物です。
