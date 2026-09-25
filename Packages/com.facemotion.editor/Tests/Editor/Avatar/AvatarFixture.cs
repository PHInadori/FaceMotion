using System;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// In-memory avatar hierarchy for EditMode tests. No assets are generated: meshes are
    /// built procedurally on the heap and the whole hierarchy is destroyed on dispose.
    /// </summary>
    internal sealed class AvatarFixture : IDisposable
    {
        public const string RootName = "AvatarRoot";
        public const string FaceRendererPath = "Body/Face";
        public const string CheekRendererPath = "Body/Cheek";
        public const string HiddenRendererPath = "Body/Hidden";
        public const string NoMeshRendererPath = "Body/NoMesh";
        public const string HeadPath = "Armature/Hips/Spine/Head";
        public const string LeftEarPath = HeadPath + "/LeftEar";
        public const string RightEarPath = HeadPath + "/RightEar";
        public const string WristPath = "Body/Wrist";

        public GameObject Root;
        public VRCAvatarDescriptor Descriptor;
        public Transform Armature;
        public Transform Hips;
        public Transform Spine;
        public Transform Head;
        public Transform LeftEar;
        public Transform RightEar;
        public Transform Body;
        public Transform Face;
        public Transform Cheek;
        public Transform Hidden;
        public Transform NoMesh;
        public Transform Wrist;
        public Mesh FaceMesh;
        public Mesh CheekMesh;
        public Mesh HiddenMesh;
        public SkinnedMeshRenderer FaceRenderer;
        public SkinnedMeshRenderer CheekRenderer;
        public SkinnedMeshRenderer HiddenRenderer;
        public SkinnedMeshRenderer NoMeshRenderer;

        private AvatarFixture()
        {
        }

        public static AvatarFixture Create()
        {
            var fixture = new AvatarFixture();
            var root = new GameObject(RootName);
            fixture.Build(root);
            return fixture;
        }

        public void AddFork(string childName)
        {
            var fork = new GameObject(childName);
            fork.transform.SetParent(Head, worldPositionStays: false);
        }

        public void Dispose()
        {
            if (FaceMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(FaceMesh);
            }

            if (CheekMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(CheekMesh);
            }

            if (HiddenMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(HiddenMesh);
            }

            if (Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private void Build(GameObject root)
        {
            Root = root;
            Root.name = RootName;
            Descriptor = Root.AddComponent<VRCAvatarDescriptor>();

            Armature = new GameObject("Armature").transform;
            Hips = new GameObject("Hips").transform;
            Spine = new GameObject("Spine").transform;
            Head = new GameObject("Head").transform;
            LeftEar = new GameObject("LeftEar").transform;
            RightEar = new GameObject("RightEar").transform;
            Body = new GameObject("Body").transform;
            Face = new GameObject("Face").transform;
            Cheek = new GameObject("Cheek").transform;
            Hidden = new GameObject("Hidden").transform;
            NoMesh = new GameObject("NoMesh").transform;
            Wrist = new GameObject("Wrist").transform;

            SetParent(Armature, Root.transform);
            SetParent(Hips, Armature);
            SetParent(Spine, Hips);
            SetParent(Head, Spine);
            SetParent(LeftEar, Head);
            SetParent(RightEar, Head);
            SetParent(Body, Root.transform);
            SetParent(Face, Body);
            SetParent(Cheek, Body);
            SetParent(Hidden, Body);
            SetParent(NoMesh, Body);
            SetParent(Wrist, Body);

            FaceMesh = CreateMesh("FaceMesh", "Mouth_Smile", "EyeBlink_L", "EyeBlink_R", "Smile");
            CheekMesh = CreateMesh("CheekMesh", "Smile");
            HiddenMesh = CreateMesh("HiddenMesh", "Secret_Smile");

            FaceRenderer = AttachRenderer(Face, FaceMesh);
            CheekRenderer = AttachRenderer(Cheek, CheekMesh);
            HiddenRenderer = AttachRenderer(Hidden, HiddenMesh);
            NoMeshRenderer = AttachRenderer(NoMesh, null);

            Hidden.gameObject.SetActive(false);
        }

        private static SkinnedMeshRenderer AttachRenderer(Transform target, Mesh mesh)
        {
            var renderer = target.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            return renderer;
        }

        private static void SetParent(Transform child, Transform parent)
        {
            child.SetParent(parent, worldPositionStays: false);
        }

        private static Mesh CreateMesh(string meshName, params string[] blendShapes)
        {
            var mesh = new Mesh();
            mesh.name = meshName;
            mesh.vertices = new[]
            {
                new Vector3(-1f, -0.5f, 0f),
                new Vector3(1f, -0.5f, 0f),
                new Vector3(0f, 0.5f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateNormals();

            var visibleDeltas = new[] { new Vector3(0.01f, 0f, 0f), Vector3.zero, Vector3.zero };
            if (blendShapes != null)
            {
                foreach (string name in blendShapes)
                {
                    mesh.AddBlendShapeFrame(name, 0f, visibleDeltas, null, null);
                }
            }

            return mesh;
        }
    }
}
