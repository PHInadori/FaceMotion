using System;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FaceMotion.Editor.VRChat.Integration
{
    /// <summary>Ownership record for a Direct multi-animation output. This is deliberately separate from the J4 manifest schema.</summary>
    public sealed class DirectBatchIntegrationManifest : ScriptableObject
    {
        [Serializable]
        public sealed class Item
        {
            public string ClipPath;
            public string DisplayName;
            public string ParameterName;
            public string LayerName;
        }

        public VRCAvatarDescriptor Avatar;
        public string AvatarGlobalId;
        public RuntimeAnimatorController OriginalFx;
        public VRCExpressionParameters OriginalParameters;
        public VRCExpressionsMenu OriginalMenu;
        public AnimatorController GeneratedFx;
        public VRCExpressionParameters GeneratedParameters;
        public VRCExpressionsMenu GeneratedMenu;
        public VRCExpressionsMenu GeneratedSubMenu;
        public Item[] Items;
        public string[] OwnedAssetPaths;
    }
}
