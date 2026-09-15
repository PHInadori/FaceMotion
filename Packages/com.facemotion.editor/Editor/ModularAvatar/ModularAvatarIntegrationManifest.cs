using FaceMotion.Serialization;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.ModularAvatar
{
    /// <summary>
    /// Ownership marker for components and assets created by the MA backend.
    /// SchemaVersion/BackendId/IntegrationId/State are the Phase I.2 compatibility fields;
    /// the integration ID is generated once and preserved across reapplies.
    /// </summary>
    public sealed class ModularAvatarIntegrationManifest : ScriptableObject
    {
        public VRCAvatarDescriptor Avatar;
        public string AvatarGlobalId;
        public string IntegrationObjectName;
        public string IntegrationObjectGlobalId;
        public string ParameterName;
        public string[] OwnedAssetPaths;

        public int SchemaVersion;
        public string IntegrationId;
        public string BackendId;
        public string AvatarFingerprint;
        public string AnimationId;
        public IntegrationState State;
    }
}