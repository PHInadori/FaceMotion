using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Localization
{
    public static partial class FaceMotionDiagnosticLocalizationCatalog
    {
        static partial void RegisterModularAvatarTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en)
        {
            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarMetadataUnavailable,
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatarの情報を取得できません",
                    Summary = "Modular Avatar連携に必要なバージョン情報が取得できません。",
                    Cause = "パッケージ情報が読み込めない可能性があります。",
                    Impact = "Modular Avatar連携を実行できません。",
                    Resolution = "VCCでModular Avatarパッケージの状態を確認してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar metadata unavailable",
                    Summary = "Version information required for Modular Avatar integration is unavailable.",
                    Cause = "The package metadata may not be readable.",
                    Impact = "Modular Avatar integration cannot run.",
                    Resolution = "Check the Modular Avatar package state in VCC.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarNotInstalled,
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatarがインストールされていません",
                    Summary = "Modular Avatarパッケージがこのプロジェクトにインストールされていません。",
                    Cause = "Modular Avatarが未導入です。",
                    Impact = "Modular Avatar連携の機能は無効になります。",
                    Resolution = "VCCからプロジェクトへModular Avatarを追加してください。",
                    Caution = "Modular Avatarが無くても、FaceMotionはAnimationClip出力で動作します。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar is not installed",
                    Summary = "The Modular Avatar package is not installed in this project.",
                    Cause = "Modular Avatar has not been added.",
                    Impact = "Modular Avatar integration features are disabled.",
                    Resolution = "Add Modular Avatar to the project through VCC.",
                    Caution = "FaceMotion still works via AnimationClip output without Modular Avatar."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarVersionUnavailable,
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatarのバージョンを確認できません",
                    Summary = "Modular Avatarのバージョン情報が取得できませんでした。",
                    Cause = "パッケージ情報が読めない、または未対応の状態です。",
                    Impact = "互換確認ができないため、連携機能を停止しました。",
                    Resolution = "Modular Avatarを最新版へ更新してから再度お試しください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar version unavailable",
                    Summary = "The Modular Avatar version could not be determined.",
                    Cause = "The package info is unreadable or unsupported.",
                    Impact = "Compatibility cannot be confirmed, so integration was stopped.",
                    Resolution = "Update Modular Avatar to the latest version and retry.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarNotImplemented,
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar連携は未実装です",
                    Summary = "この操作のModular Avatar連携は、まだ実装されていません。",
                    Cause = "対応していない操作が選択されました。",
                    Impact = "その操作の連携は行われません。",
                    Resolution = "対応している操作（アバター統合）をご利用ください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar integration not implemented",
                    Summary = "Modular Avatar integration for this operation is not implemented yet.",
                    Cause = "An unsupported operation was selected.",
                    Impact = "No integration is performed for that operation.",
                    Resolution = "Use a supported operation, such as avatar integration.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarManifest,
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar連携のマニフェストが不正です",
                    Summary = "Modular Avatar連携結果を管理するマニフェストが欠落・不正です。",
                    Cause = "マニフェストが存在しない、または読み込めません。",
                    Impact = "連携結果の追跡・解除ができません。",
                    Resolution = "プロジェクトを再検証し、マニフェストを再作成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar manifest is invalid",
                    Summary = "The manifest that tracks Modular Avatar integration is missing or invalid.",
                    Cause = "The manifest does not exist or cannot be read.",
                    Impact = "The integration cannot be tracked or detached.",
                    Resolution = "Re-validate the project to recreate the manifest.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarAvatar,
                new DiagnosticLocalizedFields
                {
                    Title = "統合対象のアバターがありません",
                    Summary = "Modular Avatar統合の対象アバターが指定されていません。",
                    Cause = "アバターが未設定（null）の状態で操作が実行されました。",
                    Impact = "統合アセットを生成できません。",
                    Resolution = "対象アバターを選択してから操作してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "No avatar for integration",
                    Summary = "No avatar is assigned for Modular Avatar integration.",
                    Cause = "The operation ran with a null avatar.",
                    Impact = "The integration asset cannot be generated.",
                    Resolution = "Select the destination avatar before operating.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarPrefabAsset,
                new DiagnosticLocalizedFields
                {
                    Title = "出力先のPrefabアセットがありません",
                    Summary = "Modular Avatar統合の書き込み先アセットが指定されていません。",
                    Cause = "アセットが未設定（null）の状態で操作が実行されました。",
                    Impact = "統合アセットを生成できません。",
                    Resolution = "書き込み先のPrefabを指定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "No prefab asset for output",
                    Summary = "No asset is assigned to receive the Modular Avatar integration.",
                    Cause = "The operation ran with a null asset.",
                    Impact = "The integration asset cannot be generated.",
                    Resolution = "Assign the destination prefab.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarClip,
                new DiagnosticLocalizedFields
                {
                    Title = "生成対象のClipがありません",
                    Summary = "統合するAnimationClip（データ源）が指定されていません。",
                    Cause = "クリップが未設定（null）の状態で操作が実行されました。",
                    Impact = "統合アセットを生成できません。",
                    Resolution = "クリップを作成してから操作してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "No clip to integrate",
                    Summary = "No AnimationClip source is assigned for integration.",
                    Cause = "The operation ran with a null clip.",
                    Impact = "The integration asset cannot be generated.",
                    Resolution = "Create the clip before operating.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarPath,
                new DiagnosticLocalizedFields
                {
                    Title = "出力先のパスが空です",
                    Summary = "Modular Avatar統合の出力先パスが指定されていません。",
                    Cause = "パスが空の状態で操作が実行されました。",
                    Impact = "統合アセットを生成できません。",
                    Resolution = "書き込み先のパスを設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Output path is empty",
                    Summary = "No destination path was provided for the Modular Avatar integration.",
                    Cause = "The operation ran with an empty path.",
                    Impact = "The integration asset cannot be generated.",
                    Resolution = "Set a destination path.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarParameterName,
                new DiagnosticLocalizedFields
                {
                    Title = "パラメーター名が空です",
                    Summary = "統合に必要なパラメーター名が指定されていません。",
                    Cause = "パラメーター名が空の状態で操作が実行されました。",
                    Impact = "パラメーターを登録できません。",
                    Resolution = "有効なパラメーター名を設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Parameter name is empty",
                    Summary = "The parameter name required for integration is not provided.",
                    Cause = "The operation ran with an empty parameter name.",
                    Impact = "The parameter cannot be registered.",
                    Resolution = "Set a valid parameter name.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarManifestConflict,
                new DiagnosticLocalizedFields
                {
                    Title = "マニフェストが競合しています",
                    Summary = "既存の統合マニフェストと競合し、上書きできません。",
                    Cause = "出力先に未対応のマニフェストが既に存在します。",
                    Impact = "上書きせず、操作を中止しました。",
                    Resolution = "既存の統合状態を解除してから、もう一度操作してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Manifest conflict",
                    Summary = "The operation conflicts with an existing integration manifest and cannot overwrite it.",
                    Cause = "An unsupported manifest already exists at the destination.",
                    Impact = "The operation was cancelled without overwriting.",
                    Resolution = "Detach the existing integration, then retry.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarPlan,
                new DiagnosticLocalizedFields
                {
                    Title = "統合計画を構築できませんでした",
                    Summary = "Modular Avatar統合の計画（プラン）を構築できませんでした。",
                    Cause = "入力データが不整合な可能性があります。",
                    Impact = "統合アセットを生成できません。",
                    Resolution = "入力を確認してから、もう一度操作してください。",
                    Caution = "詳細メッセージで原因を確認できます。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Could not build the integration plan",
                    Summary = "The Modular Avatar integration plan could not be built.",
                    Cause = "The input data may be inconsistent.",
                    Impact = "The integration asset cannot be generated.",
                    Resolution = "Check the inputs and retry.",
                    Caution = "See the detail message for the cause."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarDetached,
                new DiagnosticLocalizedFields
                {
                    Title = "統合を解除しました",
                    Summary = "Modular Avatar統合の適用状態を解除（分離）しました。",
                    Cause = "解除操作が正常に完了しました。",
                    Impact = "アセットは統合前の状態に戻りました。",
                    Resolution = "特に操作は不要です。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Integration detached",
                    Summary = "The applied Modular Avatar integration was detached.",
                    Cause = "The detach operation completed successfully.",
                    Impact = "The asset is back to its pre-integration state.",
                    Resolution = "No action is required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarApplied,
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar統合を適用しました",
                    Summary = "Modular Avatar統合の生成が完了し、適用されました。",
                    Cause = "統合処理が正常に完了しました。",
                    Impact = "アバター上で統合の準備が整いました。",
                    Resolution = "アップロード前にアバターの動作をご確認ください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar integration applied",
                    Summary = "The Modular Avatar integration was generated and applied successfully.",
                    Cause = "Integration completed successfully.",
                    Impact = "The avatar is set up for the integration.",
                    Resolution = "Verify the avatar behavior before uploading.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarApply,
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar統合の適用に失敗しました",
                    Summary = "Modular Avatar統合の生成・適用に失敗しました。",
                    Cause = "出力先や入力データに問題がある可能性があります。",
                    Impact = "統合結果は更新されていません。",
                    Resolution = "詳細メッセージを確認し、状態を修正してから再実行してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Failed to apply the Modular Avatar integration",
                    Summary = "Generating and applying the Modular Avatar integration failed.",
                    Cause = "The destination or input data is problematic.",
                    Impact = "The integration result was not updated.",
                    Resolution = "Check the detail message, fix the state, and re-run.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarOwnership,
                new DiagnosticLocalizedFields
                {
                    Title = "所有権を確認できません",
                    Summary = "統合対象のアバターが、以前の統合プロジェクトと一致しません。",
                    Cause = "アバターが別のプロジェクトに所有されている、または未スキャンです。",
                    Impact = "安全に上書き・解除できないため、操作を中止しました。",
                    Resolution = "アバターを再スキャンして所有権を確認してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Ownership could not be confirmed",
                    Summary = "The avatar does not match the previous integration project.",
                    Cause = "The avatar is owned by another project or has not been scanned.",
                    Impact = "Safe overwrite/detach is not possible, so the operation was cancelled.",
                    Resolution = "Rescan the avatar to confirm ownership.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarRemoved,
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar統合を削除しました",
                    Summary = "Modular Avatar統合の適用結果を削除しました。",
                    Cause = "削除操作が正常に完了しました。",
                    Impact = "統合されたアセットが取り除かれました。",
                    Resolution = "特に操作は不要です。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Modular Avatar integration removed",
                    Summary = "The applied Modular Avatar integration was removed.",
                    Cause = "The removal operation completed successfully.",
                    Impact = "The integrated assets were removed.",
                    Resolution = "No action is required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarParameterConflict,
                new DiagnosticLocalizedFields
                {
                    Title = "パラメーター名が競合しています",
                    Summary = "統合により追加しようとするパラメーターが、既存と競合しています。",
                    Cause = "同じ名前が別の用途で使われています。",
                    Impact = "競合するパラメーターは追加されません。",
                    Resolution = "既存と重複しないパラメーター名を設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Parameter name conflicts",
                    Summary = "A parameter the integration would add conflicts with an existing one.",
                    Cause = "The same name is already used for another purpose.",
                    Impact = "The conflicting parameter is not added.",
                    Resolution = "Use a name that does not duplicate an existing parameter.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarBindingConflict,
                new DiagnosticLocalizedFields
                {
                    Title = "バインドが競合しています",
                    Summary = "統合結果の内で、同一対象へのバインドが競合しています。",
                    Cause = "複数の統合先が同じブレンドシェイプ等を指しています。",
                    Impact = "一部の統合結果が適用されない可能性があります。",
                    Resolution = "競合するバインドを整理してから再実行してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Binding conflict",
                    Summary = "Multiple integration targets bind to the same element.",
                    Cause = "Several destinations point to the same blend shape or object.",
                    Impact = "Some integration results may not be applied.",
                    Resolution = "Resolve the conflicting bindings and re-run.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ModularAvatarCrossBindingConflict,
                new DiagnosticLocalizedFields
                {
                    Title = "バインドが複数メッシュで競合しています",
                    Summary = "複数のメッシュ（スキンメッシュ）をまたいで、同じ名前のBlendShapeへバインドが集中しています。",
                    Cause = "異なるメッシュの同名BlendShapeにバインドが設定されています。",
                    Impact = "どのメッシュへのバインドかが曖昧で、動作が不安定な可能性があります。",
                    Resolution = "バインド先のメッシュを明確にし、競合を解消してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Cross-mesh binding conflict",
                    Summary = "Bindings across multiple skinned meshes target identically named blend shapes.",
                    Cause = "Bindings are set on same-named blend shapes of different meshes.",
                    Impact = "Which mesh is bound is ambiguous, and behavior may be unstable.",
                    Resolution = "Make the bound mesh explicit and resolve the conflict.",
                    Caution = ""
                });
        }
    }
}