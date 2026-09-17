using System.Collections.Generic;
using UnityEngine;

namespace FaceMotion.Editor.UI.Localization
{
    /// <summary>Editor UI string table. Japanese is the product default; English is the fallback.</summary>
    public static class FaceMotionUiText
    {
        private static readonly Dictionary<string, string> Japanese = new Dictionary<string, string>
        {
            { "keyInspector", "キーインスペクター" },
            { "time", "時間" },
            { "noKeySelected", "キーが選択されていません。時間と値を設定してからキーを追加してください。" },
            { "blendShape", "ブレンドシェイプ" },
            { "value", "値" },
            { "interpolation", "補間" },
            { "apply", "適用" },
            { "revert", "元に戻す" },
            { "addKey", "キーを追加" },
            { "selectAll", "すべて選択" },
            { "delete", "削除" },
            { "snap", "スナップ" },
            { "noTracks", "トラックがありません。アニメーションを選択してトラックを追加してください。" },
            { "selectAnimation", "タイムラインを編集するアニメーションを選択または作成してください。" },
            { "keyTooltip", "キー: {0:0.###} 秒" },
            { "play", "再生" },
            { "pause", "一時停止" },
            { "fit", "全体表示" },
            { "zoomOut", "縮小" },
            { "zoomIn", "拡大" },
            { "file", "ファイル" }, { "avatar", "アバター" },
            { "fileTooltip", "FaceMotion プロジェクト操作" }, { "avatarTooltip", "アバターの選択とインデックス操作" },
            { "playTooltip", "プレビューを再生または一時停止" }, { "fitTooltip", "タイムラインをウィンドウに合わせる" },
            { "zoomOutLabel", "縮小 -" }, { "zoomInLabel", "拡大 +" },
            { "createProject", "FaceMotion プロジェクトを作成" }, { "openProject", "FaceMotion プロジェクトを開く" },
            { "saveProject", "FaceMotion プロジェクトを保存" }, { "validateProject", "プロジェクトを検証" },
            { "selectVrcAvatar", "VRC アバターを選択" }, { "rebuildAvatarIndex", "アバターインデックスを再構築" },
            { "diagnostics", "診断" }, { "noIssues", "問題はありません。" }, { "refresh", "更新" }, { "fix", "修正方法" },
            { "xLocal", "X (ローカル)" }, { "yLocal", "Y (ローカル)" }, { "zLocal", "Z (ローカル)" },
            { "hold", "保持" }, { "linear", "線形" }, { "easeIn", "イーズイン" }, { "easeOut", "イーズアウト" }, { "easeInOut", "イーズイン/アウト" }, { "smooth", "スムーズ" },
            { "diagnosticFallback", "詳細: {0}" }, { "diagnosticUnknown", "詳細は診断コードを参照してください。" }, { "diagnosticFixUnknown", "診断コードを確認し、入力または設定を見直してください。" },
            { "actionRequired", "解決が必要" }, { "actionRecommended", "推奨" }, { "actionInfo", "情報" },
            { "severityError", "エラー" }, { "severityWarning", "警告" }, { "severityInfo", "情報" },
            { "select", "選択" }, { "copyCode", "コードをコピー" }, { "copied", "コピーしました" },
            { "cause", "原因" }, { "impact", "影響" }, { "resolution", "対処方法" }, { "caution", "注意" }, { "context", "対象" }, { "conflictObject", "競合オブジェクト" }, { "conflictBinding", "競合binding" }, { "hierarchyPath", "階層" }, { "animatorController", "Animator Controller" }, { "conflictAnimationClip", "競合AnimationClip" }, { "component", "コンポーネント" }, { "category", "分類" }, { "details", "詳細" }, { "severity", "重大度" }, { "actionLevel", "対応レベル" }, { "technicalDetails", "技術詳細" }, { "rawDiagnosticMessage", "生成元メッセージ" }, { "contextId", "ContextId" }, { "diagnosticCode", "診断コード" }
            , { "project", "プロジェクト" }, { "openProjectDialog", "FaceMotion プロジェクトを開く" }, { "newProject", "新規プロジェクト" }, { "asset", "アセット" }, { "animations", "アニメーション" }, { "noProject", "（プロジェクトなし）" },
            { "selectAvatar", "アバターを選択" }, { "active", "使用中" }, { "unnamed", "（名前なし）" }, { "bindings", "バインディング" }, { "selectDescriptor", "VRC AvatarDescriptor を選択してください。" }, { "avatarIndexChanged", "アバターインデックスが変更されました。再構築を推奨します。" }, { "noDescriptor", "現在のシーンに VRCAvatarDescriptor が見つかりません。" }, { "clear", "クリア" }, { "ok", "OK" },
            { "duplicate", "複製" }, { "deleteAnimation", "アニメーションを削除" }, { "deleteAnimationConfirm", "\"{0}\" を削除しますか？" }, { "newAnimation", "+ 新規アニメーション" }, { "selected", "選択中" }, { "none", "（なし）" }, { "rename", "名前を変更" }, { "cancel", "キャンセル" }, { "timelineSettings", "タイムライン設定" }, { "duration", "長さ" }, { "frameRate", "フレームレート" }, { "loop", "ループ" },
            { "tracks", "トラック" }, { "search", "検索" }, { "filter", "フィルター" }, { "source", "ソース" }, { "classification", "分類根拠" }, { "browserAll", "すべて" }, { "browserConflicts", "競合のみ" }, { "browserSafe", "安全のみ" }, { "browserAdded", "追加済み" }, { "browserConflict", "競合" }, { "browserCaution", "注意" }, { "categoryFace", "Face" }, { "categoryHair", "Hair" }, { "categoryBody", "Body" }, { "categoryClothes", "Clothes" }, { "categoryOther", "Other" }, { "categoryEye", "Eye" }, { "categoryBlink", "Blink" }, { "categoryBrow", "Brow" }, { "categoryMouth", "Mouth" }, { "categoryFaceOther", "Face Other" }, { "addBlendShape", "+ ブレンドシェイプ" }, { "addTransform", "+ トランスフォーム" }, { "kind", "種類" }, { "position", "位置" }, { "rotation", "回転" }, { "scale", "スケール" }, { "selectAvatarForBlend", "ブレンドシェイプのバインディングを選択するにはアバターを選択してください。" }, { "selectAvatarForTransform", "トランスフォームのバインディングを選択するにはアバターを選択してください。" }, { "advancedManualBinding", "詳細な手動バインディング" }, { "rendererPath", "レンダラーパス" }, { "blendShapeName", "ブレンドシェイプ名" }, { "transformPath", "トランスフォームパス" }, { "addManualBlendShape", "手動ブレンドシェイプを追加" }, { "addManualTransform", "手動トランスフォームを追加" }, { "deleteShort", "削除" },
            { "presetsGeneration", "プリセットと生成" }, { "openProjectSelectAnimation", "生成モーションを適用するにはプロジェクトを開き、アニメーションを選択してください。" }, { "selectAvatarRebuildIndex", "論理プリセットの解決または生成対象の選択には、アバターを選択してインデックスを再構築してください。" }, { "builtIns", "標準プリセット" }, { "confirmUniqueMapping", "{0}: 一意のマッピングを先に確定してください" }, { "generateBlink", "まばたきを生成" }, { "confirmedMapping", "確定済みマッピング" }, { "createMappingProfile", "マッピングプロファイルを作成" }, { "createMappingProfileDialog", "FaceMotion マッピングプロファイルを作成" }, { "mappingProfilePrompt", "確定したアバターバインディングの保存先を選択してください。" }, { "profile", "プロファイル" }, { "noLogicalTargets", "一意に一致する論理ターゲットはありません。" }, { "confirm", "確定" }, { "randomBlendShape", "ランダムなブレンドシェイプ" }, { "noBlendShapes", "アバターにブレンドシェイプがありません。" }, { "target", "対象" }, { "generateRandomBlendShape", "ランダムなブレンドシェイプを生成" }, { "randomRotation", "ランダムな回転" }, { "generateRandomRotation", "ランダムな回転を生成" }, { "seed", "シード" }, { "step", "間隔" }, { "minimum", "最小値" }, { "maximum", "最大値" }, { "avatarRoot", "（アバタールート）" }, { "generationOutcome", "{0} 個のキーを追加しました。手動キー {1} 個を保護しました。" },
            { "animationClipExport", "AnimationClip エクスポート" }, { "selectAnimationToExport", "エクスポートするアニメーションを選択してください。" }, { "assetPath", "アセットパス" }, { "chooseExportPath", "エクスポート先を選択" }, { "exportAnimationClipDialog", "AnimationClip をエクスポート" }, { "exportPathPrompt", "Assets 配下の AnimationClip の保存先を選択してください。" }, { "exportAnimationClip", "AnimationClip をエクスポート" },
            { "directIntegration", "VRChat 統合" }, { "integrationBackend", "統合バックエンド" }, { "directBackend", "直接統合 (Direct)" }, { "modularAvatarBackend", "Modular Avatar (任意)" }, { "optionalBackendStatus", "任意バックエンドの状態" }, { "animationClip", "AnimationClip" }, { "outputFolder", "出力フォルダー" }, { "expressionBudgetPolicy", "パラメーター予算: Bool は 1 bit を使用します。合計 256 bit を超える統合は適用できません。" }, { "planIntegration", "統合を計画して検証" }, { "proposedParameter", "提案パラメーター" }, { "proposedFxLayer", "提案 FX レイヤー" }, { "generatedAssetStem", "生成アセット名" }, { "applyDirectIntegration", "直接統合を適用" }, { "proposedMaObject", "提案 MA オブジェクト" }, { "applyModularAvatarIntegration", "Modular Avatar 統合を適用" }, { "removeModularAvatarIntegration", "Modular Avatar 統合を削除" },
            { "oneClickIntegration", "VRChatへ追加（ワンクリック統合）" }, { "addToVrchat", "VRChatへ追加" }, { "oneClickStatusReady", "準備完了" }, { "oneClickStatusReview", "要確認" }, { "oneClickBackendLabel", "バックエンド" }, { "oneClickExportPathLabel", "出力クリップ" }, { "oneClickParameterLabel", "パラメーター" }, { "oneClickStageExport", "エクスポート中…" }, { "oneClickStagePlan", "計画中…" }, { "oneClickStageValidate", "検証中…" }, { "oneClickStageApply", "統合中…" }, { "advancedSettings", "詳細設定" }, { "oneClickResultSucceeded", "VRChatへの追加が完了しました" }, { "oneClickResultFailed", "VRChatへの追加に失敗しました" }, { "oneClickStepExport", "Step 1 Export" }, { "oneClickStepPlan", "Step 2 Plan" }, { "oneClickStepValidate", "Step 3 Validate" }, { "oneClickStepApply", "Step 4 Apply" }, { "succeededShort", "成功" }, { "failedShort", "失敗" }, { "notExecutedShort", "未実行" }, { "selectGeneratedObject", "生成オブジェクトを選択" }, { "revealExportPath", "出力先を開く" }, { "noAnimationSelectedShort", "アニメーション未選択" }, { "noAvatarSelectedShort", "アバター未選択" },
            { "preview", "プレビュー" }, { "previewIsolation", "プレビューは分離されています。生成モーションが自動でシーンに適用されることはありません。" }, { "selectAvatarToPreview", "選択中のアニメーションをプレビューするにはアバターを選択してください。" }, { "rebuildPreview", "プレビューを再構築" }, { "startPreview", "プレビューを開始" }, { "stopPreview", "プレビューを停止" }, { "previewPlay", "▶ 再生" }, { "previewPause", "⏸ 一時停止" }, { "previewStop", "■ 停止" }, { "stopSceneApply", "シーン適用を停止" }, { "applyToScene", "シーンに適用" }, { "fitAvatar", "アバターに合わせる" }, { "resetView", "ビューをリセット" }, { "previewControls", "Alt+左ドラッグ: 回転  中ドラッグ: 移動  ホイール: 拡大/縮小  F: アバターに合わせる" }, { "previewNoRenderers", "プレビューアバターにレンダラーがないため、ルートトランスフォームを表示します。" }, { "missing", "不足" }, { "empty", "空" }, { "fps", "fps" }, { "openWindowMenu", "FaceMotion ウィンドウを開く" },
            { "guidanceNextAction", "次にやること" }, { "guidanceStepFormat", "Step {0}" },
            { "guidanceHintAvatar", "アバターを選択してください" }, { "guidanceHintAnimation", "アニメーションを作成してください" },
            { "guidanceHintTrack", "ブレンドシェイプのトラックを追加してください" }, { "guidanceHintKey", "キーを追加してください" },
            { "guidanceHintReady", "プレビューで確認し、VRChatへ追加してください" }, { "guidanceCompleteBadge", "完了" },
            { "emptyAnimations", "まだアニメーションがありません" }, { "emptyTracks", "まだトラックがありません" }, { "emptyKeys", "このトラックにはまだキーがありません" },
            { "shortcutHelpTitle", "操作ヘルプ" }, { "shortcutTimelineZoom", "Ctrl + ホイール: タイムラインの拡大/縮小" }, { "shortcutDeleteKey", "Delete: 選択中のキーを削除" }, { "shortcutPreviewCamera", "プレビュー: Alt+左ドラッグで回転、中ドラッグで移動" }, { "shortcutFocus", "F: アバターにフォーカス" },
            { "tooltipBackend", "統合方式を選択します。変更は保存されます。" }, { "tooltipDuration", "アニメーションの長さ（秒）" }, { "tooltipFrameRate", "1秒あたりのキー評価フレームレート" }, { "tooltipLoop", "プレビューとエクスポートでループするか" }, { "tooltipTrack", "アニメーションする対象（トラック）" }, { "tooltipAddToVrchat", "エクスポートから統合までを一度に実行します" }, { "tooltipAdvancedSettings", "手動エクスポートやPlan/Validateなどの上級者向け設定" },
            { "backendModularAvatarDescription", "Modular Avatar: おすすめ。既存改変と共存しやすい方式です。" }, { "backendDirectDescription", "Direct: Animator Controllerへ直接統合する上級者向け方式です。" },
            { "successBackendLabel", "統合方式" }, { "successAnimationLabel", "アニメーション" }
        };

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "keyInspector", "Key Inspector" }, { "time", "Time" },
            { "noKeySelected", "No key selected. Set Time and values, then Add Key." },
            { "blendShape", "Blend Shape" }, { "value", "Value" }, { "interpolation", "Interp" },
            { "apply", "Apply" }, { "revert", "Revert" }, { "addKey", "Add Key" },
            { "selectAll", "Select All" }, { "delete", "Delete" }, { "snap", "Snap" },
            { "noTracks", "No tracks. Select an animation and add a track." },
            { "selectAnimation", "Select or create an animation to edit its timeline." },
            { "keyTooltip", "Key: {0:0.###} s" }, { "play", "Play" }, { "pause", "Pause" },
            { "fit", "Fit" }, { "zoomOut", "Zoom out" }, { "zoomIn", "Zoom in" }
            , { "file", "File" }, { "avatar", "Avatar" },
            { "fileTooltip", "FaceMotion project actions" }, { "avatarTooltip", "Avatar selection and index actions" },
            { "playTooltip", "Play/pause preview" }, { "fitTooltip", "Fit timeline to window" },
            { "zoomOutLabel", "Zoom -" }, { "zoomInLabel", "Zoom +" },
            { "createProject", "Create FaceMotion Project" }, { "openProject", "Open FaceMotion Project" },
            { "saveProject", "Save FaceMotion Project" }, { "validateProject", "Validate Project" },
            { "selectVrcAvatar", "Select VRC Avatar" }, { "rebuildAvatarIndex", "Rebuild Avatar Index" },
            { "diagnostics", "Diagnostics" }, { "noIssues", "No issues." }, { "refresh", "Refresh" }, { "fix", "Fix" },
            { "xLocal", "X (local)" }, { "yLocal", "Y (local)" }, { "zLocal", "Z (local)" },
            { "hold", "Hold" }, { "linear", "Linear" }, { "easeIn", "Ease In" }, { "easeOut", "Ease Out" }, { "easeInOut", "Ease In-Out" }, { "smooth", "Smooth" },
            { "diagnosticFallback", "Details: {0}" }, { "diagnosticUnknown", "See the diagnostic code for details." }, { "diagnosticFixUnknown", "Review the diagnostic code, input, and configuration." }
            , { "actionRequired", "Required" }, { "actionRecommended", "Recommended" }, { "actionInfo", "Info" },
            { "severityError", "Error" }, { "severityWarning", "Warning" }, { "severityInfo", "Info" },
            { "select", "Select" }, { "copyCode", "Copy code" }, { "copied", "Copied" },
            { "cause", "Cause" }, { "impact", "Impact" }, { "resolution", "Resolution" }, { "caution", "Caution" }, { "context", "Target" }, { "conflictObject", "Conflict object" }, { "conflictBinding", "Conflicting binding" }, { "hierarchyPath", "Hierarchy path" }, { "animatorController", "Animator Controller" }, { "conflictAnimationClip", "Conflicting AnimationClip" }, { "component", "Component" }, { "category", "Category" }, { "details", "Details" }, { "severity", "Severity" }, { "actionLevel", "Action" }, { "technicalDetails", "Technical details" }, { "rawDiagnosticMessage", "Raw diagnostic message" }, { "contextId", "ContextId" }, { "diagnosticCode", "Code" }
            , { "project", "Project" }, { "openProjectDialog", "Open FaceMotion Project" }, { "newProject", "New Project" }, { "asset", "Asset" }, { "animations", "Animations" }, { "noProject", "(no project)" },
            { "selectAvatar", "Select Avatar" }, { "active", "Active" }, { "unnamed", "(unnamed)" }, { "bindings", "Bindings" }, { "selectDescriptor", "Select a VRC AvatarDescriptor." }, { "avatarIndexChanged", "Avatar index changed; rebuild recommended." }, { "noDescriptor", "No VRCAvatarDescriptor found in the current scene." }, { "clear", "Clear" }, { "ok", "OK" },
            { "duplicate", "Dup" }, { "deleteAnimation", "Delete Animation" }, { "deleteAnimationConfirm", "Delete \"{0}\"?" }, { "newAnimation", "+ New Animation" }, { "selected", "Selected" }, { "none", "(none)" }, { "rename", "Rename" }, { "cancel", "Cancel" }, { "timelineSettings", "Timeline Settings" }, { "duration", "Duration" }, { "frameRate", "Frame Rate" }, { "loop", "Loop" },
            { "tracks", "Tracks" }, { "search", "Search" }, { "filter", "Filter" }, { "source", "Source" }, { "classification", "Classification" }, { "browserAll", "All" }, { "browserConflicts", "Conflicts" }, { "browserSafe", "Safe" }, { "browserAdded", "Added" }, { "browserConflict", "CONFLICT" }, { "browserCaution", "CAUTION" }, { "categoryFace", "Face" }, { "categoryHair", "Hair" }, { "categoryBody", "Body" }, { "categoryClothes", "Clothes" }, { "categoryOther", "Other" }, { "categoryEye", "Eye" }, { "categoryBlink", "Blink" }, { "categoryBrow", "Brow" }, { "categoryMouth", "Mouth" }, { "categoryFaceOther", "Face Other" }, { "addBlendShape", "+ BlendShape" }, { "addTransform", "+ Transform" }, { "kind", "Kind" }, { "position", "Position" }, { "rotation", "Rotation" }, { "scale", "Scale" }, { "selectAvatarForBlend", "Select an avatar to choose a BlendShape binding." }, { "selectAvatarForTransform", "Select an avatar to choose a Transform binding." }, { "advancedManualBinding", "Advanced manual binding" }, { "rendererPath", "Renderer Path" }, { "blendShapeName", "BlendShape Name" }, { "transformPath", "Transform Path" }, { "addManualBlendShape", "Add Manual BlendShape" }, { "addManualTransform", "Add Manual Transform" }, { "deleteShort", "Del" },
            { "presetsGeneration", "Presets & Generation" }, { "openProjectSelectAnimation", "Open a project and select an animation to apply generated motion." }, { "selectAvatarRebuildIndex", "Select an avatar and rebuild its index to resolve logical presets or choose generation targets." }, { "builtIns", "Built-ins" }, { "confirmUniqueMapping", "{0}: confirm a unique mapping first" }, { "generateBlink", "Generate Blink" }, { "confirmedMapping", "Confirmed Mapping" }, { "createMappingProfile", "Create Mapping Profile" }, { "createMappingProfileDialog", "Create FaceMotion Mapping Profile" }, { "mappingProfilePrompt", "Choose where to persist confirmed avatar bindings." }, { "profile", "Profile" }, { "noLogicalTargets", "No unique logical target matches." }, { "confirm", "Confirm" }, { "randomBlendShape", "Random Blend Shape" }, { "noBlendShapes", "Avatar has no blend shapes." }, { "target", "Target" }, { "generateRandomBlendShape", "Generate Random Blend Shape" }, { "randomRotation", "Random Rotation" }, { "generateRandomRotation", "Generate Random Rotation" }, { "seed", "Seed" }, { "step", "Step" }, { "minimum", "Minimum" }, { "maximum", "Maximum" }, { "avatarRoot", "(Avatar Root)" }, { "generationOutcome", "Added {0} key(s); protected {1} manual key(s)." },
            { "animationClipExport", "AnimationClip Export" }, { "selectAnimationToExport", "Select an animation to export." }, { "assetPath", "Asset Path" }, { "chooseExportPath", "Choose Export Path" }, { "exportAnimationClipDialog", "Export AnimationClip" }, { "exportPathPrompt", "Choose an AnimationClip location under Assets." }, { "exportAnimationClip", "Export AnimationClip" },
            { "directIntegration", "VRChat Integration" }, { "integrationBackend", "Integration Backend" }, { "directBackend", "Direct" }, { "modularAvatarBackend", "Modular Avatar (optional)" }, { "optionalBackendStatus", "Optional Backend Status" }, { "animationClip", "AnimationClip" }, { "outputFolder", "Output Folder" }, { "expressionBudgetPolicy", "Parameter budget: a Bool uses 1 bit. Integration is blocked above 256 bits." }, { "planIntegration", "Plan and Validate Integration" }, { "proposedParameter", "Proposed Parameter" }, { "proposedFxLayer", "Proposed FX Layer" }, { "generatedAssetStem", "Generated Asset Stem" }, { "applyDirectIntegration", "Apply Direct Integration" }, { "proposedMaObject", "Proposed MA Object" }, { "applyModularAvatarIntegration", "Apply Modular Avatar Integration" }, { "removeModularAvatarIntegration", "Remove Modular Avatar Integration" },
            { "oneClickIntegration", "VRChat One-Click Integration" }, { "addToVrchat", "Add to VRChat" }, { "oneClickStatusReady", "Ready" }, { "oneClickStatusReview", "Review Needed" }, { "oneClickBackendLabel", "Backend" }, { "oneClickExportPathLabel", "Export Clip" }, { "oneClickParameterLabel", "Parameter" }, { "oneClickStageExport", "Exporting..." }, { "oneClickStagePlan", "Planning..." }, { "oneClickStageValidate", "Validating..." }, { "oneClickStageApply", "Integrating..." }, { "advancedSettings", "Advanced Settings" }, { "oneClickResultSucceeded", "Added to VRChat" }, { "oneClickResultFailed", "Failed to add to VRChat" }, { "oneClickStepExport", "Step 1 Export" }, { "oneClickStepPlan", "Step 2 Plan" }, { "oneClickStepValidate", "Step 3 Validate" }, { "oneClickStepApply", "Step 4 Apply" }, { "succeededShort", "OK" }, { "failedShort", "FAIL" }, { "notExecutedShort", "NOT RUN" }, { "selectGeneratedObject", "Select Generated Object" }, { "revealExportPath", "Reveal Output" }, { "noAnimationSelectedShort", "No Animation" }, { "noAvatarSelectedShort", "No Avatar" },
            { "preview", "Preview" }, { "previewIsolation", "Preview is isolated; generated motion never applies to the scene automatically." }, { "selectAvatarToPreview", "Select an avatar to preview the selected animation." }, { "rebuildPreview", "Rebuild Preview" }, { "startPreview", "Start Preview" }, { "stopPreview", "Stop Preview" }, { "previewPlay", "Play" }, { "previewPause", "Pause" }, { "previewStop", "Stop" }, { "stopSceneApply", "Stop Scene Apply" }, { "applyToScene", "Apply To Scene" }, { "fitAvatar", "Fit Avatar" }, { "resetView", "Reset View" }, { "previewControls", "Alt+LMB Orbit  MMB Pan  Wheel Zoom  F Focus" }, { "previewNoRenderers", "Preview avatar has no renderers; framing its root transform." }, { "missing", "MISSING" }, { "empty", "(empty)" }, { "fps", "fps" }, { "openWindowMenu", "Open FaceMotion Window" },
            { "guidanceNextAction", "Next" }, { "guidanceStepFormat", "Step {0}" },
            { "guidanceHintAvatar", "Select an avatar" }, { "guidanceHintAnimation", "Create an animation" },
            { "guidanceHintTrack", "Add a BlendShape track" }, { "guidanceHintKey", "Add a key" },
            { "guidanceHintReady", "Preview, then add to VRChat" }, { "guidanceCompleteBadge", "Done" },
            { "emptyAnimations", "No animations yet" }, { "emptyTracks", "No tracks yet" }, { "emptyKeys", "This track has no keys yet" },
            { "shortcutHelpTitle", "Shortcut Help" }, { "shortcutTimelineZoom", "Ctrl + Wheel: zoom the timeline" }, { "shortcutDeleteKey", "Delete: remove selected keys" }, { "shortcutPreviewCamera", "Preview: Alt+LMB orbit, MMB pan" }, { "shortcutFocus", "F: focus the avatar" },
            { "tooltipBackend", "Choose the integration method; the change is saved." }, { "tooltipDuration", "Animation length in seconds" }, { "tooltipFrameRate", "Key evaluation frames per second" }, { "tooltipLoop", "Loop during preview and export" }, { "tooltipTrack", "Animated target (track)" }, { "tooltipAddToVrchat", "Run export through integration in one step" }, { "tooltipAdvancedSettings", "Advanced manual export and Plan/Validate controls" },
            { "backendModularAvatarDescription", "Modular Avatar: recommended; coexists with existing modifications." }, { "backendDirectDescription", "Direct: advanced; integrates directly into the Animator Controller." },
            { "successBackendLabel", "Integration" }, { "successAnimationLabel", "Animation" }
        };

        // Frequently surfaced diagnostic codes. Unknown diagnostics retain their original detail.
        private static readonly Dictionary<string, string> JapaneseDiagnostics = new Dictionary<string, string>
        {
            { "FM-UI-0001", "プロジェクトアセットを作成できませんでした。" },
            { "FM-UI-0002", "FaceMotion プロジェクトを読み込めませんでした。" },
            { "FM-UI-0003", "選択が無効です。" },
            { "FM-UI-0004", "貼り付け先のトラックが見つかりません。" },
            { "FM-UI-0005", "貼り付け先の時刻は既存のキーと重複しています。" },
            { "FM-EXPORT-SUCCEEDED", "AnimationClip をエクスポートしました。" },
            { "FM-EXPORT-NO-TIMELINE", "タイムラインを含むアニメーションを選択してください。" },
            { "FM-EXPORT-INVALID-DURATION", "タイムラインの長さは有限の正数である必要があります。" },
            { "FM-EXPORT-INVALID-FRAMERATE", "タイムラインのフレームレートは有限の正数である必要があります。" },
            { "FM-EXPORT-INVALID-PATH", "エクスポート先は Assets 内の .anim アセットである必要があります。" },
            { "FM-EXPORT-PATH-OCCUPIED", "出力先は AnimationClip 以外のアセットによって使用されています。" },
            { "FM-G-AVATAR", "VRCAvatarDescriptor を選択してください。" },
            { "FM-G-CLIP", "統合前に AnimationClip をエクスポートまたは選択してください。" },
            { "FM-G-PATH", "出力フォルダーは Assets 配下の既存フォルダーである必要があります。" },
            { "FM-G-BUDGET", "Bool パラメーターの追加により VRChat の式パラメーター予算 (256 bit) を超えます。" },
            { "FM-G-PREFAB-ASSET", "Prefab アセットは直接統合で変更できません。シーン内のアバターを使用してください。" },
            { "FM-G-APPLIED", "コピーオンライトのアセットを使用して VRChat 直接統合を適用しました。" },
            { "FM-H-MA-NOT-INSTALLED", "Modular Avatar パッケージがインストールされていません。" },
            { "FM-H-MA-METADATA-UNAVAILABLE", "Unity のパッケージメタデータを取得できませんでした。" },
            { "FM-H-MA-VERSION-UNAVAILABLE", "Modular Avatar は検出されましたが、パッケージバージョンを取得できませんでした。" },
            { "FM-H-MA-NOT-IMPLEMENTED", "Modular Avatar は検出されましたが、この任意バックエンドは未実装です。" },
            { "FM-H-MA-PREFAB-ASSET", "Prefab アセットは Modular Avatar 統合で変更できません。シーン内のアバターを使用してください。" },
            { "FM-GEN-0004", "手動キーが同じ時刻を使用しているため、生成キーをスキップしました。" }
        };

        public static string Get(string key)
        {
            return Get(key, SystemLanguage.Japanese);
        }

        internal static string Get(string key, SystemLanguage language)
        {
            var table = language == SystemLanguage.Japanese ? Japanese : English;
            return table.TryGetValue(key, out var text) ? text : key;
        }

        public static string FormatDiagnostic(string code, string message, string suggestedFix)
        {
            return FormatDiagnostic(code, message, suggestedFix, SystemLanguage.Japanese);
        }

        internal static string FormatDiagnostic(string code, string message, string suggestedFix, SystemLanguage language)
        {
            string detail;
            string fix;
            if (language != SystemLanguage.Japanese)
            {
                detail = message;
                fix = suggestedFix;
            }
            else
            {
                detail = JapaneseDiagnostics.TryGetValue(code ?? string.Empty, out var localized)
                    ? localized
                    : Get("diagnosticUnknown");
                fix = string.IsNullOrEmpty(suggestedFix) ? string.Empty : Get("diagnosticFixUnknown");
            }

            if (string.IsNullOrEmpty(fix)) return "[" + code + "] " + detail;
            return "[" + code + "] " + detail + "\n" + Get("fix", language) + ": " + fix;
        }
    }
}
