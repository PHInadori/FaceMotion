using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.UI.Diagnostics;
using FaceMotion.Editor.UI.Localization;
using NUnit.Framework;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Contract tests for the diagnostic registries, the action level resolver, and the
    /// presentation fallback chain. These tests never touch the scene or assets.
    /// </summary>
    public sealed class DiagnosticsContractsTests
    {
        private const int ExpectedCodeCount = 152;

        [Test]
        public void Codes_ConstantsMatchRegisteredKeys_NoDuplicates()
        {
            FieldInfo[] fields = typeof(FaceMotionDiagnosticCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static);
            IReadOnlyDictionary<string, DiagnosticDefinition> registry = FaceMotionDiagnosticDefinitionRegistry.All;
            IReadOnlyDictionary<string, DiagnosticLocalizedText> ja = FaceMotionDiagnosticLocalizationCatalog.JapaneseEntries;
            IReadOnlyDictionary<string, DiagnosticLocalizedText> en = FaceMotionDiagnosticLocalizationCatalog.EnglishEntries;

            var values = new List<string>(fields.Length);
            foreach (FieldInfo field in fields)
            {
                if (!field.IsLiteral || field.FieldType != typeof(string))
                {
                    continue;
                }

                string value = (string)field.GetValue(null);
                values.Add(value);
                Assert.That(registry.ContainsKey(value),
                    "Constant {0} = {1} is missing from the definition registry.", field.Name, value);
                Assert.That(ja.ContainsKey(value),
                    "Constant {0} = {1} is missing from the Japanese catalog.", field.Name, value);
                Assert.That(en.ContainsKey(value),
                    "Constant {0} = {1} is missing from the English catalog.", field.Name, value);
            }

            Assert.That(values.Count, Is.EqualTo(ExpectedCodeCount),
                "The codes class must expose exactly {0} string constants.", ExpectedCodeCount);
            Assert.That(values.Distinct().Count(), Is.EqualTo(values.Count),
                "Two constants must never share the same code value.");
        }

        [Test]
        public void Registry_KnownCountMatchesExpected()
        {
            Assert.That(FaceMotionDiagnosticDefinitionRegistry.All.Count, Is.EqualTo(ExpectedCodeCount));
        }

        [Test]
                public void Localization_JapaneseAndEnglishEachMatchExpectedCount_NoDivergence()
        {
            IReadOnlyDictionary<string, DiagnosticLocalizedText> ja = FaceMotionDiagnosticLocalizationCatalog.JapaneseEntries;
            IReadOnlyDictionary<string, DiagnosticLocalizedText> en = FaceMotionDiagnosticLocalizationCatalog.EnglishEntries;
            IReadOnlyDictionary<string, DiagnosticDefinition> registry = FaceMotionDiagnosticDefinitionRegistry.All;

            Assert.That(ja.Count, Is.EqualTo(ExpectedCodeCount), "Japanese catalog count.");
            Assert.That(en.Count, Is.EqualTo(ExpectedCodeCount), "English catalog count.");

            HashSet<string> jaKeys = new HashSet<string>(ja.Keys);
            HashSet<string> enKeys = new HashSet<string>(en.Keys);
            HashSet<string> registryKeys = new HashSet<string>(registry.Keys);

            Assert.That(enKeys.SetEquals(jaKeys),
                "Japanese and English catalogs must cover the same codes.");
            Assert.That(registryKeys.SetEquals(jaKeys),
                "Registry and localization must cover the same codes.");
        }

        [Test]
        public void Definitions_NeverRegisterRequiredAsNonBlockingLevel()
        {
            foreach (KeyValuePair<string, DiagnosticDefinition> pair in FaceMotionDiagnosticDefinitionRegistry.All)
            {
                Assert.That(pair.Value.NonBlockingActionLevel,
                    Is.Not.EqualTo(FaceMotionDiagnosticActionLevel.Required),
                    "NonBlockingActionLevel for {0} must be Recommended or Info.", pair.Key);
            }

            Assert.That(
                () => new DiagnosticDefinition("FM-X-0001", FaceMotionDiagnosticActionLevel.Required, "x"),
                Throws.ArgumentException);
        }

        [Test]
        public void ActionLevel_BlockingAlwaysRequired()
        {
            var diagnostic = new FaceMotionDiagnostic(
                "ANY-0001",
                FaceMotionDiagnosticSeverity.Info,
                "Message",
                string.Empty,
                blocking: true,
                string.Empty);

            Assert.That(
                FaceMotionDiagnosticActionLevelResolver.Resolve(diagnostic, null),
                Is.EqualTo(FaceMotionDiagnosticActionLevel.Required));
        }

        [Test]
        public void ActionLevel_NonBlockingKnownFollowsPolicy()
        {
            foreach (KeyValuePair<string, DiagnosticDefinition> pair in FaceMotionDiagnosticDefinitionRegistry.All)
            {
                var diagnostic = new FaceMotionDiagnostic(
                    pair.Key,
                    FaceMotionDiagnosticSeverity.Warning,
                    "Message",
                    string.Empty,
                    blocking: false,
                    string.Empty);

                Assert.That(
                    FaceMotionDiagnosticActionLevelResolver.Resolve(diagnostic, pair.Value),
                    Is.EqualTo(pair.Value.NonBlockingActionLevel),
                    "Non-blocking resolution for {0}.", pair.Key);
            }
        }

        [Test]
                public void ActionLevel_InfoCodesResolveToInfo_ExactlyFourteen()
        {
            var infoCodes = FaceMotionDiagnosticDefinitionRegistry.All
                .Where(pair => pair.Value.NonBlockingActionLevel == FaceMotionDiagnosticActionLevel.Info)
                .Select(pair => pair.Key)
                .ToArray();

            Assert.That(infoCodes.Length, Is.EqualTo(14),
                "Expected exactly fourteen Info codes, got: " + string.Join(",", infoCodes));

            foreach (string code in infoCodes)
            {
                var diagnostic = new FaceMotionDiagnostic(
                    code,
                    FaceMotionDiagnosticSeverity.Warning,
                    "Message",
                    string.Empty,
                    blocking: false,
                    string.Empty);

                Assert.That(
                    FaceMotionDiagnosticActionLevelResolver.Resolve(diagnostic, FaceMotionDiagnosticDefinitionRegistry.Get(code)),
                    Is.EqualTo(FaceMotionDiagnosticActionLevel.Info),
                    "Info resolution for {0}.", code);
            }
        }

        [Test]
        public void ActionLevel_UnknownCode_InfoStaysInfo_WarningRecommended()
        {
            var info = new FaceMotionDiagnostic(
                "FM-UNKNOWN-0001",
                FaceMotionDiagnosticSeverity.Info,
                "Message",
                string.Empty,
                blocking: false,
                string.Empty);
            Assert.That(
                FaceMotionDiagnosticActionLevelResolver.Resolve(info, null),
                Is.EqualTo(FaceMotionDiagnosticActionLevel.Info));

            var warning = new FaceMotionDiagnostic(
                "FM-UNKNOWN-0002",
                FaceMotionDiagnosticSeverity.Warning,
                "Message",
                string.Empty,
                blocking: false,
                string.Empty);
            Assert.That(
                FaceMotionDiagnosticActionLevelResolver.Resolve(warning, null),
                Is.EqualTo(FaceMotionDiagnosticActionLevel.Recommended));

            var error = new FaceMotionDiagnostic(
                "FM-UNKNOWN-0003",
                FaceMotionDiagnosticSeverity.Error,
                "Message",
                string.Empty,
                blocking: false,
                string.Empty);
            Assert.That(
                FaceMotionDiagnosticActionLevelResolver.Resolve(error, null),
                Is.EqualTo(FaceMotionDiagnosticActionLevel.Recommended));

            Assert.That(FaceMotionDiagnosticActionLevelResolver.Resolve(null, null),
                Is.EqualTo(FaceMotionDiagnosticActionLevel.Info));
        }

        [Test]
        public void Presentation_KnownCode_UsesLocalizedTitlePerLanguage()
        {
            const string code = FaceMotionDiagnosticCodes.DuplicateTransformPath;

            var diagnostic = new FaceMotionDiagnostic(
                code,
                FaceMotionDiagnosticSeverity.Error,
                "Raw message",
                "Body/Face",
                blocking: true,
                "Suggested fix");

            var ja = new FaceMotionDiagnosticPresentation(
                diagnostic,
                FaceMotionDiagnosticLanguage.Japanese);
            var en = new FaceMotionDiagnosticPresentation(
                diagnostic,
                FaceMotionDiagnosticLanguage.English);

            FaceMotionDiagnosticLocalizationCatalog.TryGet(code, FaceMotionDiagnosticLanguage.Japanese, out DiagnosticLocalizedText jaText);
            FaceMotionDiagnosticLocalizationCatalog.TryGet(code, FaceMotionDiagnosticLanguage.English, out DiagnosticLocalizedText enText);

            Assert.That(ja.IsKnown, Is.True);
            Assert.That(ja.Title, Is.EqualTo(jaText.Title));
            Assert.That(ja.Summary, Is.EqualTo(jaText.Summary),
                "Known diagnostics must keep their localized summary as normal user-facing data.");
            Assert.That(ja.Resolution, Is.Not.Empty);
            Assert.That(ja.Code, Is.EqualTo(code));
            Assert.That(ja.ContextId, Is.EqualTo("Body/Face"));
            Assert.That(ja.ActionLevel, Is.EqualTo(FaceMotionDiagnosticActionLevel.Required));
            Assert.That(ja.ActionLevelText, Is.EqualTo("解決が必要"));
            Assert.That(ja.Detail, Is.EqualTo("Raw message"),
                "The raw generator message must remain available for technical details.");

            Assert.That(en.Title, Is.EqualTo(enText.Title));
            Assert.That(en.Summary, Is.EqualTo(enText.Summary));
            Assert.That(en.ActionLevelText, Is.EqualTo("Required"));
            Assert.That(ja.Title, Is.Not.EqualTo(en.Title));
        }

        [Test]
        public void Presentation_UnexpectedIntegrationPlan_UsesLocalizedSafetyGuidanceAndPreservesTechnicalDetail()
        {
            var diagnostic = new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.ModularAvatarPlanUnexpected,
                FaceMotionDiagnosticSeverity.Error,
                "Exception: ArgumentOutOfRangeException\nStack trace:\nexample",
                "modular-avatar/plan/Avatar",
                blocking: true,
                "Retry");

            var ja = new FaceMotionDiagnosticPresentation(diagnostic, FaceMotionDiagnosticLanguage.Japanese);
            var en = new FaceMotionDiagnosticPresentation(diagnostic, FaceMotionDiagnosticLanguage.English);

            Assert.That(ja.Title, Is.EqualTo("VRChat統合の計画を作成できませんでした"));
            Assert.That(ja.Summary, Is.EqualTo("統合設定の処理中に予期しない問題が発生しました。"));
            Assert.That(ja.Resolution, Is.Not.Empty);
            Assert.That(en.Title, Is.EqualTo("Could not create the VRChat integration plan"));
            Assert.That(en.Summary, Is.EqualTo("An unexpected problem occurred while processing the integration settings."));
            Assert.That(ja.ActionLevel, Is.EqualTo(FaceMotionDiagnosticActionLevel.Required));
            Assert.That(ja.Detail, Does.Contain("ArgumentOutOfRangeException"));
            Assert.That(ja.Detail, Does.Contain("Stack trace:"));
        }

        [Test]
        public void Presentation_AmbiguousModularAvatarBindingGuidesToFmAvt0007()
        {
            var diagnostic = new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.ModularAvatarBindingConflict,
                FaceMotionDiagnosticSeverity.Error,
                "Raw binding detail",
                "Body/Face",
                blocking: true,
                "Raw suggested fix",
                new Dictionary<string, string>
                {
                    { FaceMotionDiagnosticDetailKeys.Reason, FaceMotionDiagnosticDetailKeys.ReasonAmbiguousRelativePath }
                });

            var ja = new FaceMotionDiagnosticPresentation(diagnostic, FaceMotionDiagnosticLanguage.Japanese);
            var en = new FaceMotionDiagnosticPresentation(diagnostic, FaceMotionDiagnosticLanguage.English);

            Assert.That(ja.Title, Is.EqualTo("Modular Avatarのアニメーション対象を一意に特定できません"));
            Assert.That(ja.Summary, Does.Contain("同じ相対パス"));
            Assert.That(ja.Resolution, Does.Contain("FM-AVT-0007"));
            Assert.That(ja.Resolution, Does.Contain("統合を計画して検証"));
            Assert.That(en.Title, Is.EqualTo("Modular Avatar animation target cannot be uniquely resolved"));
            Assert.That(en.Summary, Does.Contain("same relative path"));
            Assert.That(en.Resolution, Does.Contain("FM-AVT-0007"));
            Assert.That(en.Resolution, Does.Contain("Plan and Validate Integration"));
            Assert.That(ja.ActionLevel, Is.EqualTo(FaceMotionDiagnosticActionLevel.Required));
            Assert.That(ja.Detail, Is.EqualTo("Raw binding detail"));
            Assert.That(ja.SelectionKind, Is.EqualTo(FaceMotionDiagnosticSelectionKind.ParentOfAmbiguousPath));
        }

        [Test]
        public void Localization_ModularAvatarBindingConflict_DescribesTheNonAmbiguousCause()
        {
            FaceMotionDiagnosticLocalizationCatalog.TryGet(
                FaceMotionDiagnosticCodes.ModularAvatarBindingConflict,
                FaceMotionDiagnosticLanguage.Japanese,
                out DiagnosticLocalizedText ja);
            FaceMotionDiagnosticLocalizationCatalog.TryGet(
                FaceMotionDiagnosticCodes.ModularAvatarBindingConflict,
                FaceMotionDiagnosticLanguage.English,
                out DiagnosticLocalizedText en);

            Assert.That(ja.Title, Does.Contain("Modular Avatar"));
            Assert.That(ja.Summary, Does.Contain("Merge Animator"));
            Assert.That(ja.Resolution, Does.Contain("統合を計画して検証"));
            Assert.That(en.Title, Is.Not.Empty);
            Assert.That(en.Summary, Does.Contain("Merge Animator"));
            Assert.That(en.Resolution, Is.Not.Empty);
        }

        [Test]
        public void Presentation_UnknownCode_PreservesRawFields_GenericFallback()
        {
            const string code = "FM-UNKNOWN-9999";
            var diagnostic = new FaceMotionDiagnostic(
                code,
                FaceMotionDiagnosticSeverity.Warning,
                "The raw generated message.",
                "SomeContext/Id",
                blocking: false,
                "Raw suggested fix.");

            var ja = new FaceMotionDiagnosticPresentation(
                diagnostic,
                FaceMotionDiagnosticLanguage.Japanese);
            var en = new FaceMotionDiagnosticPresentation(
                diagnostic,
                FaceMotionDiagnosticLanguage.English);

            Assert.That(ja.IsKnown, Is.False);
            Assert.That(ja.Category, Is.EqualTo("general"));
            Assert.That(ja.CanAutoFix, Is.False);
            Assert.That(ja.SelectionKind, Is.EqualTo(FaceMotionDiagnosticSelectionKind.None));
            Assert.That(ja.HelpTopicId, Is.EqualTo(code));
            Assert.That(ja.Code, Is.EqualTo(code));
            Assert.That(ja.ContextId, Is.EqualTo("SomeContext/Id"));
            Assert.That(ja.Summary, Is.EqualTo("The raw generated message."));
            Assert.That(ja.Resolution, Is.EqualTo("Raw suggested fix."));
            Assert.That(ja.Detail, Is.EqualTo("The raw generated message."));
            Assert.That(ja.Title, Is.EqualTo("FaceMotionから診断が報告されました"));

            Assert.That(en.Title, Is.EqualTo("FaceMotion reported a diagnostic"));
            Assert.That(en.Summary, Is.EqualTo("The raw generated message."));
            Assert.That(en.Resolution, Is.EqualTo("Raw suggested fix."));
            Assert.That(en.Detail, Is.EqualTo("The raw generated message."));
        }

        [Test]
        public void Presentation_EmptyLocalizedField_FallsBackToGenericData()
        {
            FaceMotionDiagnosticLocalizationCatalog.TryGet(
                FaceMotionDiagnosticCodes.NullProject,
                FaceMotionDiagnosticLanguage.Japanese,
                out DiagnosticLocalizedText jaText);
            Assert.That(jaText, Is.Not.Null);
            bool cautionEmptyInBothLanguages = string.IsNullOrEmpty(jaText.Caution);

            FaceMotionDiagnosticLocalizationCatalog.TryGet(
                FaceMotionDiagnosticCodes.NullProject,
                FaceMotionDiagnosticLanguage.English,
                out DiagnosticLocalizedText enText);
            Assert.That(enText, Is.Not.Null);
            Assert.That(string.IsNullOrEmpty(enText.Caution), Is.EqualTo(cautionEmptyInBothLanguages),
                "A field emptied in one language must be empty in the other for the fallback chain to be deterministic.");

            var diagnostic = new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.NullProject,
                FaceMotionDiagnosticSeverity.Warning,
                "Message",
                string.Empty,
                blocking: false,
                "Suggested fix");
            var presentation = new FaceMotionDiagnosticPresentation(
                diagnostic,
                FaceMotionDiagnosticLanguage.Japanese);

            if (cautionEmptyInBothLanguages)
            {
                Assert.That(presentation.Caution, Is.Empty);
            }
            else
            {
                Assert.That(presentation.Caution, Is.EqualTo(enText.Caution));
            }

            Assert.That(presentation.Title, Is.EqualTo(jaText.Title));
            Assert.That(presentation.Summary, Is.EqualTo(jaText.Summary));
            Assert.That(presentation.Resolution, Is.EqualTo(jaText.Resolution));
        }

        [Test]
        public void KnownCodes_CommandAndMapping_VariantValuesAreStable()
        {
            Assert.That(FaceMotionDiagnosticCodes.CommandInvalidProject, Is.EqualTo("FM-CMD-0001"));
            Assert.That(FaceMotionDiagnosticCodes.InvalidBinding, Is.EqualTo("FM-MAP-0016"));

            foreach (string code in new[] { FaceMotionDiagnosticCodes.CommandInvalidProject, FaceMotionDiagnosticCodes.InvalidBinding })
            {
                Assert.That(FaceMotionDiagnosticDefinitionRegistry.Get(code), Is.Not.Null, code);
                Assert.That(FaceMotionDiagnosticLocalizationCatalog.TryGet(code, FaceMotionDiagnosticLanguage.Japanese, out _), Is.True, code);
                Assert.That(FaceMotionDiagnosticLocalizationCatalog.TryGet(code, FaceMotionDiagnosticLanguage.English, out _), Is.True, code);
            }
        }

        [Test]
        public void FmAvt0007_Contract_Holds()
        {
            DiagnosticDefinition definition = FaceMotionDiagnosticDefinitionRegistry.Get(FaceMotionDiagnosticCodes.DuplicateTransformPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(FaceMotionDiagnosticCodes.DuplicateTransformPath, Is.EqualTo("FM-AVT-0007"));
            Assert.That(definition.NonBlockingActionLevel, Is.EqualTo(FaceMotionDiagnosticActionLevel.Recommended));
            Assert.That(definition.SelectionKind, Is.EqualTo(FaceMotionDiagnosticSelectionKind.ParentOfAmbiguousPath));
            Assert.That(definition.CanAutoFix, Is.False,
                "FM-AVT-0007 must never auto-rename or auto-fix.");

            var blocking = new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.DuplicateTransformPath,
                FaceMotionDiagnosticSeverity.Error,
                "Message",
                "Body/Face",
                blocking: true,
                "Suggested fix");
            var presentation = new FaceMotionDiagnosticPresentation(
                blocking,
                FaceMotionDiagnosticLanguage.Japanese);

            Assert.That(presentation.ActionLevel, Is.EqualTo(FaceMotionDiagnosticActionLevel.Required),
                "Blocking FM-AVT-0007 must resolve to Required.");
            Assert.That(presentation.ActionLevelText, Is.EqualTo("解決が必要"));
            Assert.That(presentation.SelectionKind, Is.EqualTo(FaceMotionDiagnosticSelectionKind.ParentOfAmbiguousPath));
            Assert.That(presentation.CanAutoFix, Is.False);

            FaceMotionDiagnosticLocalizationCatalog.TryGet(
                FaceMotionDiagnosticCodes.DuplicateTransformPath,
                FaceMotionDiagnosticLanguage.Japanese,
                out DiagnosticLocalizedText ja);
            Assert.That(ja.Title, Is.EqualTo("同じ名前のオブジェクトが見つかりました"));
            Assert.That(presentation.Title, Is.EqualTo(ja.Title));
            Assert.That(presentation.Cause, Is.Not.Empty);
            Assert.That(presentation.Impact, Is.Not.Empty);
            Assert.That(presentation.Resolution, Is.Not.Empty);
            Assert.That(presentation.ContextId, Is.EqualTo("Body/Face"));
            Assert.That(presentation.Detail, Is.EqualTo("Message"),
                "FM-AVT-0007 must preserve its raw generator message for technical details.");
        }

        [Test]
        public void Presentation_BuildingEveryCodeInBothLanguages_IsStableAndSafe()
        {
            string[] codes = FaceMotionDiagnosticDefinitionRegistry.All.Keys.ToArray();
            foreach (string code in codes)
            {
                var diagnostic = new FaceMotionDiagnostic(
                    code,
                    FaceMotionDiagnosticSeverity.Error,
                    "Message",
                    string.Empty,
                    blocking: true,
                    "Suggested fix");

                var ja = new FaceMotionDiagnosticPresentation(diagnostic, FaceMotionDiagnosticLanguage.Japanese);
                var en = new FaceMotionDiagnosticPresentation(diagnostic, FaceMotionDiagnosticLanguage.English);

                Assert.That(ja.IsKnown, Is.True);
                Assert.That(en.IsKnown, Is.True);
                Assert.That(ja.Title, Is.Not.Null);
                Assert.That(en.Title, Is.Not.Null);
                Assert.That(ja.Summary, Is.Not.Null);
                Assert.That(en.Summary, Is.Not.Null);
                Assert.That(ja.Resolution, Is.Not.Null);
                Assert.That(en.Resolution, Is.Not.Null);
                Assert.That(ja.Detail, Is.EqualTo("Message"));
                Assert.That(ja.ActionLevel, Is.EqualTo(FaceMotionDiagnosticActionLevel.Required));
                Assert.That(ja.ActionLevelText,
                    Is.EqualTo(FaceMotionUiText.Get("actionRequired", SystemLanguage.Japanese)));
                Assert.That(en.ActionLevel, Is.EqualTo(FaceMotionDiagnosticActionLevel.Required));
                Assert.That(en.ActionLevelText,
                    Is.EqualTo(FaceMotionUiText.Get("actionRequired", SystemLanguage.English)));
                Assert.That(ja.SeverityText,
                    Is.EqualTo(FaceMotionUiText.Get("severityError", SystemLanguage.Japanese)));
            }
        }

        [Test]
        public void UiText_DiagnosticLabels_ResolveInBothLanguages()
        {
            string[] keys =
            {
                "actionRequired", "actionRecommended", "actionInfo",
                "severityError", "severityWarning", "severityInfo",
                "select", "copyCode", "copied",
                "cause", "impact", "resolution", "caution", "context",
                "category", "details", "severity", "actionLevel", "technicalDetails",
                "rawDiagnosticMessage", "contextId", "diagnosticCode"
            };

            foreach (string key in keys)
            {
                Assert.That(FaceMotionUiText.Get(key, SystemLanguage.Japanese), Is.Not.Empty, key);
                Assert.That(FaceMotionUiText.Get(key, SystemLanguage.English), Is.Not.Empty, key);
            }

            Assert.That(FaceMotionUiText.Get("actionRequired", SystemLanguage.Japanese), Is.EqualTo("解決が必要"));
            Assert.That(FaceMotionUiText.Get("actionRequired", SystemLanguage.English), Is.EqualTo("Required"));
            Assert.That(FaceMotionUiText.Get("technicalDetails", SystemLanguage.Japanese), Is.EqualTo("技術詳細"));
            Assert.That(FaceMotionUiText.Get("technicalDetails", SystemLanguage.English), Is.EqualTo("Technical details"));
        }
    }
}
