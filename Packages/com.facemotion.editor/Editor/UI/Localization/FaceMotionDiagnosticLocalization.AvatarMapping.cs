using System.Collections.Generic;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Localization
{
    public static partial class FaceMotionDiagnosticLocalizationCatalog
    {
        static partial void RegisterMappingTexts(
            Dictionary<string, DiagnosticLocalizedText> ja,
            Dictionary<string, DiagnosticLocalizedText> en)
        {
            Add(ja, en, FaceMotionDiagnosticCodes.InvalidProfileId,
                new DiagnosticLocalizedFields
                {
                    Title = "マッピングプロファイルIDが不正です",
                    Summary = "マッピングプロファイルのIDが有効な識別子ではありません。",
                    Cause = "保存されたプロファイルIDが壊れているか、空です。",
                    Impact = "プロファイルを一意に識別できません。",
                    Resolution = "プロファイルIDを再生成してください。",
                    Caution = "IDを手動で書き換えるとデータ参照が壊れる可能性があります。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid mapping profile ID",
                    Summary = "The mapping profile ID is not a valid identifier.",
                    Cause = "The stored profile ID is malformed or empty.",
                    Impact = "The profile cannot be uniquely identified.",
                    Resolution = "Regenerate the profile ID.",
                    Caution = "Editing IDs by hand may break data references."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.NullMappingProfile,
                new DiagnosticLocalizedFields
                {
                    Title = "マッピングプロファイルがありません",
                    Summary = "対象のマッピングプロファイルが存在しないか、読み込まれていません。",
                    Cause = "プロファイルが未選択または破損しています。",
                    Impact = "マッピングの評価・検証を進められません。",
                    Resolution = "有効なマッピングプロファイルを読み込んでください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Mapping profile missing",
                    Summary = "The target mapping profile does not exist or is not loaded.",
                    Cause = "The profile is not selected or is corrupted.",
                    Impact = "Mapping evaluation and validation cannot continue.",
                    Resolution = "Load a valid mapping profile.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.FutureProfileSchema,
                new DiagnosticLocalizedFields
                {
                    Title = "プロファイル形式が未対応の新しいものです",
                    Summary = "マッピングプロファイルの形式（スキーマ）が、このFaceMotionより新しい可能性があります。",
                    Cause = "プロファイルが未対応の新しいスキーマを使用しています。",
                    Impact = "このバージョンでは安全に読み込めません。",
                    Resolution = "FaceMotionを新しいバージョンへ更新してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Profile uses a newer, unsupported format",
                    Summary = "The mapping profile schema may be newer than this FaceMotion version.",
                    Cause = "The profile uses a schema this version cannot read.",
                    Impact = "The profile cannot be loaded safely.",
                    Resolution = "Update FaceMotion to a newer version.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.UninitializedProfileSchema,
                new DiagnosticLocalizedFields
                {
                    Title = "プロファイル形式が未初期化です",
                    Summary = "マッピングプロファイルの形式（スキーマ）が初期化されていません。",
                    Cause = "プロファイルが正しい手順で作成されていません。",
                    Impact = "読み込みの安全性を確認できません。",
                    Resolution = "プロファイルを正しい手順で作成し直してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Profile format is uninitialized",
                    Summary = "The mapping profile schema is not initialized.",
                    Cause = "The profile was not created through the proper flow.",
                    Impact = "Safe loading cannot be verified.",
                    Resolution = "Create the profile through the intended flow.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InvalidEntryId,
                new DiagnosticLocalizedFields
                {
                    Title = "マッピング項目IDが不正です",
                    Summary = "マッピング項目（エントリー）のIDが有効な識別子ではないか、項目が存在しません。",
                    Cause = "項目のIDが壊れているか、項目自体が破損しています。",
                    Impact = "項目を一意に識別できません。",
                    Resolution = "項目を修復するか、無効な項目を削除してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid mapping entry ID",
                    Summary = "A mapping entry ID is not a valid identifier, or the entry is missing.",
                    Cause = "An entry ID is malformed or the entry is corrupted.",
                    Impact = "The entry cannot be uniquely identified.",
                    Resolution = "Repair the entry, or remove invalid entries.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateEntryId,
                new DiagnosticLocalizedFields
                {
                    Title = "マッピング項目IDが重複しています",
                    Summary = "同じマッピング項目IDが複数存在します。",
                    Cause = "複数の項目に同じIDが使われています。",
                    Impact = "どちらの項目を指すのか確定できません。",
                    Resolution = "それぞれに一意なIDを割り当ててください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Duplicate mapping entry ID",
                    Summary = "More than one mapping entry uses the same ID.",
                    Cause = "Multiple entries share a single ID.",
                    Impact = "The intended entry cannot be determined.",
                    Resolution = "Assign a unique ID to each entry.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.MissingLogicalTargetId,
                new DiagnosticLocalizedFields
                {
                    Title = "論理ターゲットが未設定です",
                    Summary = "マッピング項目に論理ターゲットID（例: mouth.smile）がありません。",
                    Cause = "項目の論理ターゲットIDが空です。",
                    Impact = "どの論理ターゲットを表すのか特定できません。",
                    Resolution = "論理ターゲットIDを設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Logical target is not set",
                    Summary = "A mapping entry has no logical target ID (e.g. mouth.smile).",
                    Cause = "The entry's logical target ID is empty.",
                    Impact = "Which logical target the entry represents is unknown.",
                    Resolution = "Assign a logical target ID.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InvalidLogicalTargetKind,
                new DiagnosticLocalizedFields
                {
                    Title = "論理ターゲット種類が未対応です",
                    Summary = "論理ターゲットの種類（BlendShape/Transform）が対応していません。",
                    Cause = "未対応のターゲット種類が指定されています。",
                    Impact = "その項目は解決できません。",
                    Resolution = "対応している種類（BlendShape/Transform）を使用してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Unsupported logical target kind",
                    Summary = "The logical target kind (BlendShape/Transform) is not supported.",
                    Cause = "An unsupported target kind is specified.",
                    Impact = "The entry cannot be resolved.",
                    Resolution = "Use a supported kind (BlendShape or Transform).",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.MissingBinding,
                new DiagnosticLocalizedFields
                {
                    Title = "バインドが設定されていません",
                    Summary = "マッピング項目に対応する実際のバインド先（Renderer/BlendShape/Transform）がありません。",
                    Cause = "ターゲット種類に見合ったバインドが未設定です。",
                    Impact = "論理ターゲットをアバター上の要素へ結びつけられません。",
                    Resolution = "論理ターゲットにアバター上の具体的な要素をバインドしてください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Binding is not set",
                    Summary = "A mapping entry has no concrete binding (renderer/blend shape/transform) for its target kind.",
                    Cause = "The binding for the target kind is missing.",
                    Impact = "The logical target cannot be connected to an avatar element.",
                    Resolution = "Bind the logical target to a concrete avatar element.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InvalidFingerprint,
                new DiagnosticLocalizedFields
                {
                    Title = "保存された指紋が不正です",
                    Summary = "マッピングプロファイルに保存されているアバター指紋が壊れています。",
                    Cause = "指紋データが不正な形式で保存されています。",
                    Impact = "プロファイルがどのアバター用かを確認できません。",
                    Resolution = "アバターをスキャンして指紋を再設定してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Stored fingerprint is invalid",
                    Summary = "The avatar fingerprint stored in the mapping profile is malformed.",
                    Cause = "The fingerprint data was saved in an invalid form.",
                    Impact = "Which avatar the profile targets cannot be verified.",
                    Resolution = "Rescan the avatar to refresh the fingerprint.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.DuplicateLogicalTarget,
                new DiagnosticLocalizedFields
                {
                    Title = "論理ターゲットが重複しています",
                    Summary = "同じ論理ターゲットIDが複数の項目でバインドされています。",
                    Cause = "同じ論理ターゲットIDが2回以上使われています。",
                    Impact = "どちらのバインドを優先するかが曖昧になります。",
                    Resolution = "論理ターゲットIDを一意に整理してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Duplicate logical target",
                    Summary = "The same logical target ID is bound more than once.",
                    Cause = "A logical target ID is used by multiple entries.",
                    Impact = "Which binding is used becomes ambiguous.",
                    Resolution = "Make each logical target ID unique.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.MissingRenderer,
                new DiagnosticLocalizedFields
                {
                    Title = "Rendererが見つかりません",
                    Summary = "アバター上で対象のRendererが見つかりませんでした。",
                    Cause = "バインドしたRendererが存在しない、削除された、または名前が変わった可能性があります。",
                    Impact = "そのRenderer上のBlendShapeを制御できません。",
                    Resolution = "既存のRendererを指すようにバインドを修正してください。",
                    Caution = "アバター更新後に発生することがあります。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Renderer not found",
                    Summary = "The target renderer was not found on the avatar.",
                    Cause = "The bound renderer may be missing, removed, or renamed.",
                    Impact = "Blend shapes on that renderer cannot be controlled.",
                    Resolution = "Point the binding at an existing renderer.",
                    Caution = "This can occur after an avatar update."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.MissingBlendShape,
                new DiagnosticLocalizedFields
                {
                    Title = "BlendShapeが見つかりません",
                    Summary = "アバター上で対象のBlendShapeが見つかりませんでした。",
                    Cause = "メッシュに指定のBlendShapeが存在しない、または名前が変わった可能性があります。",
                    Impact = "そのBlendShapeを制御できません。",
                    Resolution = "既存のBlendShapeを指すようにバインドを修正してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Blend shape not found",
                    Summary = "The target blend shape was not found on the avatar.",
                    Cause = "The shape may not exist on the mesh or may have been renamed.",
                    Impact = "That blend shape cannot be controlled.",
                    Resolution = "Point the binding at an existing blend shape.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.MissingTransform,
                new DiagnosticLocalizedFields
                {
                    Title = "Transformが見つかりません",
                    Summary = "アバター上で対象のTransformが見つかりませんでした。",
                    Cause = "指定のTransformが存在しない、削除された、または名前が変わった可能性があります。",
                    Impact = "そのTransformを制御できません。",
                    Resolution = "既存のTransformを指すようにバインドを修正してください。",
                    Caution = ""
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Transform not found",
                    Summary = "The target transform was not found on the avatar.",
                    Cause = "The transform may be missing, removed, or renamed.",
                    Impact = "That transform cannot be controlled.",
                    Resolution = "Point the binding at an existing transform.",
                    Caution = ""
                });

            Add(ja, en, FaceMotionDiagnosticCodes.AmbiguousBinding,
                new DiagnosticLocalizedFields
                {
                    Title = "バインド対象が曖昧です",
                    Summary = "指定した対象がアバター上で複数該当し、一意に特定できません。",
                    Cause = "同じ名前のBlendShapeやTransformが複数ある可能性があります。",
                    Impact = "対象を安全に特定できないため、制御結果が安定しません。",
                    Resolution = "重複している名前を変更するか、対象を指し直してください。",
                    Caution = "どちらか一方に勝手に割り当てることはしません。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Binding target is ambiguous",
                    Summary = "Multiple avatar elements match the specified target.",
                    Cause = "Duplicate blend shape or transform names may exist.",
                    Impact = "The target cannot be identified safely, so control is unstable.",
                    Resolution = "Rename the duplicates, or rebind the target.",
                    Caution = "FaceMotion never assigns the target to one side arbitrarily."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.InvalidBinding,
                new DiagnosticLocalizedFields
                {
                    Title = "バインドが不正です",
                    Summary = "トラックやマッピングのバインド内容が正しくありません。",
                    Cause = "対象のパス・種類・データが欠けている、または無効な状態です。",
                    Impact = "そのトラックやマッピングは解決できません。",
                    Resolution = "対象を正しい状態にバインドし直してください。",
                    Caution = "同一コードで複数の原因が含まれます。詳細メッセージをご確認ください。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Invalid binding",
                    Summary = "A track or mapping binding is invalid.",
                    Cause = "The target path, kind, or data is missing or invalid.",
                    Impact = "The track or mapping cannot be resolved.",
                    Resolution = "Rebind the target correctly.",
                    Caution = "This code covers several causes; check the detail message."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.ProfileFingerprintMismatch,
                new DiagnosticLocalizedFields
                {
                    Title = "アバターの指紋が一致しません",
                    Summary = "プロファイル作成時のアバターと、現在のアバターの指紋が一致しません。",
                    Cause = "アバター構造が変更された可能性があります。",
                    Impact = "プロファイルは「古い」扱いになりますが、各バインドは個別に再検証されます。",
                    Resolution = "各バインドを再確認し、指紋を再登録してください。",
                    Caution = "指紋不一致だけではプロファイル全体が無効になりません。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Avatar fingerprint does not match",
                    Summary = "The current avatar fingerprint differs from the one stored in the profile.",
                    Cause = "The avatar structure may have changed.",
                    Impact = "The profile is treated as stale, but every binding is re-validated individually.",
                    Resolution = "Re-validate each binding, then rebind the fingerprint.",
                    Caution = "A mismatched fingerprint alone does not invalidate the profile."
                });

            Add(ja, en, FaceMotionDiagnosticCodes.StaleMappingButValid,
                new DiagnosticLocalizedFields
                {
                    Title = "指紋だけが古い状態です",
                    Summary = "すべてのバインドは一致していますが、プロファイルの指紋だけが古くなっています。",
                    Cause = "アバター構造は同じですが、指紋が未更新です。",
                    Impact = "現状では安全に利用できます。",
                    Resolution = "必要に応じて指紋を再登録してください。",
                    Caution = "指紋を更新しない限り、今後もこの案内が出ることがあります。"
                },
                new DiagnosticLocalizedFields
                {
                    Title = "Only the fingerprint is stale",
                    Summary = "Every binding still resolves, but the stored fingerprint is outdated.",
                    Cause = "The avatar structure is the same, but the fingerprint was not refreshed.",
                    Impact = "The profile can be used safely right now.",
                    Resolution = "Rebind the fingerprint when convenient.",
                    Caution = "Until refreshed, this notice may keep appearing."
                });
        }
    }
}