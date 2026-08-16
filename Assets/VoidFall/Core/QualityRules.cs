using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    public enum QualityPresetId
    {
        Low,
        Balanced,
        High,
    }

    public struct QualityPreset
    {
        public QualityPresetId Id;
        public int Detail;
        public float RenderScale;
        public float DprCap;
        public float ParticleScale;
        public float FloaterScale;
        public bool DeathGhosts;
        public bool PlayerTrail;
        public bool ProjectileTrails;
        public bool PickupPulse;
    }

    public static class QualityRules
    {
        public const float MissFactor = 1.5f;
        public const float DownMissRate = 0.2f;
        public const float UpMissRate = 0.04f;
        public const float CriticalMissRate = 0.8f;
        public const float DownHoldSeconds = 0.75f;
        public const float CriticalHoldSeconds = 0.3f;
        public const float UpHoldSeconds = 8f;
        public const float DwellSeconds = 2.5f;
        public const float AbsoluteDownMs = 22f;
        public const float AbsoluteHoldSeconds = 2.5f;
        public const float SignalTauSeconds = 0.5f;
        public const int SettleFrames = 4;
        public const float SettleSeconds = 0.25f;
        public const float OscillationWindowSeconds = 20f;
        public const int OscillationLimit = 2;
        public const float CeilingSeconds = 90f;
        public const int CalibrationSamples = 32;
        public const int CalibrationWindow = 120;
        public const int CalibrationStride = 8;
        public const float CalibrationPercentile = 0.25f;
        public const float MinPeriodMs = 1000f / 250f;
        public const float MaxPeriodMs = 20f;
        public const float MaxDynamicPixels = 2_600_000f;
        public const float ReferenceDpi = 96f;

        public static readonly float[] DisplayPeriodsMs =
        {
            1000f / 240f,
            1000f / 165f,
            1000f / 144f,
            1000f / 120f,
            1000f / 100f,
            1000f / 90f,
            1000f / 75f,
            1000f / 60f,
        };

        public static QualityPreset Preset(QualityPresetId id)
        {
            switch (id)
            {
                case QualityPresetId.Low:
                    return new QualityPreset
                    {
                        Id = id,
                        Detail = 0,
                        RenderScale = 0.65f,
                        DprCap = 1f,
                        ParticleScale = 0.2f,
                        FloaterScale = 0.3f,
                        DeathGhosts = false,
                        PlayerTrail = false,
                        ProjectileTrails = false,
                        PickupPulse = false,
                    };
                case QualityPresetId.Balanced:
                    return new QualityPreset
                    {
                        Id = id,
                        Detail = 1,
                        RenderScale = 0.85f,
                        DprCap = 1.5f,
                        ParticleScale = 0.7f,
                        FloaterScale = 0.8f,
                        DeathGhosts = true,
                        PlayerTrail = true,
                        ProjectileTrails = true,
                        PickupPulse = true,
                    };
                default:
                    return new QualityPreset
                    {
                        Id = QualityPresetId.High,
                        Detail = 2,
                        RenderScale = 1f,
                        DprCap = 1.75f,
                        ParticleScale = 1f,
                        FloaterScale = 1f,
                        DeathGhosts = true,
                        PlayerTrail = true,
                        ProjectileTrails = true,
                        PickupPulse = true,
                    };
            }
        }

        public static QualityPresetId RecommendedInitialQuality(bool touchFirst, int viewportWidth)
        {
            return touchFirst || viewportWidth <= 700
                ? QualityPresetId.Balanced
                : QualityPresetId.High;
        }

        /// <summary>
        /// Maps Unity's physical backbuffer to the browser reference's
        /// CSS-pixel/DPR backing-store rule. Unity already reports physical
        /// pixels through Screen.width/height, so a DPR cap is applied as a
        /// multiplier against that existing backbuffer rather than counted
        /// twice.
        /// </summary>
        public static float EffectiveRenderScale(
            int viewportWidth,
            int viewportHeight,
            float dpi,
            QualityPreset preset)
        {
            if (preset.Id == QualityPresetId.High)
            {
                return 1f;
            }

            var width = Math.Max(1, viewportWidth);
            var height = Math.Max(1, viewportHeight);
            var dpr = dpi > 0 && !float.IsNaN(dpi) && !float.IsInfinity(dpi)
                ? Math.Max(1f, Math.Min(3f, dpi / ReferenceDpi))
                : 1f;
            var scale = preset.RenderScale;
            if (dpr > preset.DprCap) scale *= preset.DprCap / dpr;
            var pixels = width * (double)height * scale * scale;
            if (pixels > MaxDynamicPixels)
                scale *= (float)Math.Sqrt(MaxDynamicPixels / pixels);
            return Math.Max(0.5f, Math.Min(1f, scale));
        }

        public static int Index(QualityPresetId id) => (int)id;

        public static QualityPresetId FromIndex(int index)
        {
            return (QualityPresetId)Math.Max(0, Math.Min(2, index));
        }

        public static QualityPresetId FromName(string value)
        {
            if (value == "low") return QualityPresetId.Low;
            if (value == "balanced") return QualityPresetId.Balanced;
            return QualityPresetId.High;
        }
    }

    /// <summary>
    /// Source-aligned adaptive controller. It changes cosmetic tiers only after
    /// sustained frame pressure, uses display-relative misses, and ignores the
    /// cost of the tier change itself before sampling again.
    /// </summary>
    public sealed class AdaptiveQualityController
    {
        private int _index;
        private readonly int _floorIndex;
        private float _downHold;
        private float _upHold;
        private float _criticalHold;
        private float _absoluteHold;
        private float _dwell;
        private float _missRate;
        private float _smoothedMs = 1000f / 60f;
        private float _periodMs = 1000f / 60f;
        private bool _periodCalibrated;
        private readonly float[] _intervalRing = new float[QualityRules.CalibrationWindow];
        private readonly float[] _intervalScratch = new float[QualityRules.CalibrationWindow];
        private int _intervalCount;
        private int _intervalWrite;
        private int _sinceEstimate;
        private int _settleFrames;
        private float _settleSeconds;
        private float _clock;
        private float _enteredByUpgradeAt = -1;
        private readonly Dictionary<int, int> _failedUpgrades = new Dictionary<int, int>();
        private int _ceilingIndex = 2;
        private float _ceilingRemaining;

        public AdaptiveQualityController(QualityPresetId startId = QualityPresetId.High, int floorIndex = 0)
        {
            _floorIndex = Math.Max(0, Math.Min(2, floorIndex));
            _index = Math.Max(_floorIndex, QualityRules.Index(startId));
            BeginSession();
        }

        public QualityPresetId PresetId => QualityRules.FromIndex(_index);
        public QualityPreset CurrentPreset => QualityRules.Preset(PresetId);
        public int Index => _index;
        public float MissRate => _missRate;
        public float SmoothedMs => _smoothedMs;
        public float PeriodMs => _periodMs;
        public bool PeriodCalibrated => _periodCalibrated;

        public void SetTier(QualityPresetId id)
        {
            _index = Math.Max(_floorIndex, QualityRules.Index(id));
            BeginSession();
        }

        public void BeginSession()
        {
            _downHold = 0;
            _upHold = 0;
            _criticalHold = 0;
            _absoluteHold = 0;
            _dwell = QualityRules.DwellSeconds;
            _missRate = 0;
            _smoothedMs = _periodMs;
            _intervalCount = 0;
            _intervalWrite = 0;
            _sinceEstimate = 0;
            _periodCalibrated = false;
            _failedUpgrades.Clear();
            _ceilingIndex = 2;
            _ceilingRemaining = 0;
            _enteredByUpgradeAt = -1;
            BeginSettle();
        }

        public void BeginSettle()
        {
            _settleFrames = Math.Max(_settleFrames, QualityRules.SettleFrames);
            _settleSeconds = Math.Max(_settleSeconds, QualityRules.SettleSeconds);
        }

        public bool Update(float frameMs, float dtSeconds)
        {
            if (float.IsNaN(frameMs) || float.IsInfinity(frameMs) ||
                float.IsNaN(dtSeconds) || float.IsInfinity(dtSeconds)) return false;

            var dt = Math.Max(0, Math.Min(0.25f, dtSeconds));
            _clock += dt;
            _dwell = Math.Max(0, _dwell - dt);
            if (_ceilingRemaining > 0) _ceilingRemaining = Math.Max(0, _ceilingRemaining - dt);

            if (_settleFrames > 0 || _settleSeconds > 0)
            {
                _settleFrames = Math.Max(0, _settleFrames - 1);
                _settleSeconds = Math.Max(0, _settleSeconds - dt);
                return false;
            }

            Calibrate(frameMs);
            var blend = 1f - (float)Math.Exp(-dt / QualityRules.SignalTauSeconds);
            _smoothedMs += (frameMs - _smoothedMs) * blend;
            var missed = frameMs > _periodMs * QualityRules.MissFactor ? 1f : 0;
            _missRate += (missed - _missRate) * blend;
            if (_smoothedMs > QualityRules.AbsoluteDownMs) _absoluteHold += dt;
            else _absoluteHold = 0;

            var canStepDown = _index > _floorIndex;
            var overloaded = _missRate > QualityRules.DownMissRate ||
                _absoluteHold >= QualityRules.AbsoluteHoldSeconds;
            if (_missRate >= QualityRules.CriticalMissRate)
            {
                _criticalHold += dt;
                _upHold = 0;
                if (_criticalHold >= QualityRules.CriticalHoldSeconds && _dwell <= 0 && canStepDown)
                    return StepDown();
            }
            else
            {
                _criticalHold = 0;
            }

            if (overloaded)
            {
                _upHold = 0;
                _downHold += dt;
                if (_downHold >= QualityRules.DownHoldSeconds && _dwell <= 0 && canStepDown)
                    return StepDown();
                return false;
            }

            _downHold = 0;
            if (_missRate < QualityRules.UpMissRate && _smoothedMs < QualityRules.AbsoluteDownMs)
            {
                _upHold += dt;
                if (_upHold >= QualityRules.UpHoldSeconds && _dwell <= 0 && _index < EffectiveCeiling())
                    return StepUp();
            }
            else
            {
                _upHold = 0;
            }
            return false;
        }

        private void Calibrate(float frameMs)
        {
            if (_periodCalibrated || frameMs < QualityRules.MinPeriodMs) return;
            _intervalRing[_intervalWrite] = frameMs;
            _intervalWrite = (_intervalWrite + 1) % QualityRules.CalibrationWindow;
            if (_intervalCount < QualityRules.CalibrationWindow) _intervalCount++;
            _sinceEstimate++;
            if (_intervalCount < QualityRules.CalibrationSamples || _sinceEstimate < QualityRules.CalibrationStride) return;
            _sinceEstimate = 0;
            for (var index = 0; index < _intervalScratch.Length; index++)
                _intervalScratch[index] = float.PositiveInfinity;
            for (var index = 0; index < _intervalCount; index++) _intervalScratch[index] = _intervalRing[index];
            Array.Sort(_intervalScratch);
            var rank = Math.Min(_intervalCount - 1,
                (int)Math.Floor(QualityRules.CalibrationPercentile * _intervalCount));
            var estimate = _intervalScratch[rank];
            if (estimate > QualityRules.MaxPeriodMs) return;

            var best = estimate;
            var bestDistance = float.PositiveInfinity;
            foreach (var period in QualityRules.DisplayPeriodsMs)
            {
                var distance = Math.Abs(estimate - period) / period;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = period;
                }
            }
            _periodMs = bestDistance <= 0.08f ? best : estimate;
            _periodCalibrated = true;
        }

        private int EffectiveCeiling() => _ceilingRemaining > 0 ? _ceilingIndex : 2;

        private bool StepDown()
        {
            if (_enteredByUpgradeAt >= 0 && _clock - _enteredByUpgradeAt <= QualityRules.OscillationWindowSeconds)
            {
                var failures = _failedUpgrades.TryGetValue(_index, out var previous) ? previous + 1 : 1;
                _failedUpgrades[_index] = failures;
                if (failures >= QualityRules.OscillationLimit)
                {
                    _ceilingIndex = Math.Max(_floorIndex, _index - 1);
                    _ceilingRemaining = QualityRules.CeilingSeconds;
                }
            }
            _enteredByUpgradeAt = -1;
            _index--;
            _downHold = 0;
            _upHold = 0;
            _criticalHold = 0;
            _absoluteHold = 0;
            _dwell = QualityRules.DwellSeconds;
            BeginSettle();
            return true;
        }

        private bool StepUp()
        {
            _index++;
            _enteredByUpgradeAt = _clock;
            _downHold = 0;
            _upHold = 0;
            _criticalHold = 0;
            _absoluteHold = 0;
            _dwell = QualityRules.DwellSeconds;
            BeginSettle();
            return true;
        }
    }
}
