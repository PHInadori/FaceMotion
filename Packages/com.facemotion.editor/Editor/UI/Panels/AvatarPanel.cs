using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.VRChat;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Panels
{
    public sealed class AvatarPanel
    {
        private readonly FaceMotionEditorSession _session;
        private readonly AvatarController _avatar;

        public AvatarPanel(FaceMotionEditorSession session, AvatarController avatar)
        {
            _session = session ?? throw new System.ArgumentNullException(nameof(session));
            _avatar = avatar ?? throw new System.ArgumentNullException(nameof(avatar));
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("avatar"), EditorStyles.boldLabel);
            if (GUILayout.Button(FaceMotionUiText.Get("selectAvatar"), GUILayout.ExpandWidth(true)))
            {
                ShowSceneSelector();
            }

            if (_session.ActiveDescriptor != null)
            {
                EditorGUILayout.LabelField(FaceMotionUiText.Get("active"), _session.ActiveDescriptor.name ?? FaceMotionUiText.Get("unnamed"));
            }
            else
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectDescriptor"), MessageType.Info);
            }

            if (_session.AvatarIndexDirty)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("avatarIndexChanged"), MessageType.Warning);
            }

            EditorGUILayout.LabelField(FaceMotionUiText.Get("bindings"), (_session.TrackBindings?.Count ?? 0).ToString());
        }

        public void ShowSceneSelector()
        {
            var descriptors = Object.FindObjectsOfType<VRCAvatarDescriptor>();
            if (descriptors == null || descriptors.Length == 0)
            {
                EditorUtility.DisplayDialog(FaceMotionUiText.Get("avatar"), FaceMotionUiText.Get("noDescriptor"), FaceMotionUiText.Get("ok"));
                return;
            }

            var menu = new GenericMenu();
            for (int i = 0; i < descriptors.Length; i++)
            {
                var d = descriptors[i];
                string name = !string.IsNullOrEmpty(d.name) ? d.name : FaceMotionUiText.Get("avatar") + " " + i;
                menu.AddItem(new GUIContent(name), false, OnSelectDescriptor, d);
            }

            if (_session.ActiveDescriptor != null)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(FaceMotionUiText.Get("clear")), false, OnClearDescriptor);
            }

            menu.ShowAsContext();
        }

        private void OnSelectDescriptor(object target)
        {
            if (target is VRCAvatarDescriptor descriptor)
            {
                _avatar.SetDescriptor(descriptor);
            }
        }

        private void OnClearDescriptor()
        {
            _session.ClearAvatar();
        }
    }
}
