using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Localization
{
    public static partial class FaceMotionDiagnosticLocalizationCatalog
    {
        static partial void RegisterDataTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en)
        {
            Add(ja, en, FaceMotionDiagnosticCodes.NullProject,
                new DiagnosticLocalizedFields
                {
                    Title = "プロジェクトがありません",
                    Summary = "開かれているプロジェクトが存在しないか、読み込まれていません。",
                    Cause = "プロジェクトが選択されていない、または破損しています。",
                    Impact = "操作を進められません。",
                    Resolution = "FaceMotionプロジェクトを開いてください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Project is missing",
                    Summary = "There is no open project, or it has not been loaded.",
                    Cause = "No project is selected, or the data is corrupted.",
                    Impact = "Operations cannot proceed.",
                    Resolution = "Open a FaceMotion project.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InvalidProjectId,
                new DiagnosticLocalizedFields
                {
                    Title = "プロジェクトIDが不正です",
                    Summary = "プロジェクトのIDが有効な識別子ではありません。",
                    Cause = "プロジェクトのIDが壊れているか、空です。",
                    Impact = "プロジェクトを一意に識別できません。",
                    Resolution = "プロジェクトIDを再生成してください。",
                    Caution = "IDを書き換えるとデータ参照が壊れる可能性があります。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid project ID",
                    Summary = "The project ID is not a valid identifier.",
                    Cause = "The project ID is malformed or empty.",
                    Impact = "The project cannot be uniquely identified.",
                    Resolution = "Regenerate the project ID.",
                    Caution = "Editing IDs by hand may break data references."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InvalidAnimationId,
                new DiagnosticLocalizedFields
                {
                    Title = "アニメーションIDが不正です",
                    Summary = "タイムラインのアニメーションIDが有効な識別子ではありません。",
                    Cause = "アニメーションIDが壊れているか、空です。",
                    Impact = "アニメーションを一意に識別できません。",
                    Resolution = "アニメーションIDを再生成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid animation ID",
                    Summary = "The timeline animation ID is not a valid identifier.",
                    Cause = "The animation ID is malformed or empty.",
                    Impact = "The animation cannot be uniquely identified.",
                    Resolution = "Regenerate the animation ID.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateAnimationId,
                new DiagnosticLocalizedFields
                {
                    Title = "アニメーションIDが重複しています",
                    Summary = "同じアニメーションIDが複数存在します。",
                    Cause = "複数のアニメーションに同じIDが使われています。",
                    Impact = "どちらのアニメーションを指すのか確定できません。",
                    Resolution = "それぞれに一意なIDを割り当ててください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Duplicate animation ID",
                    Summary = "More than one animation uses the same ID.",
                    Cause = "Multiple animations share a single ID.",
                    Impact = "The intended animation cannot be determined.",
                    Resolution = "Assign a unique ID to each animation.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NullAnimation,
                new DiagnosticLocalizedFields
                {
                    Title = "アニメーションがありません",
                    Summary = "タイムラインに割り当てられたアニメーションが存在しません。",
                    Cause = "アニメーションが削除された、または未作成です。",
                    Impact = "タイムラインを編集できません。",
                    Resolution = "アニメーションを作成または選択してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Animation is missing",
                    Summary = "The animation assigned to the timeline does not exist.",
                    Cause = "The animation was removed or has not been created.",
                    Impact = "The timeline cannot be edited.",
                    Resolution = "Create or select an animation.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NullTimeline,
                new DiagnosticLocalizedFields
                {
                    Title = "タイムラインがありません",
                    Summary = "アニメーションにタイムラインが設定されていません。",
                    Cause = "タイムラインのデータが存在しないか、破損しています。",
                    Impact = "タイムラインを編集できません。",
                    Resolution = "アニメーションにタイムラインを設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Timeline is missing",
                    Summary = "The animation has no timeline.",
                    Cause = "Timeline data is absent or corrupted.",
                    Impact = "The timeline cannot be edited.",
                    Resolution = "Assign a timeline to the animation.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InvalidDuration,
                new DiagnosticLocalizedFields
                {
                    Title = "タイムラインの長さが不正です",
                    Summary = "タイムラインの長さが有限の正の値ではありません。",
                    Cause = "長さが0以下か、非有限値です。",
                    Impact = "タイムラインを安全に再生・編集できません。",
                    Resolution = "正の長さを設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid timeline duration",
                    Summary = "The timeline duration is not a finite positive value.",
                    Cause = "The duration is non-positive or non-finite.",
                    Impact = "The timeline cannot be safely played or edited.",
                    Resolution = "Set a positive duration.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InvalidFrameRate,
                new DiagnosticLocalizedFields
                {
                    Title = "フレームレートが不正です",
                    Summary = "タイムラインのフレームレートが有限の正の値ではありません。",
                    Cause = "フレームレートが0以下か、非有限値です。",
                    Impact = "タイムラインを安全に再生・編集できません。",
                    Resolution = "正のフレームレートを設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid frame rate",
                    Summary = "The timeline frame rate is not a finite positive value.",
                    Cause = "The frame rate is non-positive or non-finite.",
                    Impact = "The timeline cannot be safely played or edited.",
                    Resolution = "Set a positive frame rate.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InvalidTrackId,
                new DiagnosticLocalizedFields
                {
                    Title = "トラックIDが不正です",
                    Summary = "タイムラインのトラックIDが有効な識別子ではありません。",
                    Cause = "トラックIDが壊れているか、空です。",
                    Impact = "トラックを一意に識別できません。",
                    Resolution = "トラックIDを再生成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid track ID",
                    Summary = "A timeline track ID is not a valid identifier.",
                    Cause = "The track ID is malformed or empty.",
                    Impact = "The track cannot be uniquely identified.",
                    Resolution = "Regenerate the track ID.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateTrackId,
                new DiagnosticLocalizedFields
                {
                    Title = "トラックIDが重複しています",
                    Summary = "同じトラックIDが複数存在します。",
                    Cause = "複数のトラックに同じIDが使われています。",
                    Impact = "どちらのトラックを指すのか確定できません。",
                    Resolution = "それぞれに一意なIDを割り当ててください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Duplicate track ID",
                    Summary = "More than one track uses the same ID.",
                    Cause = "Multiple tracks share a single ID.",
                    Impact = "The intended track cannot be determined.",
                    Resolution = "Assign a unique ID to each track.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NullTrack,
                new DiagnosticLocalizedFields
                {
                    Title = "トラックが空です",
                    Summary = "タイムラインに不正なトラック（null）が含まれています。",
                    Cause = "トラックのデータが破損しています。",
                    Impact = "タイムラインを編集できません。",
                    Resolution = "空のトラックを削除してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Track is empty",
                    Summary = "The timeline contains a null track.",
                    Cause = "Track data is corrupted.",
                    Impact = "The timeline cannot be edited.",
                    Resolution = "Remove the null track.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.PayloadMismatch,
                new DiagnosticLocalizedFields
                {
                    Title = "ペイロードの種類が一致しません",
                    Summary = "トラックのペイロードの種類と、トラックの種類が一致していません。",
                    Cause = "BlendShapeトラックにTransformペイロード、またはその逆が設定されています。",
                    Impact = "そのトラックを解決できません。",
                    Resolution = "トラックの種類に合ったペイロードを設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Payload kind mismatch",
                    Summary = "The payload kind does not match the track kind.",
                    Cause = "A BlendShape track has a Transform payload, or vice versa.",
                    Impact = "The track cannot be resolved.",
                    Resolution = "Set a payload that matches the track kind.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UnsupportedRotationMode,
                new DiagnosticLocalizedFields
                {
                    Title = "未対応の回転モードです",
                    Summary = "サポートされていない回転モードが使われています。",
                    Cause = "ShortestQuaternion以外のモードが設定されています。",
                    Impact = "そのトラックを安全に解決できません。",
                    Resolution = "ShortestQuaternionモードを使用してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Unsupported rotation mode",
                    Summary = "An unsupported rotation mode is used.",
                    Cause = "A mode other than ShortestQuaternion is configured.",
                    Impact = "The track cannot be resolved safely.",
                    Resolution = "Use ShortestQuaternion mode.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NullPayload,
                new DiagnosticLocalizedFields
                {
                    Title = "ペイロードが空です",
                    Summary = "トラックにペイロードが設定されていません。",
                    Cause = "ペイロードのバインド情報が未設定です。",
                    Impact = "そのトラックを解決できません。",
                    Resolution = "ペイロードにバインドを設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Payload is empty",
                    Summary = "The track has no payload.",
                    Cause = "The payload binding information is not set.",
                    Impact = "The track cannot be resolved.",
                    Resolution = "Set a binding in the payload.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateTrackBinding,
                new DiagnosticLocalizedFields
                {
                    Title = "トラックのバインドが重複しています",
                    Summary = "複数のトラックが同じバインド先を指しています。",
                    Cause = "異なるトラックが同じRenderer/BlendShapeやTransformを参照しています。",
                    Impact = "どちらのトラックが優先されるか曖昧です。",
                    Resolution = "バインド先を整理してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Duplicate track binding",
                    Summary = "Multiple tracks point to the same binding target.",
                    Cause = "Different tracks reference the same renderer/blend shape or transform.",
                    Impact = "Which track takes priority is ambiguous.",
                    Resolution = "Consolidate the binding targets.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InvalidKeyId,
                new DiagnosticLocalizedFields
                {
                    Title = "キーIDが不正です",
                    Summary = "キーのIDが有効な識別子ではありません。",
                    Cause = "キーIDが壊れているか、空です。",
                    Impact = "キーを一意に識別できません。",
                    Resolution = "キーIDを再生成してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid key ID",
                    Summary = "A key ID is not a valid identifier.",
                    Cause = "The key ID is malformed or empty.",
                    Impact = "The key cannot be uniquely identified.",
                    Resolution = "Regenerate the key ID.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateKeyId,
                new DiagnosticLocalizedFields
                {
                    Title = "キーIDが重複しています",
                    Summary = "同じキーIDが複数存在します。",
                    Cause = "複数のキーに同じIDが使われています。",
                    Impact = "どちらのキーを指すのか確定できません。",
                    Resolution = "それぞれに一意なIDを割り当ててください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Duplicate key ID",
                    Summary = "More than one key uses the same ID.",
                    Cause = "Multiple keys share a single ID.",
                    Impact = "The intended key cannot be determined.",
                    Resolution = "Assign a unique ID to each key.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NullKey,
                new DiagnosticLocalizedFields
                {
                    Title = "キーが空です",
                    Summary = "タイムラインに不正なキー（null）が含まれています。",
                    Cause = "キーのデータが破損しています。",
                    Impact = "タイムラインを編集できません。",
                    Resolution = "空のキーを削除してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Key is empty",
                    Summary = "The timeline contains a null key.",
                    Cause = "Key data is corrupted.",
                    Impact = "The timeline cannot be edited.",
                    Resolution = "Remove the null key.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateKeyTime,
                new DiagnosticLocalizedFields
                {
                    Title = "キーの時刻が重複しています",
                    Summary = "同じ時刻に複数のキーが設定されています。",
                    Cause = "複数のキーが同じ時刻を持っています。",
                    Impact = "時刻が重複したキーはスケジュールが不安定です。",
                    Resolution = "重複するキーの時刻を変更してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Duplicate key time",
                    Summary = "Multiple keys share the same time.",
                    Cause = "Multiple keys have the same time.",
                    Impact = "Keys at the same time produce unstable scheduling.",
                    Resolution = "Adjust the times of the duplicate keys.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UnsortedKeys,
                new DiagnosticLocalizedFields
                {
                    Title = "キーが時刻順に並んでいません",
                    Summary = "キーが時刻の昇順に並んでいません。",
                    Cause = "キーが時刻順にソートされていません。",
                    Impact = "再生順序が不安定になる可能性があります。",
                    Resolution = "キーを時刻順に並べてください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Keys are not sorted by time",
                    Summary = "The keys are not in ascending time order.",
                    Cause = "The keys have not been sorted by time.",
                    Impact = "Playback order may be unstable.",
                    Resolution = "Sort the keys by time.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NegativeKeyTime,
                new DiagnosticLocalizedFields
                {
                    Title = "キーの時刻が負の値です",
                    Summary = "キーの時刻が負の値になっています。",
                    Cause = "0未満の時刻が指定されています。",
                    Impact = "負の時刻のキーは再生で無視される可能性があります。",
                    Resolution = "キーの時刻を0以上に変更してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Negative key time",
                    Summary = "A key has a negative time.",
                    Cause = "A time less than zero is specified.",
                    Impact = "Negative-time keys may be ignored during playback.",
                    Resolution = "Change the key time to zero or greater.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.KeyBeyondDuration,
                new DiagnosticLocalizedFields
                {
                    Title = "キーの時刻がタイムライン長を超えています",
                    Summary = "キーの時刻がタイムラインの長さを超えています。",
                    Cause = "長さ以上の時刻にキーが配置されています。",
                    Impact = "そのキーは再生範囲外になり、無視される可能性があります。",
                    Resolution = "キーをタイムラインの長さ以内に移動してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Key time exceeds timeline duration",
                    Summary = "A key time is beyond the timeline duration.",
                    Cause = "A key is placed at a time longer than the duration.",
                    Impact = "The key falls outside playback range and may be ignored.",
                    Resolution = "Move the key within the timeline duration.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NonFiniteKeyValue,
                new DiagnosticLocalizedFields
                {
                    Title = "キーの値が有限でありません",
                    Summary = "キーの値がNaNや無限大など、有限でない値です。",
                    Cause = "不正な値がキーに含まれています。",
                    Impact = "そのキーは正しく適用されない可能性があります。",
                    Resolution = "不正なキーを削除するか修正してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Non-finite key value",
                    Summary = "A key value is NaN, infinity, or otherwise not finite.",
                    Cause = "An invalid value is present in a key.",
                    Impact = "The key may not apply correctly.",
                    Resolution = "Remove or repair the invalid key.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NonFiniteKeyTime,
                new DiagnosticLocalizedFields
                {
                    Title = "キーの時刻が有限でありません",
                    Summary = "キーの時刻がNaNや無限大など、有限でない値です。",
                    Cause = "不正な時刻がキーに含まれています。",
                    Impact = "そのキーは正しく適用されない可能性があります。",
                    Resolution = "不正なキーを削除するか修正してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Non-finite key time",
                    Summary = "A key time is NaN, infinity, or otherwise not finite.",
                    Cause = "An invalid time is present in a key.",
                    Impact = "The key may not apply correctly.",
                    Resolution = "Remove or repair the invalid key.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ManualKeyWithGenerationId,
                new DiagnosticLocalizedFields
                {
                    Title = "生成IDを持つ手動キーがあります",
                    Summary = "手動で作成されたキーに生成IDが設定されています。",
                    Cause = "手動キーと生成キーの区別が曖昧になっています。",
                    Impact = "手動キーが生成処理で上書きされる可能性があります。",
                    Resolution = "手動キーから生成IDを削除してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Manual key has a generation ID",
                    Summary = "A manually created key has a generation ID.",
                    Cause = "The distinction between manual and generated keys is ambiguous.",
                    Impact = "The manual key may be overwritten by generation.",
                    Resolution = "Remove the generation ID from the manual key.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.OrphanedGenerationKey,
                new DiagnosticLocalizedFields
                {
                    Title = "生成レコードのない生成キーがあります",
                    Summary = "生成レコードを参照しているキーが存在しますが、対応する生成レコードがありません。",
                    Cause = "生成レコードが削除された可能性があります。",
                    Impact = "そのキーの生成元を追跡できません。",
                    Resolution = "不要な生成キーを削除してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Orphaned generation key",
                    Summary = "A key references a generation record that no longer exists.",
                    Cause = "The generation record may have been removed.",
                    Impact = "The key's origin cannot be tracked.",
                    Resolution = "Remove the orphaned generation key.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NullGenerationRecord,
                new DiagnosticLocalizedFields
                {
                    Title = "生成レコードがありません",
                    Summary = "タイムラインに不正な生成レコード（null）が含まれています。",
                    Cause = "生成レコードのデータが破損しています。",
                    Impact = "生成履歴を追跡できません。",
                    Resolution = "空の生成レコードを削除してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Generation record is empty",
                    Summary = "The timeline contains a null generation record.",
                    Cause = "Generation record data is corrupted.",
                    Impact = "Generation history cannot be tracked.",
                    Resolution = "Remove the null generation record.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateGenerationId,
                new DiagnosticLocalizedFields
                {
                    Title = "生成IDが重複しています",
                    Summary = "同じ生成IDが複数存在します。",
                    Cause = "複数の生成レコードに同じIDが使われています。",
                    Impact = "生成結果を正確に追跡できません。",
                    Resolution = "それぞれに一意なIDを割り当ててください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Duplicate generation ID",
                    Summary = "More than one generation record uses the same ID.",
                    Cause = "Multiple generation records share a single ID.",
                    Impact = "Generation results cannot be tracked accurately.",
                    Resolution = "Assign a unique ID to each generation record.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ManualKeyProtected,
                new DiagnosticLocalizedFields
                {
                    Title = "手動キーが保護されました",
                    Summary = "手動キーが同じ時刻に存在するため、生成キーがスキップされました。",
                    Cause = "同一時刻に既に手動キーが存在します。",
                    Impact = "その時刻の手動キーは保護されます（上書きされません）。",
                    Resolution = "意図しない場合は手動キーを削除してから再生成してください。",
                    Caution = "この診断は情報です。手動キーが正しく保護されました。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Manual key was protected",
                    Summary = "A generated key was skipped because a manual key occupies the same time.",
                    Cause = "A manual key already exists at the same time.",
                    Impact = "The manual key at that time is protected (not overwritten).",
                    Resolution = "If unintended, remove the manual key and regenerate.",
                    Caution = "This is informational; the manual key was correctly preserved."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ForeignContentInFolder,
                new DiagnosticLocalizedFields
                {
                    Title = "プロジェクト外のアセットが含まれています",
                    Summary = "プロジェクトが管理するフォルダに、プロジェクト外のアセットが混在しています。",
                    Cause = "他のアセットが誤ってプロジェクトフォルダに配置された可能性があります。",
                    Impact = "プロジェクトの管理対象が不明確になります。",
                    Resolution = "該当するアセットをプロジェクトフォルダの外へ移動してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Foreign content in folder",
                    Summary = "A folder managed by the project contains assets that are not part of it.",
                    Cause = "Other assets may have been accidentally placed in the project folder.",
                    Impact = "The project's managed scope becomes unclear.",
                    Resolution = "Move the foreign assets out of the project folder.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.TamperedOwnedPaths,
                new DiagnosticLocalizedFields
                {
                    Title = "プロジェクトが所有するパスが変更されています",
                    Summary = "プロジェクトが所有するアセットのパスが変更または削除されています。",
                    Cause = "プロジェクト管理外の操作でパスが変更された可能性があります。",
                    Impact = "プロジェクトの整合性が保てません。",
                    Resolution = "変更を元に戻すか、プロジェクトを再構築してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Owned paths have been tampered with",
                    Summary = "Asset paths owned by the project have been changed or removed.",
                    Cause = "Paths may have been modified outside of project management.",
                    Impact = "Project integrity cannot be maintained.",
                    Resolution = "Revert the changes, or rebuild the project.",
                    Caution = ""
                });
        }
    }
}