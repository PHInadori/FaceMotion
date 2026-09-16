using System;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.UI.Localization;

namespace FaceMotion.Editor.UI.Diagnostics
{
    /// <summary>
    /// Read-only, display-ready projection of a diagnostic for the UI. It combines the raw
    /// instance, the non-UI definition, and the localized catalog, and applies the per-field
    /// fallback chain: preferred language, other language, then the raw diagnostic data.
    /// This class never mutates assets or the scene and never triggers import or save.
    /// </summary>
    public sealed class FaceMotionDiagnosticPresentation
    {
        private const string GenericTitleJa = "FaceMotionから診断が報告されました";
        private const string GenericTitleEn = "FaceMotion reported a diagnostic";

        public FaceMotionDiagnosticPresentation(FaceMotionDiagnostic diagnostic, FaceMotionDiagnosticLanguage language)
            : this(diagnostic,
                FaceMotionDiagnosticDefinitionRegistry.Get(diagnostic == null ? null : diagnostic.Code),
                language)
        {
        }

        public FaceMotionDiagnosticPresentation(
            FaceMotionDiagnostic diagnostic,
            DiagnosticDefinition definition,
            FaceMotionDiagnosticLanguage language)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            Diagnostic = diagnostic;
            Definition = definition;
            Language = language;

            FaceMotionDiagnosticLocalizationCatalog.TryGet(diagnostic.Code, language, out DiagnosticLocalizedText preferred);
            FaceMotionDiagnosticLocalizationCatalog.TryGet(
                diagnostic.Code,
                FaceMotionDiagnosticLocalizationCatalog.Other(language),
                out DiagnosticLocalizedText other);

            Code = diagnostic.Code;
            Severity = diagnostic.Severity;
            ActionLevel = FaceMotionDiagnosticActionLevelResolver.Resolve(diagnostic, definition);
            IsKnown = definition != null;
            Category = definition != null ? definition.Category : "general";
            CanAutoFix = definition != null && definition.CanAutoFix;
            SelectionKind = definition != null
                ? definition.SelectionKind
                : FaceMotionDiagnosticSelectionKind.None;
            HelpTopicId = definition != null ? definition.HelpTopicId : diagnostic.Code;
            ContextId = diagnostic.ContextId;
            Detail = diagnostic.Message;
            Details = diagnostic.Details;

            Title = ResolveField(preferred, other, t => t.Title, GenericTitle());
            Summary = ResolveField(preferred, other, t => t.Summary, diagnostic.Message);
            Cause = ResolveField(preferred, other, t => t.Cause, string.Empty);
            Impact = ResolveField(preferred, other, t => t.Impact, string.Empty);
            Resolution = ResolveField(preferred, other, t => t.Resolution, diagnostic.SuggestedFix);
            Caution = ResolveField(preferred, other, t => t.Caution, string.Empty);

            ApplyAmbiguousModularAvatarBindingGuidance(diagnostic);

            ActionLevelText = ActionLevelLabel(ActionLevel, language);
            SeverityText = SeverityLabel(Severity, language);
        }

        /// <summary>The original diagnostic instance.</summary>
        public FaceMotionDiagnostic Diagnostic { get; }

        /// <summary>The non-UI definition, or null when the code is unknown to this version.</summary>
        public DiagnosticDefinition Definition { get; }

        public FaceMotionDiagnosticLanguage Language { get; }

        /// <summary>True when the code is registered in this version of FaceMotion.</summary>
        public bool IsKnown { get; }

        public string Code { get; }

        public FaceMotionDiagnosticSeverity Severity { get; }

        public string SeverityText { get; }

        public FaceMotionDiagnosticActionLevel ActionLevel { get; }

        public string ActionLevelText { get; }

        public string Category { get; }

        public bool CanAutoFix { get; }

        public FaceMotionDiagnosticSelectionKind SelectionKind { get; private set; }

        public string HelpTopicId { get; }

        /// <summary>Display title localized for the current language; falls back to a generic title.</summary>
        public string Title { get; private set; }

        /// <summary>One-line explanation; falls back to the raw diagnostic message.</summary>
        public string Summary { get; private set; }

        public string Cause { get; private set; }

        public string Impact { get; private set; }

        /// <summary>How to resolve; falls back to the raw suggested fix (or empty).</summary>
        public string Resolution { get; private set; }

        public string Caution { get; private set; }

