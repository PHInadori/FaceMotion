using System;
using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Diagnostics;
using UnityEngine;

namespace FaceMotion.Editor.Avatar
{
    /// <summary>Read-only result of one avatar scan.</summary>
    public sealed class AvatarScanReport
    {
        public AvatarScanReport(
            AvatarIndex index,
            IReadOnlyList<FaceMotionDiagnostic> diagnostics,
            bool hasBlocking)
        {
            Index = index;
            Diagnostics = diagnostics ?? new List<FaceMotionDiagnostic>();
            HasBlocking = hasBlocking;
        }

        /// <summary>Null only when the scan itself was unusable (fingerprint failure).</summary>
        public AvatarIndex Index { get; }

        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }

        public bool HasBlocking { get; }
    }

    /// <summary>
    /// Scans a Unity hierarchy into an SDK-neutral AvatarIndex. Inactive descendants are
    /// included; sharedMesh-less and blend-shape-less renderers are safe. Reusable: an index
    /// is built once per scan, never rescanned per frame.
    /// </summary>
    public static class UnityAvatarScanner
    {
        public static AvatarScanReport Scan(GameObject root)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (root == null)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.AvatarRootMissing,
                    "The avatar root GameObject is null.",
                    string.Empty,
                    "Provide the avatar root."));
                return new AvatarScanReport(null, diagnostics, true);
            }

            Transform rootTransform = root.transform;

            var transforms = new List<TransformIndexEntry>();
            var seenTransformPaths = new HashSet<string>(StringComparer.Ordinal);
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform element = allTransforms[i];
                string path = RelativePathUtility.GetRelativePath(rootTransform, element);
                if (path == null)
                {
                    continue;
                }

                if (!seenTransformPaths.Add(path))
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.DuplicateTransformPath,
                        $"Transform relative path \"{path}\" appears more than once; binding identity is ambiguous.",
                        path,
                        "Rename one of the duplicate sibling transforms."));
                }

                bool isRoot = ReferenceEquals(rootTransform, element);
                transforms.Add(new TransformIndexEntry(
                    path,
                    isRoot ? string.Empty : element.name,
                    RelativePathUtility.GetDepth(rootTransform, element),
                    isRoot ? string.Empty : RelativePathUtility.GetParentPath(path)));
            }

            var renderers = new List<RendererIndexEntry>();
            var blendShapes = new List<BlendShapeIndexEntry>();
            var allRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = allRenderers[i];
                string rendererPath = RelativePathUtility.GetRelativePath(rootTransform, renderer.transform);
                if (rendererPath == null)
                {
                    continue;
                }

                Mesh mesh = renderer.sharedMesh;
                bool hasMesh = mesh != null;
                int blendShapeCount = hasMesh ? mesh.blendShapeCount : 0;
                renderers.Add(new RendererIndexEntry(
                    rendererPath,
                    renderer.gameObject.name,
                    hasMesh,
                    blendShapeCount,
                    hasMesh ? mesh.name : string.Empty));

                if (!hasMesh)
                {
                    diagnostics.Add(Warning(
                        FaceMotionDiagnosticCodes.RendererSharedMeshMissing,
                        $"Renderer \"{rendererPath}\" has no shared mesh; its blend shapes cannot be bound.",
                        rendererPath,
                        "Assign a shared mesh, or ignore this renderer."));
                    continue;
                }

                var seenNames = new HashSet<string>(StringComparer.Ordinal);
                var deltaVertices = new Vector3[mesh.vertexCount];
                var deltaNormals = new Vector3[mesh.vertexCount];
                var deltaTangents = new Vector3[mesh.vertexCount];
                for (int shapeIndex = 0; shapeIndex < blendShapeCount; shapeIndex++)
                {
                    string shapeName = mesh.GetBlendShapeName(shapeIndex);
                    if (!seenNames.Add(shapeName))
                    {
                        diagnostics.Add(Warning(
                            FaceMotionDiagnosticCodes.DuplicateBlendShapeNameInMesh,
                            $"Blend shape \"{shapeName}\" appears more than once on \"{rendererPath}\"; binding identity is ambiguous.",
                            rendererPath + "/" + shapeName,
                            "Rename one of the duplicate blend shapes."));
                    }

                    blendShapes.Add(new BlendShapeIndexEntry(
                        rendererPath,
                        renderer.gameObject.name,
                        shapeName,
                        shapeIndex,
                        renderer.GetBlendShapeWeight(shapeIndex),
                        HasVisibleBlendShapeDelta(mesh, shapeIndex, deltaVertices, deltaNormals, deltaTangents)));
                }
            }

            AvatarFingerprint fingerprint;
            try
            {
                fingerprint = AvatarFingerprintBuilder.Compute(transforms, renderers, blendShapes);
            }
            catch (Exception exception)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.FingerprintFailure,
                    "Computing the avatar fingerprint failed: " + exception.Message,
                    root.name,
                    "Inspect the hierarchy for unexpected structure."));
                return new AvatarScanReport(null, diagnostics, true);
            }

            var index = AvatarIndex.Create(fingerprint, transforms, renderers, blendShapes);
            bool hasBlocking = false;
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Blocking)
                {
                    hasBlocking = true;
                    break;
                }
            }

            return new AvatarScanReport(index, diagnostics, hasBlocking);
        }

        internal static bool HasVisibleBlendShapeDelta(
            Mesh mesh,
            int shapeIndex,
            Vector3[] deltaVertices,
            Vector3[] deltaNormals,
            Vector3[] deltaTangents)
        {
            if (mesh == null || shapeIndex < 0 || shapeIndex >= mesh.blendShapeCount)
            {
                return true;
            }

            try
            {
                int frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    for (int vertexIndex = 0; vertexIndex < deltaVertices.Length; vertexIndex++)
                    {
                        if (deltaVertices[vertexIndex] != Vector3.zero
                            || deltaNormals[vertexIndex] != Vector3.zero
                            || deltaTangents[vertexIndex] != Vector3.zero)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception)
            {
                // If Unity cannot expose mesh frame data, keep the shape available rather than hide a usable binding.
                return true;
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
