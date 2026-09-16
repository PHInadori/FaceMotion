using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Localization
{
    public static partial class FaceMotionDiagnosticLocalizationCatalog
    {
        static partial void RegisterMigrationTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en)
        {
            Add(ja, en, FaceMotionDiagnosticCodes.FutureSchema,
                new DiagnosticLocalizedFields
                {
                    Title = "未対応のプロジェクト形式です",
                    Summary = "このプロジェクト形式は現在のバージョンではサポートされていません。",
                    Cause = "FutureSchemaバージョンのプロジェクトが作成されています。",
                    Impact = "プロジェクトを開けません。",
                    Resolution = "FaceMotionを最新版に更新してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Unsupported project format",
                    Summary = "This project format is not supported by the current version.",
                    Cause = "A FutureSchema-version project was created.",
                    Impact = "The project cannot be opened.",
                    Resolution = "Update FaceMotion to the latest version.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UninitializedSchema,
                new DiagnosticLocalizedFields
                {
                    Title = "プロジェクト形式が未初期化です",
                    Summary = "プロジェクトのスキーマバージョンが設定されていません。",
                    Cause = "新規プロジェクトでスキーマが初期化されていません。",
                    Impact = "プロジェクトを開けません。",
                    Resolution = "プロジェクトを再作成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Project schema is uninitialized",
                    Summary = "The project schema version is not set.",
                    Cause = "A new project has an uninitialized schema.",
                    Impact = "The project cannot be opened.",
                    Resolution = "Recreate the project.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.MissingMigrationStep,
                new DiagnosticLocalizedFields
                {
                    Title = "必要なマイグレーションステップがありません",
                    Summary = "スキーマアップグレードに必要なステップが見つかりません。",
                    Cause = "マイグレーションコードが不足している可能性があります。",
                    Impact = "プロジェクトをアップグレードできません。",
                    Resolution = "FaceMotionを最新版に更新してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Missing migration step",
                    Summary = "A migration step required for schema upgrade is not found.",
                    Cause = "Migration code may be missing.",
                    Impact = "The project cannot be upgraded.",
                    Resolution = "Update FaceMotion to the latest version.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.MigrationFailed,
                new DiagnosticLocalizedFields
                {
                    Title = "マイグレーションに失敗しました",
                    Summary = "プロジェクトのアップグレード処理に失敗しました。",
                    Cause = "データの不整合や予期しないエラーが発生しました。",
                    Impact = "プロジェクトをアップグレードできません。",
                    Resolution = "詳細メッセージを確認してください。手動の修正が必要な場合があります。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Migration failed",
                    Summary = "The project upgrade failed.",
                    Cause = "Data inconsistency or an unexpected error occurred.",
                    Impact = "The project cannot be upgraded.",
                    Resolution = "Check the detail message. Manual fixes may be required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.MigrationValidationFailed,
                new DiagnosticLocalizedFields
                {
                    Title = "マイグレーションの検証に失敗しました",
                    Summary = "アップグレード後の整合性検証に失敗しました。",
                    Cause = "アップグレード結果が期待どおりではありません。",
                    Impact = "アップグレードが不完全な状態です。",
                    Resolution = "詳細メッセージを確認してください。手動の修正が必要な場合があります。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Migration validation failed",
                    Summary = "Post-upgrade integrity validation failed.",
                    Cause = "The upgrade result does not match expectations.",
                    Impact = "The upgrade is incomplete.",
                    Resolution = "Check the detail message. Manual fixes may be required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UpgradedSchema,
                new DiagnosticLocalizedFields
                {
                    Title = "プロジェクト形式がアップグレードされました",
                    Summary = "プロジェクトが新しい形式にアップグレードされました。",
                    Cause = "マイグレーションが正常に完了しました。",
                    Impact = "プロジェクトが最新形式で保存されました。",
                    Resolution = "特に操作は不要です。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Project format upgraded",
                    Summary = "The project has been upgraded to the new format.",
                    Cause = "Migration completed successfully.",
                    Impact = "The project is now saved in the latest format.",
                    Resolution = "No action is required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.FutureSchemaBlocked,
                new DiagnosticLocalizedFields
                {
                    Title = "未対応の形式のため処理を中断しました",
                    Summary = "プロジェクト形式が未対応のため、処理を安全に停止しました。",
                    Cause = "FutureSchemaバージョンのプロジェクトが作成されています。",
                    Impact = "プロジェクトを開けません。",
                    Resolution = "FaceMotionを最新版に更新してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Blocked by unsupported format",
                    Summary = "Processing stopped safely because the project format is not supported.",
                    Cause = "A FutureSchema-version project was created.",
                    Impact = "The project cannot be opened.",
                    Resolution = "Update FaceMotion to the latest version.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.MalformedData,
                new DiagnosticLocalizedFields
                {
                    Title = "データの形式が正しくありません",
                    Summary = "プロジェクトデータの形式が壊れています。",
                    Cause = "手動の編集やツール外の操作でデータが破損した可能性があります。",
                    Impact = "プロジェクトを安全に開けません。",
                    Resolution = "プロジェクトのバックアップから復元してください。",
                    Caution = "手動でJSONを編集すると、プロジェクトが破損する可能性があります。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Malformed data",
                    Summary = "The project data format is corrupted.",
                    Cause = "Manual edits or operations outside the tool may have damaged the data.",
                    Impact = "The project cannot be safely opened.",
                    Resolution = "Restore from a project backup.",
                    Caution = "Editing JSON by hand may corrupt the project."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.IdRepaired,
                new DiagnosticLocalizedFields
                {
                    Title = "IDが修復されました",
                    Summary = "重複していたIDが自動的に修復されました。",
                    Cause = "ID衝突が自動的に解決されました。",
                    Impact = "プロジェクトの整合性が回復しました。",
                    Resolution = "特に操作は不要です。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "ID was repaired",
                    Summary = "A duplicate ID was automatically repaired.",
                    Cause = "An ID conflict was resolved automatically.",
                    Impact = "Project integrity has been restored.",
                    Resolution = "No action is required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateId,
                new DiagnosticLocalizedFields
                {
                    Title = "IDが重複しています",
                    Summary = "プロジェクト内でIDが重複しています。",
                    Cause = "複数のオブジェクトに同じIDが割り当てられています。",
                    Impact = "プロジェクトの整合性が保てません。",
                    Resolution = "IDを再生成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Duplicate ID",
                    Summary = "An ID is duplicated within the project.",
                    Cause = "Multiple objects share the same ID.",
                    Impact = "Project integrity cannot be maintained.",
                    Resolution = "Regenerate the IDs.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.PartialRecovery,
                new DiagnosticLocalizedFields
                {
                    Title = "部分的に復旧しました",
                    Summary = "一部の問題は修正されましたが、すべてが解決したわけではありません。",
                    Cause = "一部のエラーは自動修正されましたが、残りの問題は手動の対応が必要です。",
                    Impact = "プロジェクトは部分的に回復しています。",
                    Resolution = "残りの問題を手動で修正してください。",
                    Caution = "詳細メッセージで残りの問題を確認できます。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Partial recovery",
                    Summary = "Some issues were fixed, but not all.",
                    Cause = "Some errors were auto-repaired, but remaining issues need manual attention.",
                    Impact = "The project is partially recovered.",
                    Resolution = "Manually address the remaining issues.",
                    Caution = "See the detail message for remaining issues."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.IntegrationAmbiguous,
                new DiagnosticLocalizedFields
                {
                    Title = "統合の参照が曖昧です",
                    Summary = "Modular Avatar統合の参照先が明確ではありません。",
                    Cause = "統合パスが重複、または参照先が不明です。",
                    Impact = "統合の解除や更新が安全にできません。",
                    Resolution = "詳細メッセージを確認し、参照を整理してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Integration reference is ambiguous",
                    Summary = "The Modular Avatar integration reference is unclear.",
                    Cause = "Integration paths are duplicated or the target is unknown.",
                    Impact = "The integration cannot be safely detached or updated.",
                    Resolution = "Check the detail message and consolidate the references.",
                    Caution = ""
                });
        }
    }
}