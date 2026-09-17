using FaceMotion.Integration;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.VRChat.Integration
{
    public static class IntegrationBackendSelectionStore
    {
        private const string BackendSelectionKey = "IntegrationBackend";
        private const string BackendSelectionSetKey = "IntegrationBackendSet";

        public static IntegrationBackendSelection Load()
        {
            bool hasExplicit = EditorPrefs.GetBool(GetKey(BackendSelectionSetKey), false);
            int saved = EditorPrefs.GetInt(GetKey(BackendSelectionKey), (int)IntegrationBackendSelection.Direct);
            var explicitSelection = saved == (int)IntegrationBackendSelection.ModularAvatar
                ? IntegrationBackendSelection.ModularAvatar
                : IntegrationBackendSelection.Direct;
            return ResolveInitial(hasExplicit, explicitSelection, ModularAvatarIntegrationBackendLocator.Create() != null);
        }

        public static void Save(IntegrationBackendSelection backend)
        {
            EditorPrefs.SetInt(GetKey(BackendSelectionKey), (int)backend);
            EditorPrefs.SetBool(GetKey(BackendSelectionSetKey), true);
        }

        public static IntegrationBackendSelection ResolveInitial(
            bool hasExplicitSelection,
            IntegrationBackendSelection explicitSelection,
            bool modularAvatarAvailable)
        {
            if (hasExplicitSelection) return explicitSelection;
            return modularAvatarAvailable
                ? IntegrationBackendSelection.ModularAvatar
                : IntegrationBackendSelection.Direct;
        }

        private static string GetKey(string suffix)
        {
            return "FaceMotion.Window.v2." + Hash128.Compute(Application.dataPath).ToString() + "." + suffix;
        }
    }
}