        /// <summary>Instance-specific context reference (object path, project id, etc.). May be empty.</summary>
        public string ContextId { get; }

        /// <summary>The raw, instance-specific message from the generator.</summary>
        public string Detail { get; }

        public System.Collections.Generic.IReadOnlyDictionary<string, string> Details { get; }

        private static string ResolveField(
            DiagnosticLocalizedText preferred,
            DiagnosticLocalizedText other,
            Func<DiagnosticLocalizedText, string> select,
            string fallback)
        {
            string value = preferred != null ? select(preferred) : null;
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = other != null ? select(other) : null;
            return !string.IsNullOrEmpty(value) ? value : fallback ?? string.Empty;
        }

        private string GenericTitle()
        {
            return Language == FaceMotionDiagnosticLanguage.Japanese ? GenericTitleJa : GenericTitleEn;
        }

        private void ApplyAmbiguousModularAvatarBindingGuidance(FaceMotionDiagnostic diagnostic)
        {
            if (diagnostic.Code != FaceMotionDiagnosticCodes.ModularAvatarBindingConflict
                || diagnostic.Details == null
                || !diagnostic.Details.TryGetValue(FaceMotionDiagnosticDetailKeys.Reason, out string reason)
                || reason != FaceMotionDiagnosticDetailKeys.ReasonAmbiguousRelativePath)
            {
                return;
            }

            SelectionKind = FaceMotionDiagnosticSelectionKind.ParentOfAmbiguousPath;

            if (Language == FaceMotionDiagnosticLanguage.Japanese)
            {
                Title = "Modular Avatarのアニメーション対象を一意に特定できません";
                Summary = "AnimationClipが参照している対象と同じ相対パスを持つオブジェクトがアバター内に複数あります。FaceMotionは安全に統合先を判断できないため、Modular Avatar統合を停止しました。";
                Cause = "Contextに表示されたAnimationClip bindingが、重複した相対パスを参照しています。";
                Impact = "この状態ではModular Avatar統合を適用できません。アバターや既存アセットは変更されていません。";
                Resolution = "下の診断一覧で「同じ名前のオブジェクトが見つかりました」（FM-AVT-0007）を確認してください。「選択」で対象を確認し、重複しているオブジェクト名またはパスを一意にしてから、もう一度「統合を計画して検証」を実行してください。";
                Caution = "重複したまま統合すると、意図しないオブジェクトへアニメーションが適用される可能性があります。";
            }
            else
            {
                Title = "Modular Avatar animation target cannot be uniquely resolved";
                Summary = "Multiple objects in the avatar match the same relative path referenced by the AnimationClip. FaceMotion cannot safely determine the intended target, so Modular Avatar integration has been blocked.";
                Cause = "The AnimationClip binding shown in Context references a duplicated relative path.";
                Impact = "The integration cannot be applied in this state. No avatar or existing assets have been modified.";
                Resolution = "Check the related FM-AVT-0007 diagnostic below. Use Select to locate the duplicated objects, make their names or paths unique, then run Plan and Validate Integration again.";
                Caution = "Applying animation bindings while the target is ambiguous could affect an unintended object.";
            }
        }

        private static string ActionLevelLabel(
            FaceMotionDiagnosticActionLevel level,
            FaceMotionDiagnosticLanguage language)
        {
            switch (level)
            {
                case FaceMotionDiagnosticActionLevel.Required:
                    return UiText("actionRequired", language);
                case FaceMotionDiagnosticActionLevel.Info:
                    return UiText("actionInfo", language);
                default:
                    return UiText("actionRecommended", language);
            }
        }

        private static string SeverityLabel(
            FaceMotionDiagnosticSeverity severity,
            FaceMotionDiagnosticLanguage language)
        {
            switch (severity)
            {
                case FaceMotionDiagnosticSeverity.Error:
                    return UiText("severityError", language);
                case FaceMotionDiagnosticSeverity.Warning:
                    return UiText("severityWarning", language);
                default:
                    return UiText("severityInfo", language);
            }
        }

        private static string UiText(string key, FaceMotionDiagnosticLanguage language)
        {
            return FaceMotionUiText.Get(
                key,
                language == FaceMotionDiagnosticLanguage.Japanese
                    ? UnityEngine.SystemLanguage.Japanese
                    : UnityEngine.SystemLanguage.English);
        }
    }
}
