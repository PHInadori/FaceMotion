using System;
using System.Collections.Generic;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.VRChat.Integration;

namespace FaceMotion.Editor.UI.Panels
{
    public enum DesiredStateResultKind { Succeeded, NoChange, RemovalComplete, FailedRolledBack, FailedRollbackFailed, PreflightFailed, Failed }

    /// <summary>Immutable presentation model for the single beginner-facing update result.</summary>
    public sealed class DesiredStateResultPresentation
    {
        public DesiredStateResultPresentation(DesiredStateResultKind kind, string titleKey, int appliedCount, bool hasBindingConflict, string conflictObjectName, IReadOnlyList<string> technicalDetails)
        {
            Kind = kind;
            TitleKey = titleKey ?? string.Empty;
            AppliedCount = appliedCount;
            HasBindingConflict = hasBindingConflict;
            ConflictObjectName = conflictObjectName ?? string.Empty;
            TechnicalDetails = technicalDetails ?? Array.Empty<string>();
        }

        public DesiredStateResultKind Kind { get; }
        public string TitleKey { get; }
        public int AppliedCount { get; }
        public bool HasBindingConflict { get; }
        public string ConflictObjectName { get; }
        public IReadOnlyList<string> TechnicalDetails { get; }
    }

    /// <summary>
    /// Maps a VrchatDesiredStateReconciliationResult to beginner-readable UI messages. The
    /// mapping is pure and reads only the structured result fields, so the UI never parses
    /// diagnostic strings to claim success. It never claims success or restoration for
    /// FailedRollbackFailed.
    /// </summary>
    public static class DesiredStateResultPresenter
    {
        private const string MaObjectPrefix = "FaceMotion MA ";

        public const string TitleSucceeded = "resultSucceededTitle";
        public const string CountSucceeded = "resultSucceededCount";
        public const string TitleNoChange = "resultNoChange";
        public const string TitleRemovalComplete = "resultRemovalComplete";
        public const string TitleFailedRolledBack = "resultFailedRolledBack";
        public const string TitleFailedRollbackFailed = "resultFailedRollbackFailed";
        public const string TitlePreflightFailed = "resultPreflightFailed";
        public const string TitleFailed = "resultGenericFailed";
        public const string ConflictBeginnerGeneric = "conflictBindingBeginner";
        public const string ConflictBeginnerNamed = "conflictBindingOther";

        public static DesiredStateResultPresentation Build(VrchatDesiredStateReconciliationResult result)
        {
            int adds = 0;
            int removes = 0;
            int keeps = 0;
            bool hasBindingConflict = false;
            string conflictObjectName = string.Empty;
            IReadOnlyList<FaceMotionDiagnostic> diagnostics = result == null ? null : result.Diagnostics;
            if (diagnostics != null)
            {
                for (var i = 0; i < diagnostics.Count; i++)
                {
                    var diagnostic = diagnostics[i];
                    if (diagnostic == null || !IsBindingConflict(diagnostic.Code))
                    {
                        continue;
                    }

                    hasBindingConflict = true;
                    if (string.IsNullOrEmpty(conflictObjectName))
                    {
                        conflictObjectName = ResolveConflictObjectName(diagnostic);
                    }
                }
            }

            if (result != null && result.Items != null)
            {
                for (var i = 0; i < result.Items.Count; i++)
                {
                    var item = result.Items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    switch (item.Action)
                    {
                        case VrchatDesiredStateAction.Add: adds++; break;
                        case VrchatDesiredStateAction.Remove: removes++; break;
                        default: keeps++; break;
                    }
                }
            }

            VrchatDesiredStateReconciliationOutcome outcome = result == null
                ? VrchatDesiredStateReconciliationOutcome.Failed
                : result.Outcome;
            DesiredStateResultKind kind;
            string titleKey;
            switch (outcome)
            {
                case VrchatDesiredStateReconciliationOutcome.Succeeded:
                    if (adds == 0 && removes == 0)
                    {
                        kind = DesiredStateResultKind.NoChange;
                        titleKey = TitleNoChange;
                    }
                    else if (adds == 0 && keeps == 0)
                    {
                        kind = DesiredStateResultKind.RemovalComplete;
                        titleKey = TitleRemovalComplete;
                    }
                    else
                    {
                        kind = DesiredStateResultKind.Succeeded;
                        titleKey = TitleSucceeded;
                    }

                    break;
                case VrchatDesiredStateReconciliationOutcome.FailedRolledBack:
                    kind = DesiredStateResultKind.FailedRolledBack;
                    titleKey = TitleFailedRolledBack;
                    break;
                case VrchatDesiredStateReconciliationOutcome.FailedRollbackFailed:
                    kind = DesiredStateResultKind.FailedRollbackFailed;
                    titleKey = TitleFailedRollbackFailed;
                    break;
                case VrchatDesiredStateReconciliationOutcome.PreflightFailed:
                    kind = DesiredStateResultKind.PreflightFailed;
                    titleKey = TitlePreflightFailed;
                    break;
                default:
                    kind = DesiredStateResultKind.Failed;
                    titleKey = TitleFailed;
                    break;
            }

            var technical = new List<string>();
            if (diagnostics != null)
            {
                for (var i = 0; i < diagnostics.Count; i++)
                {
                    var diagnostic = diagnostics[i];
                    if (diagnostic != null && diagnostic.Blocking)
                    {
                        technical.Add(DirectVRChatIntegrationPanel.FormatDiagnostic(diagnostic));
                    }
                }
            }

            return new DesiredStateResultPresentation(kind, titleKey, adds, hasBindingConflict, conflictObjectName, technical);
        }

        public static string ConflictBeginnerKey(DesiredStateResultPresentation presentation)
        {
            return string.IsNullOrEmpty(presentation == null ? string.Empty : presentation.ConflictObjectName)
                ? ConflictBeginnerGeneric
                : ConflictBeginnerNamed;
        }

        private static bool IsBindingConflict(string code)
        {
            return code == FaceMotionDiagnosticCodes.ModularAvatarBindingConflict
                || code == FaceMotionDiagnosticCodes.ModularAvatarCrossBindingConflict;
        }

        private static string ResolveConflictObjectName(FaceMotionDiagnostic diagnostic)
        {
            if (diagnostic.Details == null
                || !diagnostic.Details.TryGetValue(FaceMotionDiagnosticDetailKeys.Reason, out var reason)
                || reason != FaceMotionDiagnosticDetailKeys.ReasonMergeAnimatorBinding
                || !diagnostic.Details.TryGetValue(FaceMotionDiagnosticDetailKeys.ConflictObjectName, out var mergeName)
                || string.IsNullOrEmpty(mergeName))
            {
                return string.Empty;
            }

            return mergeName.StartsWith(MaObjectPrefix, StringComparison.Ordinal)
                ? mergeName.Substring(MaObjectPrefix.Length)
                : mergeName;
        }
    }
}