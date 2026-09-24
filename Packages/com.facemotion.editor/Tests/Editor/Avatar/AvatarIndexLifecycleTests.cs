using FaceMotion.Editor.Avatar;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Preview;
using FaceMotion.Editor.UI.Session;
using NUnit.Framework;

namespace FaceMotion.Editor.Tests
{
    public sealed class AvatarIndexLifecycleTests
    {
        private AvatarFixture _firstAvatar;
        private AvatarFixture _secondAvatar;
        private FaceMotionEditorSession _session;
        private AvatarController _controller;
        private int _buildCount;

        [SetUp]
        public void SetUp()
        {
            _buildCount = 0;
            _session = new FaceMotionEditorSession();
            _controller = new AvatarController(_session, _ => _buildCount++);
        }

        [TearDown]
        public void TearDown()
        {
            _firstAvatar?.Dispose();
            _secondAvatar?.Dispose();
        }

        [Test]
        public void EnsureAvatarIndexCurrent_MissingAvatar_DoesNotBuild()
        {
            int revision = _session.PreviewRevision;

            Assert.That(_controller.EnsureAvatarIndexCurrent(), Is.False);
            Assert.That(_buildCount, Is.Zero);
            Assert.That(_session.PreviewRevision, Is.EqualTo(revision));
        }

        [Test]
        public void EnsureAvatarIndexCurrent_DirtyIndex_RebuildsOnce()
        {
            SetFirstAvatar();
            _firstAvatar.AddFork("ChangedHierarchy");
            _controller.MarkAvatarDirtyFromHierarchy();

            Assert.That(_controller.EnsureAvatarIndexCurrent(), Is.True);
            Assert.That(_buildCount, Is.EqualTo(2));
            Assert.That(_session.AvatarIndexDirty, Is.False);
        }

        [Test]
        public void EnsureAvatarIndexCurrent_ValidIndex_RepeatedCallsDoNotRebuild()
        {
            SetFirstAvatar();
            int revision = _session.PreviewRevision;

            Assert.That(_controller.EnsureAvatarIndexCurrent(), Is.False);
            Assert.That(_controller.EnsureAvatarIndexCurrent(), Is.False);
            Assert.That(_buildCount, Is.EqualTo(1));
            Assert.That(_session.PreviewRevision, Is.EqualTo(revision));
        }

        [Test]
        public void RefreshIndexAfterIntegration_AddedChild_RebuildsOnceAndClearsDirty()
        {
            SetFirstAvatar();
            _firstAvatar.AddFork("FaceMotion MA Added");

            Assert.That(_controller.RefreshIndexAfterIntegration(_firstAvatar.Descriptor), Is.True);
            Assert.That(_buildCount, Is.EqualTo(2));
            Assert.That(_session.AvatarIndexDirty, Is.False);
            Assert.That(_session.ActiveObjectCache.MatchesCurrentHierarchy(_firstAvatar.Root), Is.True);
            Assert.That(_controller.RefreshIndexAfterIntegration(_firstAvatar.Descriptor), Is.False);
            Assert.That(_buildCount, Is.EqualTo(2));
        }

        [Test]
        public void RefreshIndexAfterIntegration_RemovedChild_RebuildsOnceAndClearsDirty()
        {
            SetFirstAvatar();
            var integration = new UnityEngine.GameObject("FaceMotion MA Removed");
            integration.transform.SetParent(_firstAvatar.Root.transform, false);
            _controller.RefreshIndexAfterIntegration(_firstAvatar.Descriptor);
            UnityEngine.Object.DestroyImmediate(integration);

            Assert.That(_controller.RefreshIndexAfterIntegration(_firstAvatar.Descriptor), Is.True);
            Assert.That(_buildCount, Is.EqualTo(3));
            Assert.That(_session.AvatarIndexDirty, Is.False);
            Assert.That(_session.ActiveObjectCache.MatchesCurrentHierarchy(_firstAvatar.Root), Is.True);
        }

