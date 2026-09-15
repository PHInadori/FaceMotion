namespace FaceMotion.Avatar
{
    /// <summary>
    /// Immutable snapshot entry for one Transform of an avatar. RelativePath uses the
    /// unified "/"-separated form with "" for the avatar root. Name is the transform's
    /// GameObject name (or empty for the root). Depth is the number of ancestors up to the
    /// root. No Unity object reference is stored here; runtime object references live in
    /// the Unity-side object cache.
    /// </summary>
    public sealed class TransformIndexEntry
    {
        public TransformIndexEntry(string relativePath, string name, int depth, string parentPath)
        {
            RelativePath = relativePath ?? string.Empty;
            Name = name ?? string.Empty;
            Depth = depth;
            ParentPath = parentPath ?? string.Empty;
        }

        public string RelativePath { get; }

        public string Name { get; }

        public int Depth { get; }

        public string ParentPath { get; }
    }

    /// <summary>
    /// Immutable snapshot entry for one SkinnedMeshRenderer. HasMesh reports whether a
    /// shared mesh was present at scan time; BlendShapeCount is 0 when the mesh is missing
    /// or has no blend shapes. Mesh name is diagnostic-only and is not persistent identity.
    /// </summary>
    public sealed class RendererIndexEntry
    {
        public RendererIndexEntry(
            string relativePath,
            string rendererName,
            bool hasMesh,
            int blendShapeCount,
            string meshName)
        {
            RelativePath = relativePath ?? string.Empty;
            RendererName = rendererName ?? string.Empty;
            HasMesh = hasMesh;
            BlendShapeCount = blendShapeCount;
            MeshName = meshName ?? string.Empty;
        }

        public string RelativePath { get; }

        public string RendererName { get; }

        public bool HasMesh { get; }

        public int BlendShapeCount { get; }

        public string MeshName { get; }
    }

    /// <summary>
    /// Immutable snapshot entry for one blend shape of one renderer. CurrentIndex and
    /// CurrentWeight are runtime snapshot information captured at scan time; they are never
    /// persistent identity. The persistent identity is the pair
    /// (RendererPath, BlendShapeName).
    /// </summary>
    public sealed class BlendShapeIndexEntry
    {
        public BlendShapeIndexEntry(
            string rendererPath,
            string rendererName,
            string blendShapeName,
            int currentIndex,
            float currentWeight)
        {
            RendererPath = rendererPath ?? string.Empty;
            RendererName = rendererName ?? string.Empty;
            BlendShapeName = blendShapeName ?? string.Empty;
            CurrentIndex = currentIndex;
            CurrentWeight = currentWeight;
        }

        public string RendererPath { get; }

        public string RendererName { get; }

        public string BlendShapeName { get; }

        public int CurrentIndex { get; }

        public float CurrentWeight { get; }
    }
}