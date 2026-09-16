using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Localization
{
    public static partial class FaceMotionDiagnosticLocalizationCatalog
    {
        static partial void RegisterCommandUiExportTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en)
        {
            Add(ja, en, FaceMotionDiagnosticCodes.CommandInvalidProject,
                new DiagnosticLocalizedFields
                {
                    Title = "プロジェクトが未使用状態です",
                    Summary = "コマンドを実行できる状態のプロジェクトがありません。",
                    Cause = "プロジェクトが開かれていない、または無効な状態です。",
                    Impact = "編集操作を実行できません。",
                    Resolution = "検証済みのプロジェクトを開き直してから操作してください。",
                    Caution = "プロジェクトを開くと検証が自動で再実行されます。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Project is not in a usable state",
                    Summary = "There is no project in a usable state for running commands.",
                    Cause = "No project is open, or the open project is invalid.",
                    Impact = "Editing commands cannot run.",
                    Resolution = "Open a validated project before operating.",
                    Caution = "Validation reruns automatically when a project is opened."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.CommandTargetNotFound,
                new DiagnosticLocalizedFields
                {
                    Title = "操作対象が見つかりません",
                    Summary = "コマンドの操作対象となる項目が見つかりませんでした。",
                    Cause = "対象が削除された、または選択されていない可能性があります。",
                    Impact = "コマンドを実行できません。",
                    Resolution = "既存の対象を選択してから、もう一度実行してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Command target not found",
                    Summary = "The item the command should operate on was not found.",
                    Cause = "The target may have been removed or is not selected.",
                    Impact = "The command cannot run.",
                    Resolution = "Select an existing target and try again.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.CommandInvalidArgument,
                new DiagnosticLocalizedFields
                {
                    Title = "コマンドの引数が不正です",
                    Summary = "コマンドに渡された値が不正でした。",
                    Cause = "入力値が範囲外や形式違いの可能性があります。",
                    Impact = "その操作は行われません。",
                    Resolution = "正しい値を入力してから実行し直してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid command argument",
                    Summary = "The value passed to the command was invalid.",
                    Cause = "The input may be out of range or the wrong format.",
                    Impact = "The operation was not performed.",
                    Resolution = "Enter a valid value and try again.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.CommandKindMismatch,
                new DiagnosticLocalizedFields
                {
                    Title = "コマンドの種類が一致しません",
                    Summary = "操作対象の種類とコマンドの種類が一致しません。",
                    Cause = "対象の種類（BlendShape/Transform）に見合わないコマンドが実行されました。",
                    Impact = "その操作は行われません。",
                    Resolution = "対象の種類に合うコマンドを使用してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Command kind mismatch",
                    Summary = "The command kind does not match the target item.",
                    Cause = "A command was run that does not fit the track kind.",
                    Impact = "The operation was not performed.",
                    Resolution = "Use the command that matches the track kind.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UIAssetCreateFailed,
                new DiagnosticLocalizedFields
                {
                    Title = "アセットを作成できませんでした",
                    Summary = "指定したパスにアセットを作成できませんでした。",
                    Cause = "パスが書き込み不可、または既に別のアセットが存在する可能性があります。",
                    Impact = "プロジェクトが作成されません。",
                    Resolution = "Assets配下の書き込み可能なパスを指定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Could not create the asset",
                    Summary = "The asset could not be created at the specified path.",
                    Cause = "The path may be read-only or already occupied by another asset.",
                    Impact = "The project was not created.",
                    Resolution = "Choose a writable path under Assets.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UIProjectLoadFailed,
                new DiagnosticLocalizedFields
                {
                    Title = "プロジェクトの読み込みに失敗しました",
                    Summary = "プロジェクトの読み込み中に問題が発生しました。",
                    Cause = "データが壊れているか、形式が一致しない可能性があります。",
                    Impact = "プロジェクトを利用できません。",
                    Resolution = "データを確認し、もう一度読み込み直してください。",
                    Caution = "現在の生成コードでは通常出力されない予備の診断です。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Project load failed",
                    Summary = "A problem occurred while loading the project.",
                    Cause = "The data may be corrupted or its format may not match.",
                    Impact = "The project cannot be used.",
                    Resolution = "Check the data and reload it.",
                    Caution = "This is a reserved diagnostic not normally emitted by generation."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UISelectionInvalid,
                new DiagnosticLocalizedFields
                {
                    Title = "選択が不正です",
                    Summary = "操作に必要な対象が選択されていません。",
                    Cause = "対象（プロジェクト・アニメーション等）が選択されていません。",
                    Impact = "その操作は実行できません。",
                    Resolution = "対象を選択してから操作してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Selection is invalid",
                    Summary = "The target required for this operation is not selected.",
                    Cause = "No target (project, animation, etc.) is selected.",
                    Impact = "The operation cannot run.",
                    Resolution = "Select the target first.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UIPasteTrackMissing,
                new DiagnosticLocalizedFields
                {
                    Title = "貼り付け対象のトラックがありません",
                    Summary = "キーを貼り付ける対象のトラックが見つかりませんでした。",
                    Cause = "コピー元のトラックが削除された可能性があります。",
                    Impact = "キーを貼り付けできません。",
                    Resolution = "コピー元のトラックからもう一度コピーしてください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Paste target track is missing",
                    Summary = "The track to paste keys into was not found.",
                    Cause = "The source track may have been removed.",
                    Impact = "Keys cannot be pasted.",
                    Resolution = "Re-copy the keys from the source track.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UIPasteCollision,
                new DiagnosticLocalizedFields
                {
                    Title = "一部のキーを貼り付けられませんでした",
                    Summary = "貼り付け先の時刻が埋まっていたため、一部のキーがスキップされました。",
                    Cause = "貼り付け対象の時刻に既存キーがあります。",
                    Impact = "スキップされたキーは追加されません。",
                    Resolution = "重なるキーを移動してから、もう一度貼り付けてください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Some pasted keys were skipped",
                    Summary = "Some keys were not pasted because the target time is occupied.",
                    Cause = "Existing keys occupy the paste times.",
                    Impact = "The skipped keys were not added.",
                    Resolution = "Move the overlapping keys, then paste again.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UiInfo,
                new DiagnosticLocalizedFields
                {
                    Title = "操作結果",
                    Summary = "直前の操作の結果についての案内です。",
                    Cause = "操作が完了しました（内容はメッセージをご確認ください）。",
                    Impact = "操作は実行済みです。",
                    Resolution = "特に操作は不要です。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Operation result",
                    Summary = "Information about the last operation.",
                    Cause = "The operation completed (see the message for details).",
                    Impact = "The operation has run.",
                    Resolution = "No action is required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportCreateParentFailed,
                new DiagnosticLocalizedFields
                {
                    Title = "出力先の親フォルダーを作成できません",
                    Summary = "エクスポート先の親フォルダーを作成できませんでした。",
                    Cause = "パスが書き込み不可など、フォルダーを作成できない状態です。",
                    Impact = "エクスポートは実行されません。",
                    Resolution = "Assets配下の書き込み可能なパスを指定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Could not create the parent folder",
                    Summary = "The parent folder for the export destination could not be created.",
                    Cause = "The path is not writable, so the folder cannot be created.",
                    Impact = "The export was not performed.",
                    Resolution = "Choose a writable path under Assets.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportPathOccupied,
                new DiagnosticLocalizedFields
                {
                    Title = "出力先が他のアセットに使用されています",
                    Summary = "出力先のパスが、AnimationClip以外のアセットに使用されています。",
                    Cause = "既に別の種類のアセットがそのパスに存在します。",
                    Impact = "上書きできず、エクスポートは実行されません。",
                    Resolution = "空いている.animパスか、既存のAnimationClipを指定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Destination is occupied by another asset",
                    Summary = "The output path is used by an asset that is not an AnimationClip.",
                    Cause = "Another type of asset already exists at that path.",
                    Impact = "The export was not performed.",
                    Resolution = "Choose an empty .anim path or an existing AnimationClip.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportSucceeded,
                new DiagnosticLocalizedFields
                {
                    Title = "エクスポートしました",
                    Summary = "AnimationClipのエクスポートが正常に完了しました。",
                    Cause = "エクスポート処理が完了しました。",
                    Impact = "指定したパスにAnimationClipが作成されました。",
                    Resolution = "特に操作は不要です。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Exported successfully",
                    Summary = "The AnimationClip was exported successfully.",
                    Cause = "The export completed.",
                    Impact = "An AnimationClip was created at the destination.",
                    Resolution = "No action is required.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportWriteFailed,
                new DiagnosticLocalizedFields
                {
                    Title = "エクスポートに失敗しました",
                    Summary = "エクスポート先への書き込みに失敗しました。",
                    Cause = "パスが書き込み不可、または例外が発生した可能性があります。",
                    Impact = "エクスポートは実行されません。",
                    Resolution = "出力先が書き込み可能か確認し、やり直してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Export failed",
                    Summary = "Writing to the export destination failed.",
                    Cause = "The path may be read-only, or an exception occurred.",
                    Impact = "The export was not performed.",
                    Resolution = "Check that the destination is writable and retry.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportNoTimeline,
                new DiagnosticLocalizedFields
                {
                    Title = "タイムラインを持つアニメーションを選んでください",
                    Summary = "エクスポート対象のアニメーションにタイムラインがありません。",
                    Cause = "タイムラインの無いアニメーションが選択されています。",
                    Impact = "エクスポートできません。",
                    Resolution = "タイムラインを持つ有効なアニメーションを選択してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Select an animation with a timeline",
                    Summary = "The animation selected for export has no timeline.",
                    Cause = "An animation without a timeline is selected.",
                    Impact = "The export cannot run.",
                    Resolution = "Select a valid FaceMotion animation with a timeline.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportInvalidDuration,
                new DiagnosticLocalizedFields
                {
                    Title = "タイムラインの長さが不正です",
                    Summary = "エクスポートに必要なタイムラインの長さが、有限の正の値ではありません。",
                    Cause = "タイムラインの長さが0以下か、非有限値です。",
                    Impact = "エクスポートできません。",
                    Resolution = "タイムラインに正の長さを設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid timeline duration",
                    Summary = "The timeline duration required for export is not a finite positive value.",
                    Cause = "The duration is non-positive or non-finite.",
                    Impact = "The export cannot run.",
                    Resolution = "Set a positive duration.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportInvalidFrameRate,
                new DiagnosticLocalizedFields
                {
                    Title = "フレームレートが不正です",
                    Summary = "エクスポートに必要なフレームレートが、有限の正の値ではありません。",
                    Cause = "フレームレートが0以下か、非有限値です。",
                    Impact = "エクスポートできません。",
                    Resolution = "正のフレームレートを設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid frame rate",
                    Summary = "The frame rate required for export is not a finite positive value.",
                    Cause = "The frame rate is non-positive or non-finite.",
                    Impact = "The export cannot run.",
                    Resolution = "Set a positive frame rate.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportInvalidPath,
                new DiagnosticLocalizedFields
                {
                    Title = "出力先のパスが不正です",
                    Summary = "エクスポート先がAssets配下の.animアセットではありません。",
                    Cause = "パスがAssetsの外、または拡張子が.animではありません。",
                    Impact = "エクスポートできません。",
                    Resolution = "Assets配下の.anim拡張子のパスを指定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid export path",
                    Summary = "The export destination is not a .anim asset under Assets.",
                    Cause = "The path is outside Assets or does not end with .anim.",
                    Impact = "The export cannot run.",
                    Resolution = "Choose a path under Assets with the .anim extension.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportNullTrack,
                new DiagnosticLocalizedFields
                {
                    Title = "タイムラインに空のトラックがあります",
                    Summary = "タイムラインに不正なトラック（null）が含まれています。",
                    Cause = "トラックのデータが破損している可能性があります。",
                    Impact = "エクスポートできません。",
                    Resolution = "空のトラックを削除してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Timeline contains an empty track",
                    Summary = "The timeline contains a null track.",
                    Cause = "A track may be corrupted.",
                    Impact = "The export cannot run.",
                    Resolution = "Remove the null track.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportInvalidBlendShape,
                new DiagnosticLocalizedFields
                {
                    Title = "BlendShapeトラックの設定が不正です",
                    Summary = "有効なBlendShapeトラックに名前とキーが揃っていません。",
                    Cause = "バインド（Renderer/BlendShape名）が未設定か、キーがありません。",
                    Impact = "そのトラックはエクスポートできません。",
                    Resolution = "バインドとキーを設定するか、トラックを無効化してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Blend shape track is invalid",
                    Summary = "An enabled blend shape track has no complete binding and keys.",
                    Cause = "The binding or keys are missing.",
                    Impact = "That track cannot be exported.",
                    Resolution = "Set the binding and keys, or disable the track.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportInvalidTransform,
                new DiagnosticLocalizedFields
                {
                    Title = "Transformトラックの設定が不正です",
                    Summary = "有効なTransformトラックにペイロードとキーが揃っていません。",
                    Cause = "Transformのバインドが未設定か、キーがありません。",
                    Impact = "そのトラックはエクスポートできません。",
                    Resolution = "Transformのバインドとキーを設定するか、トラックを無効化してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Transform track is invalid",
                    Summary = "An enabled transform track has no complete payload and keys.",
                    Cause = "The transform binding or keys are missing.",
                    Impact = "That track cannot be exported.",
                    Resolution = "Set the transform binding and keys, or disable the track.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportUnsupportedRotation,
                new DiagnosticLocalizedFields
                {
                    Title = "回転モードがエクスポート未対応です",
                    Summary = "回転トラックに未対応の回転モードが使われています。",
                    Cause = "ShortestQuaternion以外のモードが設定されています。",
                    Impact = "そのトラックはエクスポートできません。",
                    Resolution = "回転モードをShortestQuaternionに変更してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Rotation mode is not exportable",
                    Summary = "The rotation track uses a rotation mode that cannot be exported.",
                    Cause = "A mode other than ShortestQuaternion is configured.",
                    Impact = "That track cannot be exported.",
                    Resolution = "Use ShortestQuaternion rotation mode.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ExportInvalidKey,
                new DiagnosticLocalizedFields
                {
                    Title = "キーが不正です",
                    Summary = "キーに有限でない時刻や値が含まれています。",
                    Cause = "キーがNaNや無限大など、不正な値を持っています。",
                    Impact = "そのトラックはエクスポートできません。",
                    Resolution = "不正なキーを削除するか修正してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid key",
                    Summary = "A key has a non-finite time or value.",
                    Cause = "A key contains NaN, Infinity, or other invalid values.",
                    Impact = "That track cannot be exported.",
                    Resolution = "Remove or repair the invalid key.",
                    Caution = ""
                });
        }
    }
}