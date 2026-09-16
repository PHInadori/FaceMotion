using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Localization
{
    public static partial class FaceMotionDiagnosticLocalizationCatalog
    {
        static partial void RegisterAvatarTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en)
        {
            Add(ja, en, FaceMotionDiagnosticCodes.AvatarRootMissing,
                new DiagnosticLocalizedFields
                {
                    Title = "アバタールートが見つかりません",
                    Summary = "スキャン対象のアバター（ルート）が存在しません。",
                    Cause = "アバターのルートが未選択または無効な状態です。",
                    Impact = "アバターの構造をスキャンできず、以降の操作を続けられません。",
                    Resolution = "Hierarchyでアバターのルートを選択してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Avatar root not found",
                    Summary = "The avatar root to scan does not exist.",
                    Cause = "The avatar root is not selected or is invalid.",
                    Impact = "The avatar structure cannot be scanned.",
                    Resolution = "Select the avatar root in the Hierarchy.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DescriptorMissing,
                new DiagnosticLocalizedFields
                {
                    Title = "VRCAvatarDescriptorが見つかりません",
                    Summary = "アバターにVRCAvatarDescriptorが設定されていません。",
                    Cause = "対象がアバターではない、またはタグが未設定です。",
                    Impact = "アバターとしての情報（ルート・人型設定など）を取得できません。",
                    Resolution = "アバターにVRCAvatarDescriptorを設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "VRCAvatarDescriptor not found",
                    Summary = "The avatar has no VRCAvatarDescriptor assigned.",
                    Cause = "The target is not an avatar or the descriptor is missing.",
                    Impact = "Avatar information cannot be obtained.",
                    Resolution = "Add a VRCAvatarDescriptor to the avatar.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InactiveAvatarRoot,
                new DiagnosticLocalizedFields
                {
                    Title = "アバタールートが非アクティブです",
                    Summary = "アバターのルートが非アクティブのため、スキャン結果は暫定的なものになります。",
                    Cause = "アバタールートのGameObjectが無効化されています。",
                    Impact = "非アクティブな構造は含まれず、結果が不完全な可能性があります。",
                    Resolution = "アバタールートをアクティブにして再スキャンしてください。",
                    Caution = "非アクティブのままでも操作はできますが、結果を仮扱いします。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Avatar root is inactive",
                    Summary = "The avatar root is inactive, so the scan result is provisional.",
                    Cause = "The avatar root GameObject is inactive.",
                    Impact = "Inactive parts may be excluded and the result may be incomplete.",
                    Resolution = "Activate the avatar root and rescan.",
                    Caution = "Operations are still allowed, but the result is treated as provisional."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.MissingAnimator,
                new DiagnosticLocalizedFields
                {
                    Title = "Animatorがありません",
                    Summary = "アバターにAnimatorコンポーネントがありません。",
                    Cause = "アバタールートにAnimatorが設定されていません。",
                    Impact = "表情アニメーションを駆動できません。",
                    Resolution = "アバターにAnimatorコンポーネントを追加してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Missing Animator",
                    Summary = "The avatar has no Animator component.",
                    Cause = "No Animator is assigned to the avatar root.",
                    Impact = "Expression animations cannot be driven.",
                    Resolution = "Add an Animator component to the avatar.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.AnimatorAvatarNull,
                new DiagnosticLocalizedFields
                {
                    Title = "AnimatorにAvatarが未設定です",
                    Summary = "AnimatorコンポーネントにAvatarが割り当てられていません。",
                    Cause = "AnimatorのAvatar欄が空です。",
                    Impact = "アニメーションが正しく適用されない可能性があります。",
                    Resolution = "AnimatorにAvatarを割り当ててください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Animator has no Avatar assigned",
                    Summary = "The Animator component has no Avatar assigned.",
                    Cause = "The Animator's Avatar field is empty.",
                    Impact = "Animations may not apply correctly.",
                    Resolution = "Assign an Avatar to the Animator.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NonHumanoidAvatar,
                new DiagnosticLocalizedFields
                {
                    Title = "Humanoidアバターではありません",
                    Summary = "アバターが人型（Humanoid）として設定されていません。",
                    Cause = "AnimatorのAvatarがHumanoid以外です。",
                    Impact = "表情バインディングが制限される可能性があります。",
                    Resolution = "必要に応じてAvatarをHumanoidとして再設定してください。",
                    Caution = "Genericでも操作はできますが、表情系機能が制限されることがあります。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Avatar is not Humanoid",
                    Summary = "The avatar is not configured as a Humanoid.",
                    Cause = "The Animator's Avatar is not Humanoid.",
                    Impact = "Facial binding may be limited.",
                    Resolution = "Reconfigure the Avatar as Humanoid if needed.",
                    Caution = "Generic avatars still work, but facial features may be limited."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateTransformPath,
                new DiagnosticLocalizedFields
                {
                    Title = "同じ名前のオブジェクトが見つかりました",
                    Summary = "アバター内に同じ相対パスを持つオブジェクトが複数あり、FaceMotionが対象を一意に識別できません。",
                    Cause = "同じ親の下に同じ名前のGameObjectが複数あります。",
                    Impact = "対象を安全に特定できないため、編集・Preview・Export・VRChat統合に影響します。",
                    Resolution = "同名GameObjectの片方を一意な名前へ変更してください。",
                    Caution = "アニメーションのバインドパスや他ツールの設定に影響する可能性があります。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Objects with the same name were found",
                    Summary = "Multiple objects share the same relative path on the avatar, so FaceMotion cannot identify a single target.",
                    Cause = "Several GameObjects share both the same parent and the same name.",
                    Impact = "Editing, Preview, Export, and VRChat integration are affected because the target cannot be identified safely.",
                    Resolution = "Rename one of the duplicate GameObjects to a unique name.",
                    Caution = "Animation binding paths or other tools' settings may be affected."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.RendererSharedMeshMissing,
                new DiagnosticLocalizedFields
                {
                    Title = "Rendererにメッシュがありません",
                    Summary = "Skinned Mesh Rendererに共有メッシュが設定されておらず、対象のBlendShapeをバインドできません。",
                    Cause = "RendererのsharedMeshが未設定（null）です。",
                    Impact = "そのRendererのBlendShapeをFaceMotionから操作できません。",
                    Resolution = "Rendererに共有メッシュを設定してください（不要なら無視可）。",
                    Caution = "メッシュが無いRendererはスキャン対象から除外されます。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Renderer has no shared mesh",
                    Summary = "A Skinned Mesh Renderer has no shared mesh, so its blend shapes cannot be bound.",
                    Cause = "The renderer's sharedMesh is null.",
                    Impact = "Blend shapes on that renderer cannot be controlled by FaceMotion.",
                    Resolution = "Assign a shared mesh to the renderer, or ignore this renderer.",
                    Caution = "Renderers without a mesh are excluded from the scan."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateBlendShapeNameInMesh,
                new DiagnosticLocalizedFields
                {
                    Title = "同じ名前のBlendShapeが重複しています",
                    Summary = "1つのメッシュ内に同じ名前のBlendShapeが複数あり、対象を一意に識別できません。",
                    Cause = "メッシュ内に同名のBlendShapeが複数含まれています。",
                    Impact = "どのBlendShapeを制御するか特定できません。",
                    Resolution = "重複しているBlendShapeの片方の名前を変更してください。",
                    Caution = "既存のアニメーションがBlendShape名で参照している場合は影響に注意してください。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Duplicate blend shape names in one mesh",
                    Summary = "One mesh contains multiple blend shapes with the same name, so the target is ambiguous.",
                    Cause = "The mesh contains duplicate blend shape names.",
                    Impact = "Which blend shape is controlled cannot be determined.",
                    Resolution = "Rename one of the duplicate blend shapes.",
                    Caution = "Existing animations referencing shapes by name may be affected."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.FingerprintFailure,
                new DiagnosticLocalizedFields
                {
                    Title = "アバター指紋の計算に失敗しました",
                    Summary = "アバターの指紋（構造を特定する情報）を計算できませんでした。",
                    Cause = "アバター構造に想定外の状態がある可能性があります。",
                    Impact = "アバターの識別ができず、スキャン結果を利用できません。",
                    Resolution = "アバター構造を確認してから、もう一度スキャンしてください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Failed to compute the avatar fingerprint",
                    Summary = "The avatar fingerprint (structure identity) could not be computed.",
                    Cause = "The avatar structure may contain unexpected elements.",
                    Impact = "The avatar cannot be identified and the scan result is unusable.",
                    Resolution = "Inspect the avatar structure and rescan.",
                    Caution = ""
                });
        }
    }
}