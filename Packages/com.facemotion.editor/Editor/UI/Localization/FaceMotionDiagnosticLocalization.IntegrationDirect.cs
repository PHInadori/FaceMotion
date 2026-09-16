using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Localization
{
    public static partial class FaceMotionDiagnosticLocalizationCatalog
    {
        static partial void RegisterIntegrationDirectTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en)
        {
            Add(ja, en, FaceMotionDiagnosticCodes.GenerationAvatar,
                new DiagnosticLocalizedFields
                {
                    Title = "生成対象のアバターがありません",
                    Summary = "VRChat統合の出力先となるアバターが指定されていません。",
                    Cause = "アバターが未設定（null）の状態で生成が実行されました。",
                    Impact = "統合アセット（Prefab等）を生成できません。",
                    Resolution = "出力先のアバターを選択してから生成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "No avatar for generation",
                    Summary = "No avatar is assigned as the destination for VRChat integration.",
                    Cause = "Generation ran with a null avatar.",
                    Impact = "The integration assets (prefab etc.) cannot be generated.",
                    Resolution = "Assign the destination avatar before generating.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationPrefabAsset,
                new DiagnosticLocalizedFields
                {
                    Title = "出力先のPrefabアセットがありません",
                    Summary = "統合結果を書き込むPrefabアセットが指定されていません。",
                    Cause = "アニメーションアセットが未設定（null）の状態で生成が実行されました。",
                    Impact = "統合アセットを生成できません。",
                    Resolution = "書き込み先のPrefabを生成・指定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "No prefab asset for output",
                    Summary = "No prefab asset is assigned to receive the integration result.",
                    Cause = "Generation ran with a null animation asset.",
                    Impact = "The integration asset cannot be generated.",
                    Resolution = "Create and assign the destination prefab.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationClip,
                new DiagnosticLocalizedFields
                {
                    Title = "生成対象のClipがありません",
                    Summary = "生成するAnimationClip（データ源）が指定されていません。",
                    Cause = "クリップが未設定（null）の状態で生成が実行されました。",
                    Impact = "統合アセットを生成できません。",
                    Resolution = "生成エンジンでクリップを作成してから実行してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "No clip to generate",
                    Summary = "No AnimationClip source is assigned for generation.",
                    Cause = "Generation ran with a null clip.",
                    Impact = "The integration asset cannot be generated.",
                    Resolution = "Create the clip with the generation engine first.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationPath,
                new DiagnosticLocalizedFields
                {
                    Title = "出力先のパスが空です",
                    Summary = "統合アセットの出力先パスが指定されていません。",
                    Cause = "パスが空の状態で生成が実行されました。",
                    Impact = "統合アセットを生成できません。",
                    Resolution = "書き込み先のパスを設定してから実行してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Output path is empty",
                    Summary = "No destination path was provided for the integration asset.",
                    Cause = "Generation ran with an empty path.",
                    Impact = "The integration asset cannot be generated.",
                    Resolution = "Set a destination path before running.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationParameterName,
                new DiagnosticLocalizedFields
                {
                    Title = "パラメーター名が空です",
                    Summary = "生成に必要なパラメーター名が指定されていません。",
                    Cause = "パラメーター名が空の状態で生成が実行されました。",
                    Impact = "パラメーターを登録できません。",
                    Resolution = "有効なパラメーター名を設定してから実行してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Parameter name is empty",
                    Summary = "The parameter name required for generation is not provided.",
                    Cause = "Generation ran with an empty parameter name.",
                    Impact = "The parameter cannot be registered.",
                    Resolution = "Set a valid parameter name before running.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationOutputConflict,
                new DiagnosticLocalizedFields
                {
                    Title = "出力先が他のアセットと競合しています",
                    Summary = "統合アセットの出力先パスが、既に別の用途で使われています。",
                    Cause = "出力先パスに別のアセットが存在する可能性があります。",
                    Impact = "統合アセットを生成できません。",
                    Resolution = "出力先を変更するか、競合しているアセットを確認してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Output path conflicts with another asset",
                    Summary = "The integration output path is already used for another purpose.",
                    Cause = "Another asset may already exist at the destination.",
                    Impact = "The integration asset cannot be generated.",
                    Resolution = "Change the destination, or check the conflicting asset.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationPlan,
                new DiagnosticLocalizedFields
                {
                    Title = "生成計画を構築できませんでした",
                    Summary = "統合アセットの生成計画（プラン）を構築できませんでした。",
                    Cause = "入力データが不整合な可能性があります。",
                    Impact = "統合アセットを生成できません。",
                    Resolution = "入力を確認してから、もう一度生成してください。",
                    Caution = "詳細メッセージで原因を確認できます。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Could not build the generation plan",
                    Summary = "The plan for the integration asset could not be built.",
                    Cause = "The input data may be inconsistent.",
                    Impact = "The integration asset cannot be generated.",
                    Resolution = "Check the inputs and generate again.",
                    Caution = "See the detail message for the cause."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationApplied,
                new DiagnosticLocalizedFields
                {
                    Title = "統合を適用しました",
                    Summary = "VRChat統合アセットの生成が完了し、適用されました。",
                    Cause = "生成処理が正常に完了しました。",
                    Impact = "指定した構成で統合準備が整いました。",
                    Resolution = "特に操作は不要です。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Integration applied",
                    Summary = "The VRChat integration was generated and applied successfully.",
                    Cause = "Generation completed successfully.",
                    Impact = "The integration is set up as specified.",
                    Resolution = "No action is required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationApply,
                new DiagnosticLocalizedFields
                {
                    Title = "統合の適用に失敗しました",
                    Summary = "VRChat統合アセットの生成・適用に失敗しました。",
                    Cause = "出力先や入力データに問題がある可能性があります。",
                    Impact = "統合アセットは更新されていません。",
                    Resolution = "詳細メッセージを確認し、状態を修正してから再実行してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Failed to apply the integration",
                    Summary = "Generating and applying the VRChat integration failed.",
                    Cause = "The destination or input data is problematic.",
                    Impact = "The integration asset was not updated.",
                    Resolution = "Check the detail message, fix the state, and re-run.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationRollback,
                new DiagnosticLocalizedFields
                {
                    Title = "統合のロールバックに失敗しました",
                    Summary = "統合適用の取り消し（ロールバック）に失敗しました。",
                    Cause = "保管された適用前の状態を復元できませんでした。",
                    Impact = "アセットが統合適用済みのままになっている可能性があります。",
                    Resolution = "適用前に戻すには、元の状態を再適用してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Failed to roll back the integration",
                    Summary = "Rolling back the applied integration failed.",
                    Cause = "The stored pre-apply state could not be restored.",
                    Impact = "The asset may still contain the applied result.",
                    Resolution = "Restore the previous state to return to the pre-apply condition.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationOwnership,
                new DiagnosticLocalizedFields
                {
                    Title = "所有権を確認できません",
                    Summary = "統合対象のアバターが、以前の生成プロジェクトと一致しません。",
                    Cause = "アバターが別のプロジェクトに所有されている、または未スキャンです。",
                    Impact = "安全に上書き・分離できないため、操作を中止しました。",
                    Resolution = "アバターを再スキャンして所有権を確認してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Ownership could not be confirmed",
                    Summary = "The avatar does not match the previous generation project.",
                    Cause = "The avatar is owned by another project or has not been scanned.",
                    Impact = "Safe overwrite/detach is not possible, so the operation was cancelled.",
                    Resolution = "Rescan the avatar to confirm ownership.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationRolledBack,
                new DiagnosticLocalizedFields
                {
                    Title = "統合を安全に取り消しました",
                    Summary = "統合適用を取り消し、適用前に戻しました。",
                    Cause = "適用後に問題が検出され、自動で巻き戻しました。",
                    Impact = "アセットは適用前の状態です。",
                    Resolution = "問題を修正してから、もう一度生成してください。",
                    Caution = "原因はメッセージをご確認ください。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Integration rolled back safely",
                    Summary = "The applied integration was reverted to its previous state.",
                    Cause = "A problem was detected after applying, so the change was rolled back.",
                    Impact = "The asset is back in its pre-apply state.",
                    Resolution = "Fix the problem, then generate again.",
                    Caution = "See the message for the cause."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationParameterConflict,
                new DiagnosticLocalizedFields
                {
                    Title = "パラメーター名が競合しています",
                    Summary = "生成しようとするパラメーターが、既存のパラメーターと競合しています。",
                    Cause = "同じ名前が別の用途で使われています。",
                    Impact = "競合するパラメーターは生成されません。",
                    Resolution = "使用中のパラメーターと重複しない名前を設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Parameter name conflicts",
                    Summary = "The parameter being generated conflicts with an existing parameter.",
                    Cause = "The same name is already used for another purpose.",
                    Impact = "The conflicting parameter is not generated.",
                    Resolution = "Choose a name that does not duplicate an existing parameter.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationBudget,
                new DiagnosticLocalizedFields
                {
                    Title = "パラメーター予算を超過します",
                    Summary = "生成後のパラメーター数が、VRChatの上限を超えます。",
                    Cause = "追加が必要なパラメーターが予算残量を超えています。",
                    Impact = "生成結果がVRChatに収まらない可能性があります。",
                    Resolution = "不要なパラメーターを整理してから再生成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Parameter budget exceeded",
                    Summary = "The generated parameters would exceed the VRChat limit.",
                    Cause = "The required parameters exceed the remaining budget.",
                    Impact = "The result may not fit inside VRChat's limits.",
                    Resolution = "Clean up unused parameters, then regenerate.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationMenuCapacity,
                new DiagnosticLocalizedFields
                {
                    Title = "メニュー容量を超過します",
                    Summary = "生成後のメニュー項目が、VRChatメニューの上限を超えます。",
                    Cause = "メニュー項目の数が上限を超えています。",
                    Impact = "生成結果がVRChatに収まらない可能性があります。",
                    Resolution = "メニュー項目を整理してから再生成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Menu capacity exceeded",
                    Summary = "The generated menu would exceed the VRChat menu limit.",
                    Cause = "The number of menu items exceeds the limit.",
                    Impact = "The result may not fit inside VRChat's menus.",
                    Resolution = "Tidy up the menu items, then regenerate.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationFx,
                new DiagnosticLocalizedFields
                {
                    Title = "FXレイヤーを生成できませんでした",
                    Summary = "VRChat統合に必要なFXレイヤーを生成できませんでした。",
                    Cause = "出力先のAnimatorControllerを生成・更新できませんでした。",
                    Impact = "統合が不完全な状態です。",
                    Resolution = "出力先の状態を確認してから、もう一度生成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Could not generate the FX layer",
                    Summary = "The FX layer required for VRChat integration could not be generated.",
                    Cause = "The output AnimatorController could not be created or updated.",
                    Impact = "The integration is incomplete.",
                    Resolution = "Check the destination state and generate again.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationAnimatorParameterConflict,
                new DiagnosticLocalizedFields
                {
                    Title = "アニメーターのパラメーターと競合しています",
                    Summary = "生成が参照するパラメーターが、アニメーター内の既存パラメーターと競合しています。",
                    Cause = "型や既定値が一致しないパラメーターが既に存在しています。",
                    Impact = "生成結果の動作が想定と異なる可能性があります。",
                    Resolution = "競合するパラメーター定義を確認し、統一してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Conflicts with an animator parameter",
                    Summary = "A parameter referenced by the generation conflicts with an existing animator parameter.",
                    Cause = "An existing parameter uses a different type or default value.",
                    Impact = "The generated result may behave differently than expected.",
                    Resolution = "Review the conflicting parameter definitions and unify them.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationLayerConflict,
                new DiagnosticLocalizedFields
                {
                    Title = "レイヤー名が競合しています",
                    Summary = "追加するレイヤー名が、既存のレイヤーと重複しています。",
                    Cause = "同じ名前のレイヤーがAnimatorControllerに存在します。",
                    Impact = "レイヤーを追加できません。",
                    Resolution = "既存レイヤー名と重複しない名前へ変更してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Layer name conflicts",
                    Summary = "The layer being added duplicates an existing layer name.",
                    Cause = "An AnimatorController layer already uses the same name.",
                    Impact = "The layer cannot be added.",
                    Resolution = "Use a name that does not duplicate an existing layer.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationWriteDefaults,
                new DiagnosticLocalizedFields
                {
                    Title = "Write Defaultsが有効です",
                    Summary = "出力先のFXレイヤーでWrite Defaultsが有効になっています。",
                    Cause = "レイヤー設定でWrite Defaultsを使用する設定です。",
                    Impact = "生成したアニメーションの既定値の扱いが変わり、見た目が違う可能性があります。",
                    Resolution = "必要に応じてFXレイヤーのWrite Defaults設定をご確認ください。",
                    Caution = "VRChatでは無効化を推奨されることが多い設定です。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Write Defaults is enabled",
                    Summary = "The destination FX layer has Write Defaults enabled.",
                    Cause = "The layer is configured to use Write Defaults.",
                    Impact = "Default-value handling changes and the result may look different than expected.",
                    Resolution = "Review the FX layer's Write Defaults setting if needed.",
                    Caution = "VRChat commonly recommends disabling this."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationBindingConflict,
                new DiagnosticLocalizedFields
                {
                    Title = "生成結果のバインドが競合しています",
                    Summary = "生成したクリップ内で、同じバインドが競合しています。",
                    Cause = "同一の対象に複数の値割り当てが発生しました。",
                    Impact = "生成結果の動作が不安定な可能性があります。",
                    Resolution = "トラックの割り当てを確認し、競合を解消して再生成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Binding conflict in generated output",
                    Summary = "A generated clip contains a conflicting binding.",
                    Cause = "Multiple value assignments target the same element.",
                    Impact = "The generated result may behave unstably.",
                    Resolution = "Review track assignments, resolve the conflict, and regenerate.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.GenerationManifest,
                new DiagnosticLocalizedFields
                {
                    Title = "統合マニフェストが不正です",
                    Summary = "生成結果を管理するマニフェストが欠落・不正です。",
                    Cause = "マニフェストが存在しない、または読み込めません。",
                    Impact = "生成結果の追跡・巻き戻しができません。",
                    Resolution = "プロジェクトを再検証し、マニフェストを再作成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Integration manifest is invalid",
                    Summary = "The manifest that tracks generated output is missing or invalid.",
                    Cause = "The manifest does not exist or cannot be read.",
                    Impact = "The generated output cannot be tracked or rolled back.",
                    Resolution = "Re-validate the project to recreate the manifest.",
                    Caution = ""
                });
        }
    }
}