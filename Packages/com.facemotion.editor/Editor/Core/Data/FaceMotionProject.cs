using System.Collections.Generic;
using FaceMotion.Timeline;
using FaceMotion.Versioning;
using UnityEngine;

namespace FaceMotion.Data
{
    /// <summary>
    /// Root ScriptableObject of a persisted FaceMotion project. Holds the animation list
    /// and the project-level generation registry, plus the current schema version.
    /// Avatar Mapping Profiles are intentionally not defined in Phase A; the container
    /// remains extensible by adding an additive serialized list in a later phase.
    /// </summary>
    public sealed class FaceMotionProject : ScriptableObject
    {
        [SerializeField] private string _projectId;
        [SerializeField] private int _schemaVersion;
        [SerializeField] private List<FaceMotionAnimationData> _animations = new List<FaceMotionAnimationData>();
        [SerializeField] private List<GenerationRecord> _generations = new List<GenerationRecord>();

        public string ProjectId => _projectId;

        public int SchemaVersion => _schemaVersion;

        public IReadOnlyList<FaceMotionAnimationData> Animations => _animations;

        public IReadOnlyList<GenerationRecord> Generations => _generations;

        /// <summary>
        /// Creates a detached project with a new ID, the current schema version, and empty
        /// collections. This is the single entry point for authoring a new project.
        /// </summary>
        public static FaceMotionProject CreateNew()
        {
            var project = ScriptableObject.CreateInstance<FaceMotionProject>();
            project._projectId = StableId.New();
            project._schemaVersion = FaceMotionVersions.ProjectSchemaVersion;
            project._animations = new List<FaceMotionAnimationData>();
            project._generations = new List<GenerationRecord>();
            return project;
        }

        /// <summary>
        /// Creates a detached deep copy that preserves every ID. Used for internal
        /// transactions and migration; user-facing duplicates use the Duplicate methods.
        /// </summary>
        public static FaceMotionProject CreateClone(FaceMotionProject source)
        {
            if (source == null)
            {
                throw new System.ArgumentNullException(nameof(source));
            }

            var clone = ScriptableObject.CreateInstance<FaceMotionProject>();
            clone._projectId = source._projectId;
            clone._schemaVersion = source._schemaVersion;
            var animations = source._animations ?? new List<FaceMotionAnimationData>();
            clone._animations = new List<FaceMotionAnimationData>(animations.Count);
            for (int i = 0; i < animations.Count; i++)
            {
                clone._animations.Add(animations[i] == null ? null : animations[i].Clone());
            }

            var generations = source._generations ?? new List<GenerationRecord>();
            clone._generations = new List<GenerationRecord>(generations.Count);
            for (int i = 0; i < generations.Count; i++)
            {
                clone._generations.Add(generations[i] == null ? null : generations[i].Clone());
            }

            return clone;
        }

        public void AddAnimation(FaceMotionAnimationData animation)
        {
            if (animation == null)
            {
                throw new System.ArgumentNullException(nameof(animation));
            }

            _animations.Add(animation);
        }

        public bool RemoveAnimation(string animationId)
        {
            int index = FindAnimationIndex(animationId);
            if (index < 0)
            {
                return false;
            }

            _animations.RemoveAt(index);
            return true;
        }

        public bool TryGetAnimation(string animationId, out FaceMotionAnimationData animation)
        {
            int index = FindAnimationIndex(animationId);
            animation = index >= 0 ? _animations[index] : null;
            return index >= 0 && animation != null;
        }

        public void AddGenerationRecord(GenerationRecord record)
        {
            if (record == null)
            {
                throw new System.ArgumentNullException(nameof(record));
            }

            _generations.Add(record);
        }

        /// <summary>
        /// User-facing animation duplicate with independent provenance. The duplicate gets a
        /// fresh animation id and fresh track and key ids. Manual keys stay manual.
        /// Generated keys stay generated and are re-bound to fresh generation records that
        /// mirror the source animation's records (new generation ids, same preset, generator
        /// type, algorithm version, and settings). The original records are never touched, so
        /// the two animations never share a generation id. Returns null when the animation
        /// does not exist.
        /// </summary>
        public FaceMotionAnimationData DuplicateAnimation(string animationId)
        {
            int index = FindAnimationIndex(animationId);
            if (index < 0 || _animations[index] == null)
            {
                return null;
            }

            var source = _animations[index];

            var remap = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var addedRecords = new List<GenerationRecord>();
            for (int i = 0; i < _generations.Count; i++)
            {
                var record = _generations[i];
                if (record == null
                    || !string.Equals(record.AnimationId, animationId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                var copy = GenerationRecord.CreateDuplicate(record);
                remap[record.GenerationId] = copy.GenerationId;
                addedRecords.Add(copy);
            }

            var duplicate = source.Duplicate(remap);
            for (int i = 0; i < addedRecords.Count; i++)
            {
                addedRecords[i].SetAnimationIdForDuplicate(duplicate.AnimationId);
            }

            _animations.Add(duplicate);
            for (int i = 0; i < addedRecords.Count; i++)
            {
                _generations.Add(addedRecords[i]);
            }

            return duplicate;
        }

        public bool RemoveGenerationRecord(string generationId)
        {
            for (int i = 0; i < _generations.Count; i++)
            {
                if (_generations[i] != null
                    && System.String.Equals(_generations[i].GenerationId, generationId, System.StringComparison.Ordinal))
                {
                    _generations.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public bool TryGetGeneration(string generationId, out GenerationRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(generationId))
            {
                return false;
            }

            for (int i = 0; i < _generations.Count; i++)
            {
                if (_generations[i] != null
                    && System.String.Equals(_generations[i].GenerationId, generationId, System.StringComparison.Ordinal))
                {
                    record = _generations[i];
                    return true;
                }
            }

            return false;
        }

        public bool HasGeneration(string generationId)
        {
            return TryGetGeneration(generationId, out _);
        }

        public void ClearGenerationRecords()
        {
            _generations.Clear();
        }

        /// <summary>
        /// Applies only safe, mechanical repairs. Never fabricates user-facing values such
        /// as duration or frame rate, and never resolves kind/payload mismatches.
        /// </summary>
        internal void NormalizeStructure()
        {
            if (_animations == null)
            {
                _animations = new List<FaceMotionAnimationData>();
            }

            RemoveNulls(_animations);

            for (int i = 0; i < _animations.Count; i++)
            {
                _animations[i].NormalizeStructure();
            }

            if (_generations == null)
            {
                _generations = new List<GenerationRecord>();
            }

            RemoveNulls(_generations);
            for (int i = 0; i < _generations.Count; i++)
            {
                _generations[i].NormalizeBuiltinPresetIds();
            }
        }

        internal void SetSchemaVersionForMigration(int version)
        {
            _schemaVersion = version;
        }

        internal void SetProjectIdForMigration(string id)
        {
            _projectId = id ?? string.Empty;
        }

        internal static void RemoveNulls<T>(List<T> list) where T : class
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                {
                    list.RemoveAt(i);
                }
            }
        }

        private int FindAnimationIndex(string animationId)
        {
            for (int i = 0; i < _animations.Count; i++)
            {
                if (_animations[i] != null
                    && System.String.Equals(_animations[i].AnimationId, animationId, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
