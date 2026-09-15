# Installation

## Requirements

| Component | Contract |
| --- | --- |
| Unity | `2022.3.22f1` |
| VRChat SDK - Avatars | `3.10.5` |
| Modular Avatar | 任意、verified `1.18.7` |
| NDMF | MA の推移的依存、verified `1.14.8` |
| Unity 6 | unsupported |

FaceMotion の必須 VPM dependency は `com.vrchat.avatars` `3.10.5` だけです。lilToon は dependency ではありません。

## VCC / ALCOM

公開後は FaceMotion の public custom VPM repository URL を VCC / ALCOM に追加し、`FaceMotion` を導入します。`PHInadori-VPM` と `com.phinadori.vpm` は planned listing identity ですが、repository / Pages はまだ公開されていません。未公開の URL を VCC / ALCOM に追加しないでください。

## Manual package install

公開前の開発・検証用途では、VRChat SDK 導入済み project の Package Manager から **Add package from disk** を選び、FaceMotion folder 内の `package.json` を指定できます。これは local package workflow です。

Git URL install は public GitHub repository が作成されるまで提供しません。公開済みでない URL を使った install 手順はありません。

## Optional Modular Avatar

Modular Avatar を導入すると `Modular Avatar (任意)` backend が有効になります。未導入の場合も FaceMotion は compile し、Direct Integration、export、preview、main tests を使用できます。

## Uninstall and reinstall

FaceMotion package を削除しても、`Assets` 内の FaceMotion Project、Mapping Profile、exported AnimationClip、Direct / Modular Avatar の generated integration assets は自動削除されません。package uninstall は user asset cleanup を行いません。

Cleanup が必要な場合は package uninstall **前**に Direct integration の rollback、または MA integration の Remove を行ってください。generated folder に foreign asset がある場合、Direct rollback はそのfolderを残します。同名の再適用は安全のためblockされるため、foreign assetを整理するか別名を使います。

同じpackageを再導入すると、I.7 validation では FaceMotion Project、Mapping Profile、Direct / MA Manifest、generated asset reference が再認識されました。package未導入中のFaceMotion ScriptableObject assetは YAMLを保持したMissing Script状態となり、再導入後に解決します。詳細は [Uninstall and Update](Uninstall-Update.md) を参照してください。

## Unity 6

Unity 6 は unsupported です。`2022.3.22f1` 以外で project conversion を行わないでください。誤って Unity 6 で開いた場合は、すぐ close し、version control または backup から `ProjectSettings` / project files を確認・restore してください。復旧後は `Library` を再生成します。状況が不明な場合は、上書き保存より先に backup を確保してください。
