using System;
using System.Collections.Generic;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Editor.VRChat;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Support
{
    /// <summary>
    /// Immutable UI candidate list built once per avatar scan. Searching filters this
    /// snapshot; it is never rebuilt per OnGUI pass.
    /// </summary>
    public sealed class AvatarCandidateSnapshot
    {
        public enum BlendShapeCategory
        {
            Eye,
            Blink,
            Brow,
            Mouth,
            FaceOther,
            Hair,
            Body,
            Clothes,
            Other
        }

        public enum BlendShapeConflictStatus
        {
            Safe,
            Conflict,
            Warning
        }

        public sealed class BlendShapeCandidate
        {
            public BlendShapeCandidate(
                string rendererPath,
                string blendShapeName,
                string rendererName = null,
                int blendShapeIndex = -1,
                BlendShapeCategory category = BlendShapeCategory.Other,
                string classificationReason = "Other fallback",
                BlendShapeConflictStatus conflictStatus = BlendShapeConflictStatus.Safe,
                string conflictReason = null,
                string conflictSource = null,
                bool isSeparator = false)
            {
                RendererPath = rendererPath ?? string.Empty;
                BlendShapeName = blendShapeName ?? string.Empty;
                RendererName = rendererName ?? string.Empty;
                BlendShapeIndex = blendShapeIndex;
                Category = category;
                ClassificationReason = classificationReason ?? string.Empty;
                ConflictStatus = conflictStatus;
                ConflictReason = conflictReason ?? string.Empty;
                ConflictSource = conflictSource ?? string.Empty;
                IsSeparator = isSeparator;
            }

            public string RendererPath { get; }

            public string BlendShapeName { get; }

            public string RendererName { get; }

            public int BlendShapeIndex { get; }

            public BlendShapeCategory Category { get; }

            public string ClassificationReason { get; }

            public BlendShapeConflictStatus ConflictStatus { get; }

            public string ConflictReason { get; }

            public string ConflictSource { get; }

            public bool IsSeparator { get; }

            public string TopLevelCategory => Category == BlendShapeCategory.Hair ? "Hair"
                : Category == BlendShapeCategory.Body ? "Body"
                : Category == BlendShapeCategory.Clothes ? "Clothes"
                : Category == BlendShapeCategory.Other ? "Other"
                : "Face";

            public string DisplayLabel => RendererPath + " / " + BlendShapeName;

            public BlendShapeBinding ToBinding()
            {
                return new BlendShapeBinding(RendererPath, BlendShapeName);
            }

            public bool MatchesSearch(string query)
            {
                return Contains(BlendShapeName, query) || Contains(RendererPath, query)
                    || Contains(RendererName, query) || Contains(Category.ToString(), query)
                    || Contains(TopLevelCategory, query);
            }
        }

        public sealed class TransformCandidate
        {
            public TransformCandidate(string relativePath, string name = null, int depth = 0, string parentPath = null)
            {
                RelativePath = relativePath ?? string.Empty;
                Name = name ?? LeafName(RelativePath);
                Depth = depth;
                ParentPath = parentPath ?? string.Empty;
            }

            public string RelativePath { get; }

            public string Name { get; }

            public int Depth { get; }

            public string ParentPath { get; }

            public string DisplayLabel => string.IsNullOrEmpty(RelativePath)
                ? FaceMotionUiText.Get("avatarRoot")
                : new string(' ', Depth * 2) + Name;

            public bool MatchesSearch(string query)
            {
                return Contains(RelativePath, query) || Contains(Name, query);
            }

            private static string LeafName(string relativePath)
            {
                int slash = relativePath.LastIndexOf('/');
                return slash >= 0 ? relativePath.Substring(slash + 1) : relativePath;
            }
        }

        private readonly List<BlendShapeCandidate> _blendShapes = new List<BlendShapeCandidate>();
        private readonly List<TransformCandidate> _transforms = new List<TransformCandidate>();

        public int BlendShapeCount => _blendShapes.Count;

        public int TransformCount => _transforms.Count;

        public IReadOnlyList<BlendShapeCandidate> BlendShapes => _blendShapes;

        public static AvatarCandidateSnapshot Build(AvatarIndex index, VRCAvatarDescriptor descriptor = null)
        {
            var snapshot = new AvatarCandidateSnapshot();
            if (index == null)
            {
                return snapshot;
            }

            VrcBlendShapeConflictIndex conflicts = VrcBlendShapeConflictIndex.Build(descriptor);
            var blendShapes = new List<BlendShapeCandidate>(index.BlendShapeCount);
            for (int i = 0; i < index.BlendShapes.Count; i++)
            {
                var entry = index.BlendShapes[i];
                if (entry != null)
                {
                    if (!entry.HasVisibleDelta)
                    {
                        continue;
                    }

                    var binding = new BlendShapeBinding(entry.RendererPath, entry.BlendShapeName);
                    VrcBlendShapeConflict conflict = conflicts.Get(binding);
                    BlendShapeCategory category = Classify(entry.RendererPath, entry.RendererName, entry.BlendShapeName, conflicts, binding, out string reason);
                    BlendShapeConflictStatus status = conflict.IsConflict ? BlendShapeConflictStatus.Conflict
                        : conflict.IsWarning ? BlendShapeConflictStatus.Warning
                        : BlendShapeConflictStatus.Safe;
                    blendShapes.Add(new BlendShapeCandidate(
                        entry.RendererPath,
                        entry.BlendShapeName,
                        entry.RendererName,
                        entry.CurrentIndex,
                        category,
                        reason,
                        status,
                        conflict.Reason,
                        conflict.Source,
                        IsSeparatorName(entry.BlendShapeName)));
                }
            }

            blendShapes.Sort(
                (a, b) =>
                {
                    int byPath = string.CompareOrdinal(a.RendererPath, b.RendererPath);
                    return byPath != 0 ? byPath : string.CompareOrdinal(a.BlendShapeName, b.BlendShapeName);
                });
            snapshot._blendShapes.AddRange(blendShapes);

            var transforms = new List<TransformCandidate>(index.TransformCount);
            for (int i = 0; i < index.Transforms.Count; i++)
            {
                var entry = index.Transforms[i];
                if (entry != null)
                {
                    transforms.Add(new TransformCandidate(entry.RelativePath, entry.Name, entry.Depth, entry.ParentPath));
                }
            }

            transforms.Sort((a, b) => string.CompareOrdinal(a.RelativePath, b.RelativePath));
            snapshot._transforms.AddRange(transforms);
            return snapshot;
        }

        public IReadOnlyList<BlendShapeCandidate> FilterBlendShapes(string query)
        {
            return Filter(
                _blendShapes,
                candidate => candidate.MatchesSearch(query));
        }

        internal static bool IsSeparatorName(string name)
        {
            if (name == null)
            {
                return false;
            }

            string trimmed = name.Trim();
            if (trimmed.Length < 5)
            {
                return false;
            }

            // Strip the leading and trailing decorator runs; a heading like "===chimera===" or
            // "■■ Brow ■■" is enclosed by a strong run on both sides. "expression_smile" or
            // "Eye_Close" have no enclosing run and remain normal candidates.
            int leading = 0;
            while (leading < trimmed.Length && IsDecorator(trimmed[leading]))
            {
                leading++;
            }

            int trailing = 0;
            int last = trimmed.Length - 1;
            while (trailing < trimmed.Length - leading && IsDecorator(trimmed[last - trailing]))
            {
                trailing++;
            }

            int coreLength = trimmed.Length - leading - trailing;
            if (coreLength <= 0)
            {
                return true;
            }

            if (leading >= 2 && trailing >= 2)
            {
                return true;
            }

            return leading + trailing >= 6 && DecoratorFraction(trimmed) >= 50;
        }

        private static bool IsDecorator(char value)
        {
            if (value == '-' || value == '_' || value == '=' || value == '*' || value == '#' || value == '+'
                || value == '~' || value == '·' || value == '・' || value == '•')
            {
                return true;
            }

            // Box drawing, geometric/block decorations, en/em dashes, wave dashes, full-width decorations.
            return (value >= '\u2500' && value <= '\u257F')
                || (value >= '\u25A0' && value <= '\u25FF')
                || value == '\u2013' || value == '\u2014'
                || value == '\u301C' || value == '\uFF5E'
                || value == '\uFF0D' || value == '\uFF1D' || value == '\uFF3F' || value == '\uFF0A';
        }

        private static int DecoratorFraction(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (IsDecorator(text[i]))
                {
                    count++;
                }
            }

            return count * 100 / text.Length;
        }

        internal static BlendShapeCategory Classify(
            string rendererPath,
            string rendererName,
            string blendShapeName,
            VrcBlendShapeConflictIndex conflicts,
            BlendShapeBinding binding,
            out string reason)
        {
            if (conflicts.IsEyelid(binding))
            {
                reason = "VRChat Eyelids";
                return BlendShapeCategory.Blink;
            }

            if (conflicts.IsLipSync(binding))
            {
                reason = "VRChat LipSync";
                return BlendShapeCategory.Mouth;
            }

            string name = blendShapeName ?? string.Empty;
            string renderer = (rendererPath ?? string.Empty) + " " + (rendererName ?? string.Empty);

            const int Strong = 100;
            const int NameStrong = 90;
            const int NameMedium = 80;
            const int NameWeak = 65;
            const int RendererWeak = 55;
            const int ClassificationThreshold = 50;

            int blink = 0;
            int mouth = 0;
            int brow = 0;
            int eye = 0;
            int face = 0;
            int hair = 0;
            int body = 0;
            int clothes = 0;
            string blinkSignal = null;
            string mouthSignal = null;
            string browSignal = null;
            string eyeSignal = null;
            string faceSignal = null;
            string hairSignal = null;
            string bodySignal = null;
            string clothesSignal = null;

            // Layer 1: strong BlendShape-name signals. These always win over the weak
            // renderer/path signal so a Hair renderer never forces option/toggle/contour
            // shapes into Hair.
            if (ContainsAny(name, "blink", "wink", "eye_close", "eyeclose", "mabataki", "まばたき"))
            {
                blink = Strong;
                blinkSignal = "Name contains 'blink'/'wink'/'eye_close'";
            }

            if (ContainsAny(name, "mouth", "lip", "smile", "sad", "vowel", "tongue", "あ", "い", "う", "え", "お"))
            {
                mouth = Strong;
                mouthSignal = "Name contains 'mouth'/'smile'/'vowel'";
            }

            if (ContainsAny(name, "brow", "eyebrow", "mayu", "眉"))
            {
                brow = NameStrong;
                browSignal = "Name contains 'brow'/'眉'";
            }

            if (ContainsAny(name, "hair", "bang", "pony", "ponytail", "前髪", "後髪"))
            {
                hair = NameStrong;
                hairSignal = "Name contains 'hair'/'bangs'/'前髪'";
            }

            if (ContainsAny(name, "cloth", "shirt", "jacket", "skirt", "sleeve", "pants", "dress", "服", "衣装", "スカート", "袖"))
            {
                clothes = NameMedium;
                clothesSignal = "Name contains 'cloth'/'shirt'/'jacket'";
            }

            if (ContainsAny(name, "tummy", "belly", "腹部"))
            {
                body = NameMedium;
                bodySignal = "Name contains 'tummy'/'belly'";
            }

            if (ContainsAny(name, "eye", "pupil", "iris", "gaze"))
            {
                eye = NameMedium;
                eyeSignal = "Name contains 'eye'/'iris'";
            }

            if (ContainsAny(name, "face", "cheek", "nose", "contour", "輪郭", "頬", "鼻", "顔"))
            {
                face = NameWeak;
                faceSignal = "Name contains 'cheek'/'nose'/'contour'/'顔'";
            }

            // Layer 2: weak renderer/path signals. Alone (no contradicting name signal) they
            // still classify clearly-named renderers; they never override a name signal above.
            // option_/toggle_/accessory_ prefixed shapes in a Hair renderer therefore land in
            // Face Other or Other instead of being forced into Hair.
            if (ContainsAny(renderer, "hair", "bang", "ponytail", "前髪", "後髪", "髪") && RendererWeak > hair)
            {
                hair = RendererWeak;
                hairSignal = "Renderer path contains 'hair'/'髪'";
            }

            if (ContainsAny(renderer, "cloth", "clothes", "jacket", "skirt", "shirt", "dress", "outfit", "wear", "服", "衣装")
                && RendererWeak > clothes)
            {
                clothes = RendererWeak;
                clothesSignal = "Renderer path contains 'cloth'/'outfit'/'服'";
            }

            if (ContainsAny(renderer, "body", "skin", "torso", "chest", "breast") && RendererWeak > body)
            {
                body = RendererWeak;
                bodySignal = "Renderer path contains 'body'/'skin'";
            }

            if (ContainsAny(renderer, "face", "head") && 40 > face)
            {
                face = 40;
                faceSignal = "Renderer path contains 'face'/'head'";
            }

            // Deterministic tie-break order: Blink, Mouth, Brow, Eye, FaceOther, Hair, Body, Clothes.
            int bestScore = blink;
            BlendShapeCategory best = BlendShapeCategory.Blink;
            string bestSignal = blinkSignal;
            if (mouth > bestScore) { bestScore = mouth; best = BlendShapeCategory.Mouth; bestSignal = mouthSignal; }
            if (brow > bestScore) { bestScore = brow; best = BlendShapeCategory.Brow; bestSignal = browSignal; }
            if (eye > bestScore) { bestScore = eye; best = BlendShapeCategory.Eye; bestSignal = eyeSignal; }
            if (face > bestScore) { bestScore = face; best = BlendShapeCategory.FaceOther; bestSignal = faceSignal; }
            if (hair > bestScore) { bestScore = hair; best = BlendShapeCategory.Hair; bestSignal = hairSignal; }
            if (body > bestScore) { bestScore = body; best = BlendShapeCategory.Body; bestSignal = bodySignal; }
            if (clothes > bestScore) { bestScore = clothes; best = BlendShapeCategory.Clothes; bestSignal = clothesSignal; }

            if (bestScore < ClassificationThreshold)
            {
                reason = "Other fallback (score " + bestScore + " < " + ClassificationThreshold + ")";
                return BlendShapeCategory.Other;
            }

            reason = bestSignal + " (" + bestScore + ")";
            return best;
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (Contains(text, values[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<TransformCandidate> FilterTransforms(string query)
        {
            return Filter(
                _transforms,
                candidate => candidate.MatchesSearch(query));
        }

        private static bool Contains(string text, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            return !string.IsNullOrEmpty(text)
                && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<T> Filter<T>(IReadOnlyList<T> source, Func<T, bool> predicate)
        {
            var result = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                if (predicate(source[i]))
                {
                    result.Add(source[i]);
                }
            }

            return result;
        }
    }
}