        [Test]
        public void RefreshIndexAfterIntegration_UnchangedOrForeignAvatar_DoesNotRebuild()
        {
            SetFirstAvatar();
            _secondAvatar = AvatarFixture.Create();

            Assert.That(_controller.RefreshIndexAfterIntegration(_firstAvatar.Descriptor), Is.False);
            Assert.That(_controller.RefreshIndexAfterIntegration(_secondAvatar.Descriptor), Is.False);
            Assert.That(_buildCount, Is.EqualTo(1));
        }

        [Test]
        public void MarkAvatarDirtyFromHierarchy_IgnoresUnchangedSelectedAvatar()
        {
            SetFirstAvatar();
            int revision = _session.PreviewRevision;

            _controller.MarkAvatarDirtyFromHierarchy();

            Assert.That(_buildCount, Is.EqualTo(1));
            Assert.That(_session.AvatarIndexDirty, Is.False);
            Assert.That(_session.PreviewRevision, Is.EqualTo(revision));
        }

        [Test]
        public void MarkAvatarDirtyFromHierarchy_DirtiesAfterSelectedAvatarMutation()
        {
            SetFirstAvatar();
            _firstAvatar.AddFork("ChangedHierarchy");

            _controller.MarkAvatarDirtyFromHierarchy();

            Assert.That(_session.AvatarIndexDirty, Is.True);
        }

        [Test]
        public void PreviewRebuildHierarchyNotification_DoesNotDirtySelectedAvatarIndex()
        {
            SetFirstAvatar();
            using (var preview = new PreviewSession())
            {
                preview.RebuildAvatar(_firstAvatar.Root);
                _controller.MarkAvatarDirtyFromHierarchy();

                Assert.That(_session.AvatarIndexDirty, Is.False);
            }
        }

        [Test]
        public void DirtyThenPreviewEquivalentEnsure_IncrementsPreviewRevisionOnce()
        {
            SetFirstAvatar();
            int revision = _session.PreviewRevision;
            _firstAvatar.AddFork("ChangedHierarchy");
            _controller.MarkAvatarDirtyFromHierarchy();

            _controller.EnsureAvatarIndexCurrent();
            _controller.EnsureAvatarIndexCurrent();

            Assert.That(_buildCount, Is.EqualTo(2));
            Assert.That(_session.PreviewRevision, Is.EqualTo(revision + 1));
        }

        [Test]
        public void SetDescriptor_ValidAvatarIncrementsPreviewRevisionOnce()
        {
            _firstAvatar = AvatarFixture.Create();
            int revision = _session.PreviewRevision;

            _controller.SetDescriptor(_firstAvatar.Descriptor);
            _controller.EnsureAvatarIndexCurrent();

            Assert.That(_buildCount, Is.EqualTo(1));
            Assert.That(_session.PreviewRevision, Is.EqualTo(revision + 1));
        }

        [Test]
        public void SetDescriptor_DifferentAvatarBuildsAndSelectsTheNewAvatar()
        {
            SetFirstAvatar();
            _secondAvatar = AvatarFixture.Create();
            int revision = _session.PreviewRevision;

            _controller.SetDescriptor(_secondAvatar.Descriptor);
            _controller.SetDescriptor(_secondAvatar.Descriptor);

            Assert.That(_buildCount, Is.EqualTo(2));
            Assert.That(_session.ActiveDescriptor, Is.SameAs(_secondAvatar.Descriptor));
            Assert.That(_session.ActiveAvatarRoot, Is.SameAs(_secondAvatar.Root));
            Assert.That(_session.PreviewRevision, Is.EqualTo(revision + 1));
        }

        private void SetFirstAvatar()
        {
            _firstAvatar = AvatarFixture.Create();
            _controller.SetDescriptor(_firstAvatar.Descriptor);
        }
    }
}
