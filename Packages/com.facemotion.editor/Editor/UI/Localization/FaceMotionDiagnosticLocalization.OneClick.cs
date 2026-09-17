using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Localization
{
    public static partial class FaceMotionDiagnosticLocalizationCatalog
    {
        static partial void RegisterOneClickTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en)
        {
            Add(ja, en, FaceMotionDiagnosticCodes.OneClickNoCurrentAnimation,
                new DiagnosticLocalizedFields
                {
                    Title = "対象のアニメーションが選択されていません",
                    Summary = "ワンクリック統合を実行するには、エクスポート対象のアニメーションを選択してください。",
                    Cause = "プロジェクト内に選択中のアニメーションがありません。",
                    Impact = "AnimationClipのエクスポートとVRChat統合を実行できません。",
                    Resolution = "アニメーションリストからエクスポートしたいアニメーションを選択してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "No animation selected",
                    Summary = "Select an animation to export before running the one-click integration.",
                    Cause = "No animation is selected in the project.",
                    Impact = "The animation clip cannot be exported or integrated into VRChat.",
                    Resolution = "Select an animation in the animation list first.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.OneClickNoAvatar,
                new DiagnosticLocalizedFields
                {
                    Title = "対象のアバターが選択されていません",
                    Summary = "ワンクリック統合を実行するには、統合先のアバターを選択してください。",
                    Cause = "現在のシーンで有効なアバターが選択されていません。",
                    Impact = "VRChat統合を適用できません。",
                    Resolution = "ツールバーのアバターメニューからアバターを選択してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "No avatar selected",
                    Summary = "Select the destination avatar before running the one-click integration.",
                    Cause = "No valid avatar is selected in the current scene.",
                    Impact = "The VRChat integration cannot be applied.",
                    Resolution = "Choose an avatar from the toolbar avatar menu.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.OneClickForeignClip,
                new DiagnosticLocalizedFields
                {
                    Title = "出力先にFaceMotion所有でないクリップがあります",
                    Summary = "エクスポート先に、FaceMotionが所有していないAnimationClipが存在します。",
                    Cause = "登録されたエクスポート先を他者が使用している可能性があります。",
                    Impact = "ユーザー所有のアセットを上書きしないため、エクスポートを停止しました。",
                    Resolution = "別のアニメーション名にするか、外国のクリップを移動・削除してください。",
                    Caution = "FaceMotionは他者所有のアセットを絶対に上書きしません。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Export destination holds a foreign clip",
                    Summary = "The export destination already contains an AnimationClip that FaceMotion does not own.",
                    Cause = "The registered destination may be used by another tool.",
                    Impact = "Export was stopped so a user-owned asset is never overwritten.",
                    Resolution = "Rename the animation or move/remove the foreign clip.",
                    Caution = "FaceMotion never overwrites assets it does not own."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.OneClickBackendUnavailable,
                new DiagnosticLocalizedFields
                {
                    Title = "選択されたバックエンドが利用できません",
                    Summary = "選択された統合バックエンド（Modular Avatar）がプロジェクトにインストールされていません。",
                    Cause = "Modular Avatarが選択されたままパッケージが存在しません。",
                    Impact = "この状態では統合を適用できません。",
                    Resolution = "バックエンドを「直接統合 (Direct)」に切り替えるか、Modular Avatarを導入してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Selected backend is unavailable",
                    Summary = "The selected integration backend (Modular Avatar) is not installed in this project.",
                    Cause = "Modular Avatar was selected while the package is missing.",
                    Impact = "The integration cannot be applied in this state.",
                    Resolution = "Switch the backend to Direct or install Modular Avatar.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.OneClickCrossBackend,
                new DiagnosticLocalizedFields
                {
                    Title = "別バックエンドの統合が存在します",
                    Summary = "このアバターには選択中とは別のバックエンドによる既存統合があります。",
                    Cause = "以前に別のバックエンドで統合を適用した可能性があります。",
                    Impact = "新しく統合を追加すると、統合が並立します。",
                    Resolution = "不要な既存統合を先に削除することをお勧めします。",
                    Caution = "同一バックエンドでの再適用は自動的に置き換えられます。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "An integration under another backend exists",
                    Summary = "This avatar already has an integration created under a different backend than the selected one.",
                    Cause = "A prior integration may have been applied with another backend.",
                    Impact = "A new integration will coexist with the existing one.",
                    Resolution = "Removing the unneeded existing integration first is recommended.",
                    Caution = "Re-applying under the same backend replaces the existing integration automatically."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.OneClickExported,
                new DiagnosticLocalizedFields
                {
                    Title = "AnimationClipをエクスポートしました",
                    Summary = "選択中のアニメーションがAnimationClipとしてエクスポートされました。",
                    Cause = "エクスポート処理が正常に完了しました。",
                    Impact = "このクリップを統合プランナーへ直接渡します。",
                    Resolution = "特に操作は不要です。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "AnimationClip exported",
                    Summary = "The selected animation was exported as an AnimationClip.",
                    Cause = "Export completed successfully.",
                    Impact = "This clip is passed directly to the integration planner.",
                    Resolution = "No action is required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.OneClickReapplied,
                new DiagnosticLocalizedFields
                {
                    Title = "既存の統合を置き換えました",
                    Summary = "同一バックエンドの既存統合を検出し、新しい統合へ置き換えました。",
                    Cause = "統合が既に適用済みでした。",
                    Impact = "重複した統合は作成されません。",
                    Resolution = "特に操作は不要です。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Existing integration replaced",
                    Summary = "An existing integration under the same backend was detected and replaced with the new one.",
                    Cause = "An integration was already applied.",
                    Impact = "No duplicate integration is created.",
                    Resolution = "No action is required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.OneClickSucceeded,
                new DiagnosticLocalizedFields
                {
                    Title = "VRChatへの追加が完了しました",
                    Summary = "エクスポートとVRChat統合の適用が完了しました。",
                    Cause = "ワンクリック統合が正常に完了しました。",
                    Impact = "指定したクリップとバックエンドで統合されました。",
                    Resolution = "特に操作は不要です。生成オブジェクトはハイライトされます。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "VRChat integration complete",
                    Summary = "Export and VRChat integration were applied successfully.",
                    Cause = "The one-click integration completed successfully.",
                    Impact = "The clip was integrated using the selected backend.",
                    Resolution = "No action is required. The generated object is highlighted.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.OneClickNoPartialState,
                new DiagnosticLocalizedFields
                {
                    Title = "中途半端な統合状態は残りません",
                    Summary = "適用の失敗時に生成アセットを巻き戻したため、統合状態は存在しません。",
                    Cause = "統合の適用中に問題が発生しました。",
                    Impact = "アバターと既存アセットは変更されていません。",
                    Resolution = "上記のエラーを解決してから、もう一度実行してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "No partial integration state remains",
                    Summary = "Generated assets were rolled back on failure, so no integration state exists.",
                    Cause = "A problem occurred while applying the integration.",
                    Impact = "The avatar and existing assets were not changed.",
                    Resolution = "Resolve the errors above and run again.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.OneClickApplyStopped,
                new DiagnosticLocalizedFields
                {
                    Title = "統合の適用を停止しました",
                    Summary = "エクスポートまたは検証の段階で問題が検出されたため、適用は実行されませんでした。",
                    Cause = "ブロッキング診断が残っていました。",
                    Impact = "アバターと既存アセットは変更されていません。",
                    Resolution = "上記の診断を解決してから、もう一度実行してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Integration apply stopped",
                    Summary = "A problem was detected during export or validation, so the apply step was not run.",
                    Cause = "Blocking diagnostics remained.",
                    Impact = "The avatar and existing assets were not changed.",
                    Resolution = "Resolve the diagnostics above and run again.",
                    Caution = ""
                });
        }
    }
}