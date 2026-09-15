using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;

namespace FaceMotion.Avatar
{
    public enum MappingResolutionStatus
    {
        Resolved = 0,
        Missing = 1,
        MissingRenderer = 2,
        MissingBlendShape = 3,
        MissingTransform = 4,
        Ambiguous = 5,
        Invalid = 6
    }

    /// <summary>Outcome of resolving one mapping entry against one avatar index.</summary>
    public sealed class EntryResolution
    {
        public EntryResolution(
            BindingMappingEntry entry,
            MappingResolutionStatus status,
            FaceMotionDiagnostic diagnostic)
        {
            Entry = entry;
            Status = status;
            Diagnostic = diagnostic;
        }

        public BindingMappingEntry Entry { get; }

        public MappingResolutionStatus Status { get; }

        /// <summary>Null when the entry resolved uniquely with no advisory.</summary>
        public FaceMotionDiagnostic Diagnostic { get; }
    }

    /// <summary>
    /// Exact-only binding resolution. The resolver never guesses: a binding either matches
    /// exactly, is ambiguous (duplicate path/identity in the index), or is missing. Fuzzy or
    /// partial remapping belongs to the later Preset phase, not here.
    /// </summary>
    public static class AvatarMappingResolver
    {
        public static EntryResolution Resolve(BindingMappingEntry entry, AvatarIndex index)
        {
            if (entry == null)
            {
                return new EntryResolution(null, MappingResolutionStatus.Invalid, Blocking(
                    FaceMotionDiagnosticCodes.InvalidBinding,
                    "A null mapping entry cannot be resolved.",
                    string.Empty,
                    "Remove the null entry."));
            }

            if (index == null)
            {
                return new EntryResolution(entry, MappingResolutionStatus.Invalid, Blocking(
                    FaceMotionDiagnosticCodes.InvalidBinding,
                    "No avatar index is available for resolution.",
                    entry.EntryId,
                    "Scan the avatar first."));
            }

            switch (entry.TargetKind)
            {
                case LogicalTargetKind.BlendShape:
                    return ResolveBlendShape(entry, index);
                case LogicalTargetKind.Transform:
                    return ResolveTransform(entry, index);
                default:
                    return new EntryResolution(entry, MappingResolutionStatus.Invalid, Blocking(
                        FaceMotionDiagnosticCodes.InvalidLogicalTargetKind,
                        $"Logical target kind {(int)entry.TargetKind} is not supported.",
                        entry.EntryId,
                        "Use a supported LogicalTargetKind."));
            }
        }

        private static EntryResolution ResolveBlendShape(BindingMappingEntry entry, AvatarIndex index)
        {
            if (!entry.BlendShape.HasValue)
            {
                return new EntryResolution(entry, MappingResolutionStatus.Invalid, Blocking(
                    FaceMotionDiagnosticCodes.MissingBinding,
                    "A blend shape mapping entry has no populated binding.",
                    entry.EntryId,
                    "Bind the logical target to a concrete renderer and blend shape."));
            }

            BlendShapeBinding identity = entry.BlendShape.Value;
            if (!index.HasRenderer(identity.RendererPath))
            {
                return new EntryResolution(entry, MappingResolutionStatus.MissingRenderer, Warning(
                    FaceMotionDiagnosticCodes.MissingRenderer,
                    $"Renderer \"{identity.RendererPath}\" is absent from the avatar.",
                    entry.LogicalTargetId,
                    "Point the binding at an existing renderer, or restore the renderer."));
            }

            BlendShapeResolution resolution = index.ResolveBlendShape(identity);
            switch (resolution.Status)
            {
                case NameResolutionStatus.Unique:
                    return new EntryResolution(entry, MappingResolutionStatus.Resolved, null);
                case NameResolutionStatus.Ambiguous:
                    return new EntryResolution(entry, MappingResolutionStatus.Ambiguous, Warning(
                        FaceMotionDiagnosticCodes.AmbiguousBinding,
                        $"Blend shape \"{identity.BlendShapeName}\" on \"{identity.RendererPath}\" is ambiguous ({resolution.Count} matches).",
                        entry.LogicalTargetId,
                        "Rename the duplicate blend shape, or rebind."));
                default:
                    return new EntryResolution(entry, MappingResolutionStatus.MissingBlendShape, Warning(
                        FaceMotionDiagnosticCodes.MissingBlendShape,
                        $"Blend shape \"{identity.BlendShapeName}\" is absent from renderer \"{identity.RendererPath}\".",
                        entry.LogicalTargetId,
                        "Point the binding at an existing blend shape."));
            }
        }

        private static EntryResolution ResolveTransform(BindingMappingEntry entry, AvatarIndex index)
        {
            if (!entry.Transform.HasValue)
            {
                return new EntryResolution(entry, MappingResolutionStatus.Invalid, Blocking(
                    FaceMotionDiagnosticCodes.MissingBinding,
                    "A transform mapping entry has no populated binding.",
                    entry.EntryId,
                    "Bind the logical target to a concrete transform path."));
            }

            TransformBinding identity = entry.Transform.Value;
            TransformResolution resolution = index.ResolveTransform(identity.TransformPath);
            switch (resolution.Status)
            {
                case NameResolutionStatus.Unique:
                    return new EntryResolution(entry, MappingResolutionStatus.Resolved, null);
                case NameResolutionStatus.Ambiguous:
                    return new EntryResolution(entry, MappingResolutionStatus.Ambiguous, Warning(
                        FaceMotionDiagnosticCodes.AmbiguousBinding,
                        $"Transform path \"{identity.TransformPath}\" is ambiguous ({resolution.Count} matches).",
                        entry.LogicalTargetId,
                        "Rename the sibling transform, or rebind."));
                default:
                    return new EntryResolution(entry, MappingResolutionStatus.MissingTransform, Warning(
                        FaceMotionDiagnosticCodes.MissingTransform,
                        $"Transform \"{identity.TransformPath}\" is absent from the avatar.",
                        entry.LogicalTargetId,
                        "Point the binding at an existing transform."));
            }
        }

        private static FaceMotionDiagnostic Blocking(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, contextId, true, fix);
        }

        private static FaceMotionDiagnostic Warning(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Warning, message, contextId, false, fix);
        }
    }
}