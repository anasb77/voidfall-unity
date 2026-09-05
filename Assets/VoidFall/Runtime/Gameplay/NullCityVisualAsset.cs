using System;
using UnityEngine;

namespace VoidFall.Runtime
{
    public sealed class NullCityVisualAsset : ScriptableObject
    {
        public const int CurrentSchema = 1;
        public const float AnimationFramesPerSecond = 8f;

        private static readonly string[] RequiredUnitIds =
        {
            "null-patrol",
            "null-enforcer",
            "null-sentinel",
            "null-crawler",
            "null-volatile",
            "null-gunship",
            "null-mech",
            "null-broodmother",
            "null-light-gunship",
            "null-interceptor",
            "null-marshal",
            "null-suppressor",
            "null-motherload",
        };

        [Serializable]
        private sealed class UnitVisual
        {
            [SerializeField] private string _id;
            [SerializeField] private Vector2 _worldSize;
            [SerializeField] private Sprite[] _frames;
            [SerializeField] private Sprite _hitFrame;
            [SerializeField] private Sprite[] _exposedFrames;
            [SerializeField] private Sprite[] _tractorFrames;

            public string Id => _id;
            public Vector2 WorldSize => _worldSize;
            public Sprite[] Frames => _frames;
            public Sprite HitFrame => _hitFrame;
            public Sprite[] ExposedFrames => _exposedFrames;
            public Sprite[] TractorFrames => _tractorFrames;
        }

        [SerializeField] private int _schema = CurrentSchema;
        [SerializeField] private UnitVisual[] _units;
        [SerializeField] private Sprite _transit;
        [SerializeField] private Sprite _hangarOpen;
        [SerializeField] private Sprite _hangarClosed;
        [SerializeField] private Sprite _traffic;
        [SerializeField] private Sprite _trafficLockdown;
        [SerializeField] private Sprite _lcdSurveillance;
        [SerializeField] private Sprite _lcdLockdown;
        [SerializeField] private Sprite[] _motherloadTractorWarningFrames;
        [SerializeField] private Sprite[] _marshalBracedFrames;

        public Sprite Transit => _transit;
        public Sprite HangarOpen => _hangarOpen;
        public Sprite HangarClosed => _hangarClosed;
        public Sprite Traffic => _traffic;
        public Sprite TrafficLockdown => _trafficLockdown;
        public Sprite LcdSurveillance => _lcdSurveillance;
        public Sprite LcdLockdown => _lcdLockdown;

        public bool OwnsSprite(Sprite sprite)
        {
            if (sprite == null) return false;
            if (sprite == _transit || sprite == _hangarOpen || sprite == _hangarClosed ||
                sprite == _traffic || sprite == _trafficLockdown || sprite == _lcdSurveillance || sprite == _lcdLockdown)
                return true;
            if (Contains(_motherloadTractorWarningFrames, sprite) || Contains(_marshalBracedFrames, sprite)) return true;
            if (_units == null) return false;
            for (var i = 0; i < _units.Length; i++)
            {
                var unit = _units[i];
                if (unit != null && (sprite == unit.HitFrame || Contains(unit.Frames, sprite) ||
                    Contains(unit.ExposedFrames, sprite) || Contains(unit.TractorFrames, sprite))) return true;
            }
            return false;
        }

        private static bool Contains(Sprite[] frames, Sprite sprite)
        {
            if (frames == null) return false;
            for (var i = 0; i < frames.Length; i++) if (frames[i] == sprite) return true;
            return false;
        }

        public Sprite UnitSprite(
            string id,
            float elapsed,
            bool hit = false,
            bool exposed = false,
            bool tractor = false)
        {
            var visual = FindUnit(id);
            if (visual == null) return null;
            if (hit && visual.HitFrame != null) return visual.HitFrame;
            if (exposed && HasFrames(visual.ExposedFrames))
                return FrameAt(visual.ExposedFrames, elapsed);
            if (tractor && HasFrames(visual.TractorFrames))
                return FrameAt(visual.TractorFrames, elapsed);
            return FrameAt(visual.Frames, elapsed);
        }

        public Vector2 UnitWorldSize(string id)
        {
            var visual = FindUnit(id);
            return visual != null ? visual.WorldSize : Vector2.zero;
        }

        public Sprite MotherloadTractorWarningSprite(float elapsed)
        {
            return FrameAt(_motherloadTractorWarningFrames, elapsed);
        }

        public Sprite MarshalBracedSprite(float elapsed)
        {
            return FrameAt(_marshalBracedFrames, elapsed);
        }

        public bool IsValid()
        {
            if (_schema != CurrentSchema ||
                _transit == null ||
                _hangarOpen == null ||
                _hangarClosed == null ||
                _traffic == null ||
                _trafficLockdown == null ||
                _lcdSurveillance == null ||
                _lcdLockdown == null ||
                !HasFrames(_motherloadTractorWarningFrames) ||
                !HasFrames(_marshalBracedFrames))
            {
                return false;
            }

            for (var index = 0; index < RequiredUnitIds.Length; index++)
            {
                var visual = FindUnit(RequiredUnitIds[index]);
                if (visual == null ||
                    visual.WorldSize.x <= 0f ||
                    visual.WorldSize.y <= 0f ||
                    visual.HitFrame == null ||
                    !HasFrames(visual.Frames))
                {
                    return false;
                }
            }

            var motherload = FindUnit("null-motherload");
            return motherload != null &&
                   HasFrames(motherload.ExposedFrames) &&
                   HasFrames(motherload.TractorFrames);
        }

        private UnitVisual FindUnit(string id)
        {
            if (string.IsNullOrEmpty(id) || _units == null) return null;
            for (var index = 0; index < _units.Length; index++)
            {
                var unit = _units[index];
                if (unit != null && string.Equals(unit.Id, id, StringComparison.Ordinal))
                    return unit;
            }
            return null;
        }

        private static Sprite FrameAt(Sprite[] frames, float elapsed)
        {
            if (!HasFrames(frames)) return null;
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed)) elapsed = 0f;
            var frame = Mathf.FloorToInt(Mathf.Max(0f, elapsed) * AnimationFramesPerSecond);
            return frames[frame % frames.Length];
        }

        private static bool HasFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length != 4) return false;
            for (var index = 0; index < frames.Length; index++)
                if (frames[index] == null) return false;
            return true;
        }
    }
}
