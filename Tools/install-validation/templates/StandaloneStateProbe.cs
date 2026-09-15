// I7 install-validation state probe. Copied into a temporary project's Assets/Editor/ so it
// can run whether or not the FaceMotion package is installed. Uses only UnityEngine /
// UnityEditor / System APIs; never references FaceMotion types directly.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FaceMotion.I7Validation
{
    public static class StandaloneStateProbe
    {
        [Serializable]
        public sealed class Report
        {
            public string stage;
            public string unityVersion;
            public List<CheckItem> checks = new List<CheckItem>();
            public List<AssetState> assets = new List<AssetState>();
            public List<SceneState> scenes = new List<SceneState>();
            public List<string> notes = new List<string>();
        }

        [Serializable]
        public sealed class CheckItem
        {
            public string name;
            public bool present;
        }

        [Serializable]
        public sealed class AssetState
        {
            public string path;
            public bool exists;
            public int loadedObjects;
            public string foundScriptGuid;
            public bool scriptResolves;
            public string resolvedScriptPath;
            public bool yamlNonTrivial;
        }

        [Serializable]
        public sealed class SceneState
        {
            public string path;
            public int rootObjects;
            public int missingComponents;
            public int faceMotionRuntimeComponents;
            public int modularAvatarComponents;
        }

        private static Report _report = new Report();
        private static string _reportPath;

        public static void Run()
        {
            _report = new Report();
            _report.unityVersion = Application.unityVersion;
            try
            {
                RunChecks();
            }
            catch (Exception exception)
            {
                _report.notes.Add("PROBE_ERROR: " + exception);
            }

            WriteReport();
        }

        private static void RunChecks()
        {
            _report.checks.Add(new CheckItem { name = "Assembly.FaceMotion.Editor.Core", present = Type.GetType("FaceMotion.Versioning.FaceMotionVersions, FaceMotion.Editor.Core") != null });
            _report.checks.Add(new CheckItem { name = "FaceMotionWindow", present = Type.GetType("FaceMotion.Editor.UI.Window.FaceMotionWindow, FaceMotion.Editor.UI") != null });
            _report.checks.Add(new CheckItem { name = "DirectVRChatIntegration", present = Type.GetType("FaceMotion.Editor.VRChat.Integration.DirectVRChatIntegration, FaceMotion.Editor.VRChat") != null });
            _report.checks.Add(new CheckItem { name = "ModularAvatarBackend", present = Type.GetType("FaceMotion.Editor.ModularAvatar.ModularAvatarIntegrationBackend, FaceMotion.Editor.ModularAvatar") != null });
            _report.checks.Add(new CheckItem { name = "ModularAvatarManifestAssetType", present = Type.GetType("FaceMotion.Editor.ModularAvatar.ModularAvatarIntegrationManifest, FaceMotion.Editor.ModularAvatar") != null });
            _report.checks.Add(new CheckItem { name = "ModularAvatarCoreComponents", present = Type.GetType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator, nadena.dev.modular-avatar.core") != null });
            _report.checks.Add(new CheckItem { name = "Directory.Packages.com.facemotion.editor", present = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Packages", "com.facemotion.editor")) });
            _report.checks.Add(new CheckItem { name = "Directory.Packages.nadena.dev.modular-avatar", present = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Packages", "nadena.dev.modular-avatar")) });
            _report.checks.Add(new CheckItem { name = "Directory.Packages.nadena.dev.ndmf", present = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Packages", "nadena.dev.ndmf")) });

            ScanAssets();
            ScanScenes();
        }

        private static void ScanAssets()
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "I7Data");
            if (!Directory.Exists(root))
            {
                _report.notes.Add("Assets/I7Data does not exist; nothing to scan.");
                return;
            }

            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                if (file.EndsWith(".meta", StringComparison.Ordinal) || file.EndsWith(".unity", StringComparison.Ordinal))
                {
                    continue;
                }

                string relative = file.Replace(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, string.Empty).Replace('\\', '/');
                if (!relative.StartsWith("Assets/", StringComparison.Ordinal) || relative.Contains("/."))
                {
                    continue;
                }

                var state = new AssetState { path = relative, exists = true };
                string yaml = AppendIfReadable(file);
                state.yamlNonTrivial = !string.IsNullOrEmpty(yaml) && yaml.Length > 32;
                string scriptGuid = ExtractScriptGuid(yaml);
                state.foundScriptGuid = scriptGuid;
                if (string.IsNullOrEmpty(scriptGuid))
                {
                    state.scriptResolves = true;
                }
                else
                {
                    string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                    state.scriptResolves = !string.IsNullOrEmpty(scriptPath) && File.Exists(Path.Combine(Directory.GetCurrentDirectory(), scriptPath.Replace('/', Path.DirectorySeparatorChar)));
                    state.resolvedScriptPath = scriptPath;
                }

                UnityEngine.Object[] loaded = AssetDatabase.LoadAllAssetsAtPath(relative);
                state.loadedObjects = loaded == null ? 0 : loaded.Length;
                _report.assets.Add(state);
            }
        }

        private static void ScanScenes()
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "I7Data");
            string[] scenes = Directory.Exists(root) ? Directory.GetFiles(root, "*.unity", SearchOption.AllDirectories) : new string[0];
            Array.Sort(scenes, StringComparer.Ordinal);
            foreach (string scenePath in scenes)
            {
                string relative = scenePath.Replace(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, string.Empty).Replace('\\', '/');
                Scene scene = EditorSceneManager.OpenScene(relative, OpenSceneMode.Single);
                var sceneState = new SceneState { path = relative, rootObjects = scene.rootCount };
                if (scene.IsValid())
                {
                    GameObject[] roots = scene.GetRootGameObjects();
                    foreach (GameObject rootObject in roots)
                    {
                        Component[] all = rootObject.GetComponentsInChildren<Component>(true);
                        foreach (Component component in all)
                        {
                            if (component == null)
                            {
                                sceneState.missingComponents++;
                                continue;
                            }

                            string typeName = component.GetType().FullName ?? string.Empty;
                            if (typeName.StartsWith("FaceMotion", StringComparison.Ordinal))
                            {
                                sceneState.faceMotionRuntimeComponents++;
                            }

                            if (typeName.StartsWith("nadena.dev.modular_avatar", StringComparison.Ordinal))
                            {
                                sceneState.modularAvatarComponents++;
                            }
                        }
                    }
                }

                _report.scenes.Add(sceneState);
            }
        }

        private static string AppendIfReadable(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractScriptGuid(string yaml)
        {
            if (string.IsNullOrEmpty(yaml))
            {
                return null;
            }

            const string marker = "m_Script: {fileID: 11500000, guid: ";
            int index = yaml.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0 || index + marker.Length + 32 > yaml.Length)
            {
                return null;
            }

            return yaml.Substring(index + marker.Length, 32);
        }

        private static void WriteReport()
        {
            _reportPath = GetArgument("-i7Report");
            string json = JsonUtility.ToJson(_report, true);
            if (string.IsNullOrEmpty(_reportPath))
            {
                Debug.Log("[I7Standalone]\n" + json);
                return;
            }

            string directory = Path.GetDirectoryName(_reportPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_reportPath, json);
            Debug.Log("I7 standalone state report written: " + _reportPath);
        }

        private static string GetArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.Ordinal))
                {
                    return arguments[i + 1];
                }
            }

            return null;
        }
    }
}
