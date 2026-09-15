# Troubleshooting

## 基本

| 症状 | 確認 |
| --- | --- |
| Window が開かない | **Tools/FaceMotion/FaceMotion ウィンドウを開く** を使い、Console の compile error を先に解決します。 |
| Avatar が候補にない | scene 内の active avatar root と `VRCAvatarDescriptor` を確認します。Prefab asset ではなく scene object を選びます。 |
| BlendShape が出ない | avatar を選択し直します。対象 SkinnedMeshRenderer に shared mesh / BlendShape があるか確認します。 |
| Preview が更新されない | FaceMotion Project と Animation、avatar selection、enabled track / key を確認し、**プレビューを再構築** を試します。 |
| Export できない | `Assets` 配下の `.anim` path、positive duration / frame rate、enabled track の binding と key を確認します。親 folder は自動作成されます。 |
| MA backend が出ない | Modular Avatar `1.18.7` が導入済みか確認します。未導入時は正常に unavailable です。 |

## Integration

| 症状 | 確認 |
| --- | --- |
| Direct Apply が block される | Plan の diagnostic を確認します。parameter/binding conflict、menu capacity、expression budget、output path を確認します。restart後のManifest recoveryを使うsceneは、Apply前にsceneを保存します。 |
| `FM-G-WRITE-DEFAULTS` | 既存 FX state の Write Defaults ON が検出されています。FaceMotion は original FX を変更しません。影響を確認した controller で検証してください。 |
| MA Apply 後に動かない | scene を保存し、Build & Test で parameter toggle を確認します。Merge Animator、parameter/menu installer、binding target を確認します。 |
| OFF で戻らない | FaceMotion source AnimationClip の binding が baseline と一致するか確認します。OFF state は avatar の全状態 reset ではありません。 |
| Remove button がない | current UI は MA integration の Remove を提供します。Direct backend の専用 rollback button はありません。managed Direct reapply は既存管理integrationを rollback します。 |
| packageを削除後にFaceMotion assetがMissing Scriptになる | 実測済みの正常動作です。YAMLとAssets内のファイルは保持されます。同じpackageを再導入してassetを再認識させます。package未導入中にassetを削除・上書きしないでください。 |
| Direct rollback後に同名で再適用できない | generated folderにforeign assetが残っている場合、`FM-G-OUTPUT-CONFLICT`で安全にblockされます。foreign assetを整理するか別名で適用してください。 |

## Diagnostic categories

- `FM-G-*`: Direct Integration の plan/apply safety（例: `FM-G-WRITE-DEFAULTS`）
- `FM-H-MA-*`: Modular Avatar backend の optional dependency または integration safety
- `FM-MIG-*`: 保存データ schema / migration。future schema は安全のため block されます。
- `FM-OWNERSHIP-*`: generated asset / hierarchy の ownership が tampered または ambiguous です。自動修復せず block します。

Diagnostic の Suggested Fix を確認し、問題が不明な場合は scene と generated folder を backup してから操作してください。

## Unity 6 を誤って開いた場合

Unity 6 は unsupported です。conversion を続行したり上書き保存したりせず、project を close します。version control または backup から project files を確認・restore し、Unity `2022.3.22f1` で開き直します。復旧後に `Library` を再生成してください。状況が不明なまま asset を削除・再importしないでください。

## FaceEmo との共存

FaceMotion は FaceEmo 専用の automatic integration を提供しません。既存 FX と binding / parameter の conflict は Plan で検出対象ですが、automatic merge 互換性を保証しません。copy または検証用 avatar で確認してから本番 avatar に Apply してください。
