# Uninstall and Update

## Uninstall

I.7 validation では、FaceMotion packageだけを削除しても `Assets` 内の次のファイルは削除されませんでした。

- `FaceMotionProject.asset` と `AvatarMappingProfile.asset`
- exported `AnimationClip`
- Direct integration の generated controller/menu/parameter/reset clip/Manifest
- Modular Avatar integration の generated controller/menu/reset clip/Manifest
- generated folder内のforeign asset

package未導入中は、FaceMotion ScriptableObject asset（Project、Mapping Profile、Direct Manifest、MA Manifest）はYAMLを保持しますが、UnityではMissing Scriptになります。exported AnimationClip、generated controller/menu/reset clip、およびMA component hierarchyは保持されます。package uninstallはuser asset cleanupを行いません。

Cleanupする場合はpackage uninstall前に実行します。

- Direct: rollbackしてFaceMotion-owned assetだけを削除します。foreign assetがあればfolderは残ります。
- MA: RemoveしてFaceMotion hierarchy rootだけを削除します。Manifestとgenerated assetは保持されます。

## Reinstall

同じpackageを再導入したI.7 validationでは、ProjectId、Mapping Profile、Direct/MA Manifest、schema、generated asset referenceが再認識されました。Direct Manifestは保存済みsceneのAvatarをGlobalObjectIdで再解決し、再導入後のrollback/reapplyを実行できました。MA ManifestはAttached判定後にRemove/Reapplyでき、同じgenerated asset folderとforeign assetを保持しました。

Modular Avatarは任意dependencyです。MAなしでもCore/UI/Directは利用でき、MA/NDMF関連assemblyはロードされません。MAを後から導入するとMA backendが有効になります。

## Update and downgrade

現在サポートされるlegacy schemaはuse boundaryでautomatic migrationされます。migration済みassetのsecond passはno-opです。future schemaは安全のためblockされ、assetをdowngrade/mutateしません。

古いpackage versionへのdowngrade互換性は保証しません。downgrade前にprojectとgenerated integration folderをbackupし、future schemaのdiagnosticが出た場合は新しいpackageを使用してください。
