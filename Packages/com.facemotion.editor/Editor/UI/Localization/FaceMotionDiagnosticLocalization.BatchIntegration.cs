using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Localization
{
    public static partial class FaceMotionDiagnosticLocalizationCatalog
    {
        static partial void RegisterBatchIntegrationTexts(Dictionary<string, DiagnosticLocalizedText> ja, Dictionary<string, DiagnosticLocalizedText> en)
        {
            Add(ja, en, FaceMotionDiagnosticCodes.BatchNoSelection,
                Fields("一括統合の対象がありません", "チェックしたアニメーションがないため、一括統合を開始できません。", "アニメーションリストで対象をチェックしてください。"),
                Fields("No batch selection", "Batch integration cannot start because no animations are checked.", "Check one or more animations in the animation list."));
            Add(ja, en, FaceMotionDiagnosticCodes.BatchDuplicateExportPath,
                Fields("出力クリップのパスが重複しています", "複数の対象が同じAnimationClip出力先へ解決されました。", "対象名または出力先を変更してください。"),
                Fields("Duplicate export path", "Multiple batch items resolve to the same AnimationClip output path.", "Rename an animation or change its output path."));
            Add(ja, en, FaceMotionDiagnosticCodes.BatchPreflightFailed,
                Fields("一括統合の事前検証に失敗しました", "適用前の検証で問題が見つかったため、VRChat統合は実行されませんでした。", "表示された診断を解決してから再実行してください。"),
                Fields("Batch preflight failed", "A pre-apply validation problem stopped VRChat integration.", "Resolve the listed diagnostics and run the batch again."));
            Add(ja, en, FaceMotionDiagnosticCodes.BatchRollback,
                Fields("一括統合を巻き戻しました", "適用中の失敗後、今回作成した統合を巻き戻しました。", "診断を解決してから再実行してください。"),
                Fields("Batch integration rolled back", "The integrations created by this batch were rolled back after an apply failure.", "Resolve the diagnostics and run the batch again."));
            Add(ja, en, FaceMotionDiagnosticCodes.BatchPartialApply,
                Fields("一部の統合が残っている可能性があります", "一括適用を完全に巻き戻せませんでした。", "生成物と診断を確認してから再実行してください。"),
                Fields("Possible partial batch apply", "The batch could not be fully rolled back.", "Inspect generated objects and diagnostics before retrying."));
        }

        private static DiagnosticLocalizedFields Fields(string title, string summary, string resolution)
        {
            return new DiagnosticLocalizedFields { Title = title, Summary = summary, Cause = summary, Impact = summary, Resolution = resolution, Caution = string.Empty };
        }
    }
}
