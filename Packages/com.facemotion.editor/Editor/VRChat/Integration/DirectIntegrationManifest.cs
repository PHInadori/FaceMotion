using FaceMotion.Serialization;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FaceMotion.Editor.VRChat.Integration
{
    /// <summary>
    /// Persistent ownership and rollback data. Only assets listed here are deleted by rollback.
    /// SchemaVersion/BackendId/IntegrationId/State are the Phase I.2 compatibility fields;
    /// the integration ID is generated once and preserved across reapplies.
    /// </summary>
    public sealed class DirectIntegrationManifest : ScriptableObject
    {
        public VRCAvatarDescriptor Avatar;
        public string AvatarGlobalId;
        public RuntimeAnimatorController OriginalFx;
        public VRCExpressionParameters OriginalParameters;
        public VRCExpressionsMenu OriginalMenu;
        public AnimatorController GeneratedFx;
        public VRCExpressionParameters GeneratedParameters;
        public VRCExpressionsMenu GeneratedMenu;
        public VRCExpressionsMenu GeneratedSubMenu;
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
