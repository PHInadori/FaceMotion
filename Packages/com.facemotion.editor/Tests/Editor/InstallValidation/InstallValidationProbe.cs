using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.Export;
using FaceMotion.Editor.Serialization;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Serialization;
using FaceMotion.Timeline;
using FaceMotion.Versioning;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>Phase I.7 install/update/uninstall validation fixture builder and verifier.</summary>
    public static class InstallValidationProbe
    {
        private const string DataRoot = "Assets/I7Data";
        private const string ScenePath = DataRoot + "/I7Lifecycle.unity";

        private const string ProjectAssetPath = DataRoot + "/FaceMotionProject.asset";
        private const string MappingAssetPath = DataRoot + "/MappingProfile.asset";
        private const string ExportClipPath = DataRoot + "/Exported.anim";
        private const string DirectFolder = DataRoot + "/FaceMotion_Demo";

        private const string LegacyProjectPath = DataRoot + "/Legacy/Project.asset";
        private const string LegacyMappingPath = DataRoot + "/Legacy/Mapping.asset";
        private const string LegacyDirectManifestPath = DirectFolder + "/Manifest.asset";
        private static readonly string[] ForeignPaths = { DirectFolder + "/note.txt" };

        [Serializable]
        private sealed class Report
        {
            public string stage;
            public string unityVersion;
            public bool maAvailable;
            public List<Assertion> assertions = new List<Assertion>();
            public List<string> ownedPaths = new List<string>();
            public List<string> foreignPaths = new List<string>();
            public List<string> notes = new List<string>();
        }

        [Serializable]
        private sealed class Assertion
        {
            public string name;
            public bool passed;
            public string detail;
        }

        private static Report _report = new Report();
        private static string _reportPath;

        // ------------------------------------------------------------------ setup

        public static void I7SetupFixture()
        {
            Initialize("setup", expectsMa: true);
            try
            {
                CreateDataRoot();

                GameObject directAvatarGo = CreateAvatar("I7 Direct Avatar");
                AnimatorController directFx = AnimatorController.CreateAnimatorControllerAtPath(DataRoot + "/DirectOriginal.controller");
                directAvatarGo.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers =
                    new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = directFx } };

                AnimationClip clip = BuildMotionClip(DataRoot + "/Motion.anim");

                // Persist scene objects before capturing their GlobalObjectId in integration manifests.
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);

                FaceMotionProject project = FaceMotionProject.CreateNew();
                project.AddAnimation(BuildAnimation());
                AssetDatabase.CreateAsset(project, ProjectAssetPath);

                AvatarMappingProfile mapping = AvatarMappingProfile.CreateNew("I7 Mapping");
                AssetDatabase.CreateAsset(mapping, MappingAssetPath);

                FaceMotionAnimationData exportAnimation = BuildAnimation();
                var exportResult = AnimationClipExporter.Export(exportAnimation, ExportClipPath);
                Assert("export.clip.created", exportResult != null && exportResult.Succeeded && AssetDatabase.LoadAssetAtPath<AnimationClip>(ExportClipPath) != null,
                    exportResult == null ? "null result" : (exportResult.Succeeded ? clip.name : FirstBlocking(exportResult.Diagnostics)));

                DirectIntegrationPlan directPlan = DirectVRChatIntegration.Plan(
                    new DirectIntegrationRequest(directAvatarGo.GetComponent<VRCAvatarDescriptor>(), clip, DataRoot, "Demo"));
                DirectIntegrationResult directResult = DirectVRChatIntegration.Apply(directPlan);
                Assert("direct.apply", directResult != null && directResult.Succeeded && directResult.Manifest != null,
                    directResult == null ? "null" : (directResult.Succeeded ? "succeeded" : FirstBlocking(directResult.Diagnostics)));
                DirectIntegrationManifest directManifest = directResult?.Manifest;
                if (directManifest != null)
                {
                    foreach (string owned in directManifest.OwnedAssetPaths ?? Array.Empty<string>()) _report.ownedPaths.Add(owned);
                    _report.notes.Add("DirectManifest path: " + AssetDatabase.GetAssetPath(directManifest));
                    _report.notes.Add("DirectManifest integrationId: " + directManifest.IntegrationId);
                }

                CreateLegacyFixtures(directAvatarGo);

                WriteForeignFile(DirectFolder, "note.txt", "foreign-direct");
                _report.foreignPaths.AddRange(ForeignPaths);
                AssetDatabase.Refresh();

                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
                AssetDatabase.SaveAssets();

                _report.notes.Add("scene: " + ScenePath);
                _report.notes.Add("projectId: " + project.ProjectId);
                _report.notes.Add("mappingProfileId: " + mapping.ProfileId);
                _report.notes.Add("exportClipScriptResolves: n/a (plain AnimationClip)");
            }
            catch (Exception exception)
            {
                _report.notes.Add("SETUP_ERROR: " + exception);
            }

            WriteReport();
        }

        // ------------------------------------------------------------------ verify

        public static void I7VerifyState()
        {
            Initialize("verify", expectsMa: true);
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                FaceMotionProject project = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(ProjectAssetPath);
                Assert("verify.project.loads", project != null, project == null ? "not loadable" : "loads");
                if (project != null)
                {
                    Assert("verify.project.schema", project.SchemaVersion == FaceMotionVersions.ProjectSchemaVersion, "schema=" + project.SchemaVersion);
                    Assert("verify.project.hasAnimation", project.Animations != null && project.Animations.Count == 1, "animationCount=" + (project.Animations == null ? -1 : project.Animations.Count));
                    Assert("verify.project.idValid", StableId.IsValid(project.ProjectId), "id=" + project.ProjectId);
                }

                AvatarMappingProfile mapping = AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(MappingAssetPath);
                Assert("verify.mapping.loads", mapping != null && mapping.SchemaVersion == FaceMotionVersions.MappingProfileSchemaVersion,
                    mapping == null ? "not loadable" : ("schema=" + mapping.SchemaVersion));

                Assert("verify.exportClip.present", AssetDatabase.LoadAssetAtPath<AnimationClip>(ExportClipPath) != null, "path=" + ExportClipPath);

                bool directManifestFile = System.IO.File.Exists(ToAbsolute(DirectFolder + "/Manifest.asset"));
                Assert("verify.direct.manifestFile", directManifestFile, "file=" + DirectFolder + "/Manifest.asset");
                DirectIntegrationManifest direct = AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(DirectFolder + "/Manifest.asset");
                Assert("verify.direct.manifestLoads", direct != null, direct == null ? "not loadable" : "loads");
                if (direct != null)
                {
                    Assert("verify.direct.schema", direct.SchemaVersion == FaceMotionVersions.IntegrationManifestVersion, "schema=" + direct.SchemaVersion);
                    Assert("verify.direct.hasIntegrationId", StableId.IsValid(direct.IntegrationId), "id=" + direct.IntegrationId);
                    Assert("verify.direct.ownedNonEmpty", direct.OwnedAssetPaths != null && direct.OwnedAssetPaths.Length > 0, "owned=" + (direct.OwnedAssetPaths == null ? 0 : direct.OwnedAssetPaths.Length));
                    foreach (string path in direct.OwnedAssetPaths ?? Array.Empty<string>())
                    {
                        Assert("verify.direct.owned.exists(" + path + ")", System.IO.File.Exists(ToAbsolute(path)), "file=" + path);
                    }
                }

                foreach (string foreign in ForeignPaths)
                {
                    Assert("verify.foreign.exists(" + foreign + ")", System.IO.File.Exists(ToAbsolute(foreign)), "file=" + foreign);
                }
            }
            catch (Exception exception)
            {
                _report.notes.Add("VERIFY_ERROR: " + exception);
            }

            WriteReport();
        }

        // ------------------------------------------------------------------ legacy migration

        public static void I7MigrateLegacy()
        {
            Initialize("migrate-legacy", expectsMa: true);
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                Assert("migrate.legacy.project.file", System.IO.File.Exists(ToAbsolute(LegacyProjectPath)), LegacyProjectPath);
                FaceMotionProject legacyProject = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(LegacyProjectPath);
                if (legacyProject != null)
                {
                    int before = legacyProject.SchemaVersion;
                    string legacyId = legacyProject.ProjectId;
                    var result = ProjectMigrationService.TryMigrateOnLoad(legacyProject);
                    FaceMotionProject reloaded = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(LegacyProjectPath);
                    bool statusOk = result != null && result.Status == ProjectMigrationStatus.Migrated;
                    Assert("migrate.project.upgraded", statusOk && reloaded != null && reloaded.SchemaVersion == FaceMotionVersions.ProjectSchemaVersion,
                        "before=" + before + " status=" + (result == null ? "null" : result.Status.ToString()) + " after=" + (reloaded == null ? "null" : reloaded.SchemaVersion.ToString()));
                    Assert("migrate.project.idPreserved", reloaded != null && !string.IsNullOrEmpty(legacyId) && reloaded.ProjectId == legacyId,
                        "before=" + legacyId + " after=" + (reloaded == null ? "null" : reloaded.ProjectId));

                    var second = ProjectMigrationService.TryMigrateOnLoad(reloaded);
                    Assert("migrate.project.secondPassNoop", second != null && second.Status != ProjectMigrationStatus.Migrated,
                        "second=" + (second == null ? "null" : second.Status.ToString()));
                }
                else
                {
                    Assert("migrate.project.upgraded", false, "legacy project not loadable");
                }

                Assert("migrate.legacy.mapping.file", System.IO.File.Exists(ToAbsolute(LegacyMappingPath)), LegacyMappingPath);
                AvatarMappingProfile legacyMapping = AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(LegacyMappingPath);
                if (legacyMapping != null)
                {
                    var mappingResult = MappingProfileMigrationService.TryMigrateOnLoad(legacyMapping);
                    AvatarMappingProfile reloadedMapping = AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(LegacyMappingPath);
                    Assert("migrate.mapping.upgraded", mappingResult != null && !mappingResult.Blocked && reloadedMapping != null && reloadedMapping.SchemaVersion == FaceMotionVersions.MappingProfileSchemaVersion,
                        "blocked=" + (mappingResult == null ? "null" : mappingResult.Blocked.ToString()) + " after=" + (reloadedMapping == null ? "null" : reloadedMapping.SchemaVersion.ToString()));
                    var mappingSecond = MappingProfileMigrationService.TryMigrateOnLoad(reloadedMapping);
                    Assert("migrate.mapping.secondPassNoop", mappingSecond == null || !mappingSecond.Applied, "applied=" + (mappingSecond == null ? "null" : mappingSecond.Applied.ToString()));
                }
                else
                {
                    Assert("migrate.mapping.upgraded", false, "legacy mapping not loadable");
                }

                MigrateLegacyDirect();
            }
            catch (Exception exception)
            {
                _report.notes.Add("MIGRATE_ERROR: " + exception);
            }

            WriteReport();
        }

        private static void MigrateLegacyDirect()
        {
            Assert("migrate.legacy.direct.file", System.IO.File.Exists(ToAbsolute(LegacyDirectManifestPath)), LegacyDirectManifestPath);
            DirectIntegrationManifest legacyDirect = AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(LegacyDirectManifestPath);
            if (legacyDirect == null)
            {
                Assert("migrate.direct.upgraded", false, "legacy direct manifest not loadable");
                return;
            }

            int before = legacyDirect.SchemaVersion;
            string beforeId = legacyDirect.IntegrationId;
            var result = DirectIntegrationManifestMigration.TryMigrateOnUse(legacyDirect);
            DirectIntegrationManifest reloaded = AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(LegacyDirectManifestPath);
            Assert("migrate.direct.upgraded", !result.Blocked && reloaded != null && reloaded.SchemaVersion == FaceMotionVersions.IntegrationManifestVersion,
                "blocked=" + result.Blocked + " codes=" + Codes(result.Diagnostics) + " after=" + (reloaded == null ? "null" : reloaded.SchemaVersion.ToString()));
            if (reloaded != null && !string.IsNullOrEmpty(beforeId))
            {
                Assert("migrate.direct.idPreserved", reloaded.IntegrationId == beforeId, "before=" + beforeId + " after=" + reloaded.IntegrationId);
            }

            // future schema blocked (persisted copy)
            CreateFutureSchemaDirectManifest();
        }

        // ------------------------------------------------------------------ recovery

        public static void I7Recovery()
        {
            Initialize("recovery", expectsMa: true);
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                DirectIntegrationManifest direct = AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(DirectFolder + "/Manifest.asset");
                if (direct != null)
                {
                    List<string> ownedBefore = (direct.OwnedAssetPaths ?? Array.Empty<string>()).ToList();
                    HashSet<string> ownedSet = new HashSet<string>(ownedBefore, StringComparer.Ordinal);
                    bool rollbackSucceeded = DirectVRChatIntegration.Rollback(direct, out var rollbackDiagnostics);
                    Assert("recovery.direct.rollback", rollbackSucceeded, "code=" + Codes(rollbackDiagnostics));

                    int remainingOwned = 0;
                    foreach (string path in ownedSet)
                    {
                        if (System.IO.File.Exists(ToAbsolute(path))) remainingOwned++;
                    }

                    Assert("recovery.direct.ownedDeleted", remainingOwned == 0, "remainingOwned=" + remainingOwned);
                    Assert("recovery.direct.manifestDeleted", !System.IO.File.Exists(ToAbsolute(DirectFolder + "/Manifest.asset")), "file=" + DirectFolder + "/Manifest.asset");
                    Assert("recovery.direct.foreignPreserved", System.IO.File.Exists(ToAbsolute(DirectFolder + "/note.txt")), "file=" + DirectFolder + "/note.txt");
                }
                else
                {
                    Assert("recovery.direct.rollback", false, "direct manifest not loadable");
                }

                // Direct reapply after rollback
                GameObject directAvatar = GameObject.Find("I7 Direct Avatar");
                if (directAvatar != null && AssetDatabase.LoadAssetAtPath<AnimationClip>(DataRoot + "/Motion.anim") != null)
                {
                    DirectIntegrationPlan rePlan = DirectVRChatIntegration.Plan(
                        new DirectIntegrationRequest(directAvatar.GetComponent<VRCAvatarDescriptor>(), AssetDatabase.LoadAssetAtPath<AnimationClip>(DataRoot + "/Motion.anim"), DataRoot, "Demo"));
                    DirectIntegrationResult reResult = DirectVRChatIntegration.Apply(rePlan);
                    Assert("recovery.direct.sameNameBlockedByForeign", reResult != null && !reResult.Succeeded && HasCode(reResult.Diagnostics, "FM-G-OUTPUT-CONFLICT"), reResult == null ? "null" : FirstBlocking(reResult.Diagnostics));
                    DirectIntegrationPlan recoveredPlan = DirectVRChatIntegration.Plan(
                        new DirectIntegrationRequest(directAvatar.GetComponent<VRCAvatarDescriptor>(), AssetDatabase.LoadAssetAtPath<AnimationClip>(DataRoot + "/Motion.anim"), DataRoot, "DemoRecovered"));
                    DirectIntegrationResult recoveredResult = DirectVRChatIntegration.Apply(recoveredPlan);
                    Assert("recovery.direct.reapplyDifferentName", recoveredResult != null && recoveredResult.Succeeded, recoveredResult == null ? "null" : (recoveredResult.Succeeded ? "succeeded" : FirstBlocking(recoveredResult.Diagnostics)));
                }

                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                _report.notes.Add("RECOVERY_ERROR: " + exception);
            }

            WriteReport();
        }

        public static void I7VerifyReinstalledState()
        {
            Initialize("verify-reinstalled", expectsMa: false);
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                FaceMotionProject project = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(ProjectAssetPath);
                AvatarMappingProfile mapping = AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(MappingAssetPath);
                Assert("reinstall.projectRecognized", project != null && StableId.IsValid(project.ProjectId), project == null ? "missing" : project.ProjectId);
                Assert("reinstall.mappingRecognized", mapping != null && mapping.SchemaVersion == FaceMotionVersions.MappingProfileSchemaVersion, mapping == null ? "missing" : "schema=" + mapping.SchemaVersion);
                Assert("reinstall.exportClipPresent", AssetDatabase.LoadAssetAtPath<AnimationClip>(ExportClipPath) != null, ExportClipPath);
                DirectIntegrationManifest direct = FindAnyDirectManifest();
                Assert("reinstall.directManifestRecognized", direct != null, direct == null ? "missing" : AssetDatabase.GetAssetPath(direct));
                if (direct != null)
                {
                    Assert("reinstall.directManifestSchema", direct.SchemaVersion == FaceMotionVersions.IntegrationManifestVersion, "schema=" + direct.SchemaVersion);
                    Assert("reinstall.directManifestId", StableId.IsValid(direct.IntegrationId), "id=" + direct.IntegrationId);
                    Assert("reinstall.directOwnedAssetsPresent", (direct.OwnedAssetPaths ?? Array.Empty<string>()).All(path => File.Exists(ToAbsolute(path))), "owned=" + (direct.OwnedAssetPaths == null ? 0 : direct.OwnedAssetPaths.Length));
                }
            }
            catch (Exception exception)
            {
                _report.notes.Add("REINSTALL_VERIFY_ERROR: " + exception);
            }

            WriteReport();
        }

        public static void I7RecoveryAfterReinstall()
        {
            Initialize("recovery-after-reinstall", expectsMa: false);
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                DirectIntegrationManifest direct = FindAnyDirectManifest();
                VRCAvatarDescriptor avatar = DirectIntegrationManifestMigration.ResolveAvatar(direct);
                Assert("reinstall.directAvatarResolved", avatar != null, avatar == null ? "missing" : avatar.name);
                if (direct != null && avatar != null)
                {
                    bool rolledBack = DirectVRChatIntegration.Rollback(direct, out var diagnostics);
                    Assert("reinstall.directRollback", rolledBack, Codes(diagnostics));
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DataRoot + "/Motion.anim");
                    DirectIntegrationResult reapplied = DirectVRChatIntegration.Apply(DirectVRChatIntegration.Plan(new DirectIntegrationRequest(avatar, clip, DataRoot, "DemoReinstalled")));
                    Assert("reinstall.directReapply", reapplied != null && reapplied.Succeeded, reapplied == null ? "null" : FirstBlocking(reapplied.Diagnostics));
                }
            }
            catch (Exception exception)
            {
                _report.notes.Add("REINSTALL_RECOVERY_ERROR: " + exception);
            }

            WriteReport();
        }

        // ------------------------------------------------------------------ resolved state

        public static void I7ResolvedState()
        {
            Initialize("resolved-state", expectsMa: false);
            _report.notes.Add("FaceMotion.Editor.Core loaded: " + (Type.GetType("FaceMotion.Versioning.FaceMotionVersions, FaceMotion.Editor.Core") != null));
            _report.notes.Add("FaceMotion.Editor.UI loaded: " + (Type.GetType("FaceMotion.Editor.UI.Window.FaceMotionWindow, FaceMotion.Editor.UI") != null));
            _report.notes.Add("FaceMotion.Editor.VRChat loaded: " + (Type.GetType("FaceMotion.Editor.VRChat.Integration.DirectVRChatIntegration, FaceMotion.Editor.VRChat") != null));
            _report.notes.Add("FaceMotion.Editor.ModularAvatar loaded: " + (Type.GetType("FaceMotion.Editor.ModularAvatar.ModularAvatarIntegrationBackend, FaceMotion.Editor.ModularAvatar") != null));
            _report.notes.Add("MA core loaded: " + (Type.GetType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator, nadena.dev.modular-avatar.core") != null));
            Assert("resolved.direct.backend", DirectVRChatIntegration.BackendVersion == 1 && !string.IsNullOrEmpty(DirectVRChatIntegration.BackendId), DirectVRChatIntegration.BackendId);
            WriteReport();
        }

        // ------------------------------------------------------------------ helpers

        private static void Initialize(string stage, bool expectsMa)
        {
            _report = new Report { stage = stage, unityVersion = Application.unityVersion, maAvailable = false };
            _reportPath = GetArgument("-i7Report");
        }

        private static void CreateDataRoot()
        {
            if (!AssetDatabase.IsValidFolder(DataRoot))
            {
                string parent = "Assets";
                foreach (string segment in "I7Data".Split('/'))
                {
                    string candidate = parent + "/" + segment;
                    if (!AssetDatabase.IsValidFolder(candidate))
                    {
                        AssetDatabase.CreateFolder(parent, segment);
                    }

                    parent = candidate;
                }

                AssetDatabase.CreateFolder(DataRoot, "Legacy");
            }
        }

        private static GameObject CreateAvatar(string name)
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!currentScene.IsValid() || currentScene.rootCount == 0)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            var go = new GameObject(name);
            go.AddComponent<VRCAvatarDescriptor>();
            var armature = new GameObject("Armature");
            armature.transform.SetParent(go.transform, false);
            var hips = new GameObject("Hips");
            hips.transform.SetParent(armature.transform, false);
            var mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero };
            mesh.AddBlendShapeFrame("Smile", 100f, new[] { Vector3.zero }, new[] { Vector3.zero }, new[] { Vector3.zero });
            hips.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;
            return go;
        }

        private static AnimationClip BuildMotionClip(string assetPath)
        {
            var clip = new AnimationClip();
            clip.SetCurve("Armature/Hips", typeof(SkinnedMeshRenderer), "blendShape.Smile",
                AnimationCurve.Linear(0f, 0f, 1f, 100f));
            AssetDatabase.CreateAsset(clip, assetPath);
            return clip;
        }

        private static FaceMotionAnimationData BuildAnimation()
        {
            FaceMotionAnimationData animation = FaceMotionAnimationData.Create("I7 Demo");
            FaceTrackData track = FaceTrackData.CreateBlendShape("Body/Renderer", "Smile");
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, 0f));
            track.BlendShape.AddKey(FloatKeyframeData.Create(1f, 100f));
            animation.Timeline.AddTrack(track);
            return animation;
        }

        private static void CreateLegacyFixtures(GameObject directAvatar)
        {
            FaceMotionProject legacyProject = FaceMotionProject.CreateNew();
            legacyProject.AddAnimation(BuildAnimation());
            ReflectionUtil.SetField(legacyProject, "_schemaVersion", FaceMotionVersions.LegacySchemaVersion);
            AssetDatabase.CreateAsset(legacyProject, LegacyProjectPath);

            AvatarMappingProfile legacyMapping = AvatarMappingProfile.CreateNew("I7 Legacy Mapping");
            ReflectionUtil.SetField(legacyMapping, "_schemaVersion", FaceMotionVersions.LegacySchemaVersion);
            AssetDatabase.CreateAsset(legacyMapping, LegacyMappingPath);

            DirectIntegrationManifest legacyDirect = AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(LegacyDirectManifestPath);
            if (legacyDirect == null) throw new InvalidOperationException("Could not load the applied direct manifest for legacy migration.");
            legacyDirect.SchemaVersion = FaceMotionVersions.LegacySchemaVersion;
            legacyDirect.BackendId = string.Empty;
            EditorUtility.SetDirty(legacyDirect);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static DirectIntegrationManifest FindAnyDirectManifest()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:DirectIntegrationManifest"))
            {
                DirectIntegrationManifest manifest = AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(AssetDatabase.GUIDToAssetPath(guid));
                if (manifest != null && AssetDatabase.GetAssetPath(manifest).StartsWith(DataRoot + "/", StringComparison.Ordinal)) return manifest;
            }

            return null;
        }

        private static void CreateFutureSchemaDirectManifest()
        {
            var future = ScriptableObject.CreateInstance<DirectIntegrationManifest>();
            future.SchemaVersion = FaceMotionVersions.IntegrationManifestVersion + 1;
            future.IntegrationId = StableId.New();
            string path = DataRoot + "/FutureDirectManifest.asset";
            AssetDatabase.CreateAsset(future, path);
            DirectIntegrationManifest loaded = AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(path);
            MigrationResult result = DirectIntegrationManifestMigration.TryMigrateOnUse(loaded);
            Assert("migrate.direct.futureBlocked", result.Blocked && loaded.SchemaVersion == FaceMotionVersions.IntegrationManifestVersion + 1,
                "blocked=" + result.Blocked + " schema=" + loaded.SchemaVersion);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
        }

        private static void WriteForeignFile(string folder, string fileName, string content)
        {
            string absolute = ToAbsolute(folder);
            if (!Directory.Exists(absolute))
            {
                Directory.CreateDirectory(absolute);
            }

            File.WriteAllText(Path.Combine(absolute, fileName), content);
        }

        private static void Assert(string name, bool passed, string detail)
        {
            _report.assertions.Add(new Assertion { name = name, passed = passed, detail = detail ?? string.Empty });
        }

        private static bool HasCode(IReadOnlyList<FaceMotionDiagnostic> diagnostics, string code)
        {
            if (diagnostics == null) return false;
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i] != null && diagnostics[i].Code == code) return true;
            }

            return false;
        }

        private static string Codes(IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            if (diagnostics == null) return "none";
            return string.Join(",", diagnostics.Select(d => d == null ? "null" : d.Code));
        }

        private static string FirstBlocking(IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            if (diagnostics == null) return "no diagnostics";
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i] != null && diagnostics[i].Blocking) return diagnostics[i].Code + ": " + diagnostics[i].Message;
            }

            return "no blocking diagnostics";
        }

        private static string ToAbsolute(string projectRelativePath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteReport()
        {
            if (string.IsNullOrEmpty(_reportPath))
            {
                Debug.Log("[I7Probe]\n" + JsonUtility.ToJson(_report, true));
                return;
            }

            string directory = Path.GetDirectoryName(_reportPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_reportPath, JsonUtility.ToJson(_report, true));
            Debug.Log("I7 probe report written: " + _reportPath);
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
