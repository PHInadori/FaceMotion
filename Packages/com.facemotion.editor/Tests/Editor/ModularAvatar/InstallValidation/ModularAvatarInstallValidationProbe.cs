using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Serialization;
using FaceMotion.Versioning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>Phase I.7 MA-only lifecycle probe. This assembly is excluded when MA is absent.</summary>
    public static class ModularAvatarInstallValidationProbe
    {
        private const string DataRoot = "Assets/I7Data";
        private const string ScenePath = DataRoot + "/I7Lifecycle.unity";
        private const string MotionPath = DataRoot + "/Motion.anim";
        private const string Folder = DataRoot + "/FaceMotionMA_Demo";
        private const string ManifestPath = Folder + "/Manifest.asset";
        private const string LegacyPath = DataRoot + "/Legacy/MaManifest.asset";

        [Serializable]
        private sealed class Report
        {
            public string stage;
            public List<Assertion> assertions = new List<Assertion>();
            public List<string> notes = new List<string>();
        }

        [Serializable]
        private sealed class Assertion
        {
            public string name;
            public bool passed;
            public string detail;
        }

        private static Report _report;
        private static string _reportPath;

        public static void I7MaSetupFixture()
        {
            Initialize("ma-setup");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var avatar = CreateAvatar();
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MotionPath);
                var backend = ModularAvatarIntegrationBackendLocator.Create();
                Assert("ma.backend.available", backend != null, backend == null ? "not locatable" : "located");
                if (backend != null && clip != null)
                {
                    var result = backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(avatar, clip, DataRoot, "Demo")));
                    Assert("ma.apply", result != null && result.Succeeded && result.Manifest != null, Detail(result));
                }

                WriteForeignFile(Folder + "/note.txt", "foreign-ma");
                if (!AssetDatabase.CopyAsset(ManifestPath, LegacyPath)) throw new InvalidOperationException("Could not create the legacy MA manifest fixture.");
                var legacy = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(LegacyPath);
                legacy.SchemaVersion = FaceMotionVersions.LegacySchemaVersion;
                legacy.BackendId = string.Empty;
                EditorUtility.SetDirty(legacy);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            }
            catch (Exception exception)
            {
                _report.notes.Add("SETUP_ERROR: " + exception);
            }

            WriteReport();
        }

        public static void I7MaVerifyState()
        {
            Initialize("ma-verify");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(ManifestPath);
                Assert("verify.ma.manifestLoads", manifest != null, ManifestPath);
                if (manifest != null)
                {
                    Assert("verify.ma.schema", manifest.SchemaVersion == FaceMotionVersions.IntegrationManifestVersion, "schema=" + manifest.SchemaVersion);
                    Assert("verify.ma.integrationId", StableId.IsValid(manifest.IntegrationId), "id=" + manifest.IntegrationId);
                    Assert("verify.ma.ownedNonEmpty", manifest.OwnedAssetPaths != null && manifest.OwnedAssetPaths.Length > 0, "owned=" + (manifest.OwnedAssetPaths == null ? 0 : manifest.OwnedAssetPaths.Length));
                    foreach (string path in manifest.OwnedAssetPaths ?? Array.Empty<string>())
                    {
                        Assert("verify.ma.owned.exists(" + path + ")", File.Exists(ToAbsolute(path)), path);
                    }

                    if (ModularAvatarIntegrationManifestMigration.TryEvaluateState(manifest, manifest.Avatar, out IntegrationState state, out FaceMotionDiagnostic blocking))
                    {
                        Assert("verify.ma.stateAttached", state == IntegrationState.Attached, "state=" + state);
                    }
                    else
                    {
                        Assert("verify.ma.stateAttached", false, blocking == null ? "blocked" : blocking.Code);
                    }
                }

                Assert("verify.ma.foreignPreserved", File.Exists(ToAbsolute(Folder + "/note.txt")), Folder + "/note.txt");
            }
            catch (Exception exception)
            {
                _report.notes.Add("VERIFY_ERROR: " + exception);
            }

            WriteReport();
        }

        public static void I7MaMigrateLegacy()
        {
            Initialize("ma-migrate-legacy");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var legacy = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(LegacyPath);
                Assert("migrate.ma.legacyLoads", legacy != null, LegacyPath);
                if (legacy != null)
                {
                    MigrationResult migrated = ModularAvatarIntegrationManifestMigration.TryMigrateOnUse(legacy, legacy.Avatar);
                    Assert("migrate.ma.upgraded", !migrated.Blocked && legacy.SchemaVersion == FaceMotionVersions.IntegrationManifestVersion, "blocked=" + migrated.Blocked + " schema=" + legacy.SchemaVersion);
                    Assert("migrate.ma.idPreserved", StableId.IsValid(legacy.IntegrationId), "id=" + legacy.IntegrationId);
                }

                var avatar = GameObject.Find("I7 MA Avatar")?.GetComponent<VRCAvatarDescriptor>();
                const string ambiguousPath = DataRoot + "/Legacy/AmbiguousMaManifest.asset";
                if (avatar != null)
                {
                    var ambiguous = ScriptableObject.CreateInstance<ModularAvatarIntegrationManifest>();
                    ambiguous.Avatar = avatar;
                    ambiguous.SchemaVersion = FaceMotionVersions.LegacySchemaVersion;
                    ambiguous.IntegrationObjectName = "I7 Ambiguous MA Target";
                    ambiguous.State = IntegrationState.Unknown;
                    AssetDatabase.CreateAsset(ambiguous, ambiguousPath);
                    var foreign = new GameObject(ambiguous.IntegrationObjectName);
                    foreign.transform.SetParent(avatar.transform, false);
                    try
                    {
                        var result = ModularAvatarIntegrationManifestMigration.TryMigrateOnUse(ambiguous, avatar);
                        Assert("migrate.ma.ambiguousBlocked", result.Blocked && HasCode(result.Diagnostics, FaceMotionDiagnosticCodes.IntegrationAmbiguous), Codes(result.Diagnostics));
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(foreign);
                        AssetDatabase.DeleteAsset(ambiguousPath);
                    }
                }

                var future = ScriptableObject.CreateInstance<ModularAvatarIntegrationManifest>();
                future.SchemaVersion = FaceMotionVersions.IntegrationManifestVersion + 1;
                string futurePath = DataRoot + "/FutureMaManifest.asset";
                AssetDatabase.CreateAsset(future, futurePath);
                MigrationResult futureResult = ModularAvatarIntegrationManifestMigration.TryMigrateOnUse(future, null);
                Assert("migrate.ma.futureBlocked", futureResult.Blocked && future.SchemaVersion == FaceMotionVersions.IntegrationManifestVersion + 1, Codes(futureResult.Diagnostics));
                AssetDatabase.DeleteAsset(futurePath);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                _report.notes.Add("MIGRATE_ERROR: " + exception);
            }

            WriteReport();
        }

        public static void I7MaRecovery()
        {
            Initialize("ma-recovery");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var avatar = GameObject.Find("I7 MA Avatar")?.GetComponent<VRCAvatarDescriptor>();
                var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(ManifestPath);
                var backend = ModularAvatarIntegrationBackendLocator.Create();
                if (avatar == null || manifest == null || backend == null)
                {
                    Assert("recovery.ma.preconditions", false, "avatar=" + (avatar != null) + " manifest=" + (manifest != null) + " backend=" + (backend != null));
                }
                else
                {
                    int ownedCount = (manifest.OwnedAssetPaths ?? Array.Empty<string>()).Length;
                    var removed = backend.Remove(avatar);
                    Assert("recovery.ma.remove", removed != null && removed.Succeeded, Detail(removed));
                    var retained = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(ManifestPath);
                    Assert("recovery.ma.manifestRetained", retained != null, ManifestPath);
                    Assert("recovery.ma.ownedRetained", retained != null && (retained.OwnedAssetPaths ?? Array.Empty<string>()).Length == ownedCount, "owned=" + ownedCount);
                    Assert("recovery.ma.foreignPreserved", File.Exists(ToAbsolute(Folder + "/note.txt")), Folder + "/note.txt");
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MotionPath);
                    var reapplied = backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(avatar, clip, DataRoot, "Demo")));
                    Assert("recovery.ma.reapply", reapplied != null && reapplied.Succeeded, Detail(reapplied));
                    Assert("recovery.ma.foreignStillPreserved", File.Exists(ToAbsolute(Folder + "/note.txt")), Folder + "/note.txt");
                }

                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            }
            catch (Exception exception)
            {
                _report.notes.Add("RECOVERY_ERROR: " + exception);
            }

            WriteReport();
        }

        private static void Initialize(string stage)
        {
            _report = new Report { stage = stage };
            _reportPath = GetArgument("-i7Report");
        }

        private static VRCAvatarDescriptor CreateAvatar()
        {
            var avatar = new GameObject("I7 MA Avatar").AddComponent<VRCAvatarDescriptor>();
            var armature = new GameObject("Armature");
            armature.transform.SetParent(avatar.transform, false);
            var hips = new GameObject("Hips");
            hips.transform.SetParent(armature.transform, false);
            var mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero };
            mesh.AddBlendShapeFrame("Smile", 100f, new[] { Vector3.zero }, new[] { Vector3.zero }, new[] { Vector3.zero });
            hips.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;
            return avatar;
        }

        private static void Assert(string name, bool passed, string detail)
        {
            _report.assertions.Add(new Assertion { name = name, passed = passed, detail = detail ?? string.Empty });
        }

        private static void WriteForeignFile(string path, string content)
        {
            string absolute = ToAbsolute(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllText(absolute, content);
            AssetDatabase.Refresh();
        }

        private static string ToAbsolute(string path)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), path.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string Detail(ModularAvatarIntegrationResult result)
        {
            if (result == null) return "null";
            if (result.Succeeded) return "succeeded";
            if (result.Diagnostics == null) return "no diagnostics";
            return string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic == null ? "null" : diagnostic.Code + ": " + diagnostic.Message));
        }

        private static bool HasCode(IReadOnlyList<FaceMotionDiagnostic> diagnostics, string code)
        {
            return diagnostics != null && diagnostics.Any(diagnostic => diagnostic != null && diagnostic.Code == code);
        }

        private static string Codes(IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            return diagnostics == null ? "none" : string.Join(",", diagnostics.Select(diagnostic => diagnostic == null ? "null" : diagnostic.Code));
        }

        private static void WriteReport()
        {
            string json = JsonUtility.ToJson(_report, true);
            if (string.IsNullOrEmpty(_reportPath))
            {
                Debug.Log("[I7MA]\n" + json);
                return;
            }

            string directory = Path.GetDirectoryName(_reportPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(_reportPath, json);
        }

        private static string GetArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.Ordinal)) return arguments[i + 1];
            }

            return null;
        }
    }
}
