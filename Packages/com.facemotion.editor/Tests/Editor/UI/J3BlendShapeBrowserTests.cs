using FaceMotion.Data;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.VRChat;
using NUnit.Framework;

namespace FaceMotion.Editor.Tests
{
    public sealed class J3BlendShapeBrowserTests
    {
        [TestCase("Body/Face", "Face", "EyeBlink_L", AvatarCandidateSnapshot.BlendShapeCategory.Blink)]
        [TestCase("Body/Face", "Face", "eye_close", AvatarCandidateSnapshot.BlendShapeCategory.Blink)]
        [TestCase("Body/Face", "Face", "brow_up", AvatarCandidateSnapshot.BlendShapeCategory.Brow)]
        [TestCase("Body/Face", "Face", "眉下", AvatarCandidateSnapshot.BlendShapeCategory.Brow)]
        [TestCase("Body/Face", "Face", "Mouth_Smile", AvatarCandidateSnapshot.BlendShapeCategory.Mouth)]
        [TestCase("Body/Face", "Face", "あ", AvatarCandidateSnapshot.BlendShapeCategory.Mouth)]
        [TestCase("Body/Face", "Face", "Iris_Small", AvatarCandidateSnapshot.BlendShapeCategory.Eye)]
        [TestCase("Body/Hair", "Hair", "eye_smile", AvatarCandidateSnapshot.BlendShapeCategory.Mouth)]
        [TestCase("Outfit/Jacket", "Jacket", "hide", AvatarCandidateSnapshot.BlendShapeCategory.Clothes)]
        [TestCase("Body/Skin", "Skin", "Slim", AvatarCandidateSnapshot.BlendShapeCategory.Body)]
        [TestCase("Head/Face", "Face", "CheekPuff", AvatarCandidateSnapshot.BlendShapeCategory.FaceOther)]
        [TestCase("Accessory/Prop", "Prop", "UnknownShape", AvatarCandidateSnapshot.BlendShapeCategory.Other)]
        [TestCase("Hair/Back", "Hair_Back", "Bangs", AvatarCandidateSnapshot.BlendShapeCategory.Hair)]
        [TestCase("Hair/Back", "Hair_Back", "option_contour_thick", AvatarCandidateSnapshot.BlendShapeCategory.FaceOther)]
        [TestCase("Hair/Back", "Hair_Back", "option_contour_thin", AvatarCandidateSnapshot.BlendShapeCategory.FaceOther)]
        [TestCase("Hair/Back", "Hair_Back", "option_face_on", AvatarCandidateSnapshot.BlendShapeCategory.FaceOther)]
        [TestCase("Hair/Back", "Hair_Back", "option_face_otan", AvatarCandidateSnapshot.BlendShapeCategory.FaceOther)]
        [TestCase("Hair/Back", "Hair_Back", "contour_soft", AvatarCandidateSnapshot.BlendShapeCategory.FaceOther)]
        [TestCase("zz_Renderer", "zz_00", "zz_01", AvatarCandidateSnapshot.BlendShapeCategory.Other)]
        [TestCase("zz_Renderer", "zz_00", "mystery_shape_x", AvatarCandidateSnapshot.BlendShapeCategory.Other)]
        public void NameAndRendererHeuristics_ClassifyExpectedCategory(
            string path,
            string renderer,
            string shape,
            AvatarCandidateSnapshot.BlendShapeCategory expected)
        {
            var binding = new BlendShapeBinding(path, shape);
            var conflicts = VrcBlendShapeConflictIndex.Build(null);

            var actual = AvatarCandidateSnapshot.Classify(path, renderer, shape, conflicts, binding, out string reason);

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(reason, Is.Not.Empty);
        }

        [TestCase("-----Brow-----", true)]
        [TestCase("=====Eye=====", true)]
        [TestCase("________Mouth________", true)]
        [TestCase("--- Eye ---", true)]
        [TestCase("■■ Brow ■■", true)]
        [TestCase("-----", true)]
        [TestCase("=====VRC empty=====", true)]
        [TestCase("===chimera===", true)]
        [TestCase("===expression===", true)]
        [TestCase("BrowDown", false)]
        [TestCase("Eye_Close", false)]
        [TestCase("Mouth-A", false)]
        [TestCase("Smile", false)]
        [TestCase("expression_smile", false)]
        [TestCase("VRC_Blink", false)]
        [TestCase("mouth_smile", false)]
        public void SeparatorDetection_HidesOnlyDecorativeNames(string name, bool expected)
        {
            Assert.That(AvatarCandidateSnapshot.IsSeparatorName(name), Is.EqualTo(expected));
        }

        [TestCase("blink", true)]
        [TestCase("body/face", true)]
        [TestCase("Face", true)]
        [TestCase("Blink", true)]
        [TestCase("hair", false)]
        public void CandidateSearch_MatchesNamePathRendererAndCategory(string query, bool expected)
        {
            var candidate = new AvatarCandidateSnapshot.BlendShapeCandidate(
                "Body/Face",
                "EyeBlink_L",
                "Face",
                0,
                AvatarCandidateSnapshot.BlendShapeCategory.Blink);

            Assert.That(candidate.MatchesSearch(query), Is.EqualTo(expected));
        }

        [TestCase(AvatarCandidateSnapshot.BlendShapeCategory.Eye, "Face")]
        [TestCase(AvatarCandidateSnapshot.BlendShapeCategory.Blink, "Face")]
        [TestCase(AvatarCandidateSnapshot.BlendShapeCategory.Brow, "Face")]
        [TestCase(AvatarCandidateSnapshot.BlendShapeCategory.Mouth, "Face")]
        [TestCase(AvatarCandidateSnapshot.BlendShapeCategory.FaceOther, "Face")]
        [TestCase(AvatarCandidateSnapshot.BlendShapeCategory.Hair, "Hair")]
        [TestCase(AvatarCandidateSnapshot.BlendShapeCategory.Body, "Body")]
        [TestCase(AvatarCandidateSnapshot.BlendShapeCategory.Clothes, "Clothes")]
        [TestCase(AvatarCandidateSnapshot.BlendShapeCategory.Other, "Other")]
        public void Category_MapsToExpectedTopLevelTree(AvatarCandidateSnapshot.BlendShapeCategory category, string expected)
        {
            var candidate = new AvatarCandidateSnapshot.BlendShapeCandidate("Body/Face", "Shape", category: category);

            Assert.That(candidate.TopLevelCategory, Is.EqualTo(expected));
        }

        [Test]
        public void SameNameBindings_OnDifferentRenderersRemainDistinct()
        {
            var face = new BlendShapeBinding("Body/Face", "Smile");
            var cheek = new BlendShapeBinding("Body/Cheek", "Smile");

            Assert.That(face, Is.Not.EqualTo(cheek));
        }

        [Test]
        public void EmptyConflictIndex_LeavesCandidateSafe()
        {
            VrcBlendShapeConflict conflict = VrcBlendShapeConflictIndex.Build(null).Get(new BlendShapeBinding("Body/Face", "Smile"));

            Assert.That(conflict.IsConflict, Is.False);
            Assert.That(conflict.IsWarning, Is.False);
        }
    }
}
