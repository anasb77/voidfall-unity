using System;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private int _lifeStealKillProgress, _scavengerScrapProgress;
        private float _shieldCapacity = SurvivalSupportRules.InitialShieldCapacity;
        private Image _playerShieldTrack, _playerShieldFill, _vitalsBacking;
        private Text _playerShieldLabel, _playerShieldValue;
        private float _vitalsLayoutHealth = -1, _vitalsLayoutWidth = -1, _lastShieldDisplay = -1;

        private void ResetSurvivalSupports()
        {
            _lifeStealKillProgress = _scavengerScrapProgress = 0;
            _shieldCapacity = SurvivalSupportRules.InitialShieldCapacity;
            _vitalsLayoutHealth = _vitalsLayoutWidth = _lastShieldDisplay = -1;
        }

        private void RegisterLifeStealKill(string sourceId, int instanceId)
        {
            var rank = SupportRank("lifeSteal");
            if (rank <= 0 || _gameSim.Player.Health <= 0) return;
            if (++_lifeStealKillProgress < SurvivalSupportRules.KillThreshold) return;
            _lifeStealKillProgress -= SurvivalSupportRules.KillThreshold;
            var before = _gameSim.Player.Health;
            var requested = SurvivalSupportRules.LifeStealHealing(rank);
            _gameSim.Player.Health = Mathf.Min(_gameSim.Player.MaxHealth, before + requested);
            RecordRunHistory("support_proc", "lifeSteal", _gameSim.Player.Health > before ? "healed" : "full_health",
                sourceId: sourceId, relatedInstanceId: instanceId, amount: _gameSim.Player.Health - before,
                hp: _gameSim.Player.Health, maxHp: _gameSim.Player.MaxHealth,
                detail: JsonUtility.ToJson(new SurvivalSupportOutcome { rank = rank, requested = requested,
                    threshold = SurvivalSupportRules.KillThreshold, remainder = _lifeStealKillProgress }));
        }

        private void RegisterScavengerScraps(int value)
        {
            var rank = SupportRank("scavenger");
            if (rank <= 0 || value <= 0 || _gameSim.Player.Health <= 0) return;
            _scavengerScrapProgress += value;
            var grants = _scavengerScrapProgress / SurvivalSupportRules.ScrapThreshold;
            if (grants <= 0) return;
            _scavengerScrapProgress %= SurvivalSupportRules.ScrapThreshold;
            var before = _dealerShield;
            // Process each threshold separately so a consolidated pickup does not
            // masquerade as one larger grant and increase the shield capacity.
            for (var i = 0; i < grants; i++)
                SurvivalSupportRules.GrantShield(ref _dealerShield, ref _shieldCapacity, SurvivalSupportRules.ScrapShieldGrant);
            RecordRunHistory("support_proc", "scavenger", _dealerShield > before ? "shield_granted" : "shield_full",
                sourceId: "part", amount: _dealerShield - before,
                detail: JsonUtility.ToJson(new SurvivalSupportOutcome { rank = rank,
                    requested = grants * SurvivalSupportRules.ScrapShieldGrant,
                    threshold = SurvivalSupportRules.ScrapThreshold, remainder = _scavengerScrapProgress,
                    shield = _dealerShield, shieldCapacity = _shieldCapacity }));
        }

        private void GrantPlayerShield(float amount, string sourceId, float minimumCapacity = 0)
        {
            var granted = SurvivalSupportRules.GrantShield(ref _dealerShield, ref _shieldCapacity, amount, minimumCapacity);
            RecordRunHistory("player_shield_granted", sourceId: sourceId, amount: granted,
                detail: JsonUtility.ToJson(new SurvivalSupportOutcome { requested = amount,
                    shield = _dealerShield, shieldCapacity = _shieldCapacity }));
        }

        [Serializable] private sealed class SurvivalSupportOutcome
        {
            public int rank, threshold, remainder;
            public float requested, shield, shieldCapacity;
        }

        private void SetupSurvivalHud()
        {
            _vitalsBacking = CreateHudImage(_canvas.transform, "Vitals Backing");
            _vitalsBacking.sprite = ProceduralSpriteFactory.Square();
            _vitalsBacking.color = new Color(.02f, .03f, .06f, .88f);
            _vitalsBacking.transform.SetSiblingIndex(_healthBarBackground.transform.GetSiblingIndex());
            _playerShieldTrack = CreateHudImage(_canvas.transform, "Player Shield Track");
            _playerShieldTrack.sprite = ProceduralSpriteFactory.Square();
            _playerShieldTrack.color = new Color(.08f, .14f, .18f, .96f);
            _playerShieldFill = CreateHudImage(_canvas.transform, "Player Shield Fill");
            _playerShieldFill.sprite = ProceduralSpriteFactory.Square();
            _playerShieldFill.type = Image.Type.Filled;
            _playerShieldFill.fillMethod = Image.FillMethod.Horizontal;
            _playerShieldFill.fillOrigin = 0;
            _playerShieldFill.color = new Color(.64f, .89f, .92f);
            _playerShieldLabel = ApprovedLabel("Player Shield Label", "SHIELD");
            _playerShieldValue = ApprovedLabel("Player Shield Value", "0");
            foreach (var graphic in new Graphic[] { _vitalsBacking, _playerShieldTrack, _playerShieldFill, _playerShieldLabel, _playerShieldValue })
            { graphic.enabled = true; graphic.raycastTarget = false; }
            _vitalsLayoutHealth = _vitalsLayoutWidth = -1;
            UpdateSurvivalHud();
        }

        private void UpdateSurvivalHud()
        {
            if (_playerShieldTrack == null) return;
            var hp = _gameSim.Player.MaxHealth;
            var canvasWidth = ((RectTransform)_canvas.transform).rect.width;
            if (!Mathf.Approximately(hp, _vitalsLayoutHealth) || !Mathf.Approximately(canvasWidth, _vitalsLayoutWidth))
            {
                _vitalsLayoutHealth = hp; _vitalsLayoutWidth = canvasWidth;
                var tl = new Vector2(0, 1);
                var width = SurvivalSupportRules.HealthBarWidth(hp, 37);
                var shieldWidth = width * .88f;
                foreach (var bar in new[] { _healthBarBackground, _healthBarGhost, _healthBarFill })
                    ApprovedRect(bar.rectTransform, tl, 1.8f, -3.37f, width, 1.2f);
                ApprovedRect(_playerShieldTrack.rectTransform, tl, 1.8f, -2.27f, shieldWidth, .78f);
                ApprovedRect(_playerShieldFill.rectTransform, tl, 1.8f, -2.27f, shieldWidth, .78f);
                var labelX = 1.8f + width + 1.1f;
                ApprovedText(_playerShieldLabel, tl, labelX, -2.27f, 4.4f, .85f, .69f);
                ApprovedText(_playerShieldValue, tl, labelX + 4.4f, -2.12f, 6.6f, 1.2f, 1.06f, true);
                ApprovedText(_healthLabelText, tl, labelX, -3.4f, 4.4f, 1.2f, .92f);
                ApprovedText(_healthValueText, tl, labelX + 4.4f, -3.37f, 6.6f, 1.2f, 1.06f, true);
                _playerShieldLabel.alignment = _healthLabelText.alignment = TextAnchor.UpperLeft;
                _playerShieldValue.alignment = _healthValueText.alignment = TextAnchor.UpperRight;
                _playerShieldLabel.color = _playerShieldValue.color = new Color(.7f, .92f, .95f);
                _healthIcon.enabled = false;
                ApprovedRect(_vitalsBacking.rectTransform, tl, 1.5f, -1.9f, width + 12.8f, 2.8f);
            }
            _playerShieldFill.fillAmount = _shieldCapacity > 0 ? Mathf.Clamp01(_dealerShield / _shieldCapacity) : 0;
            if (!Mathf.Approximately(_lastShieldDisplay, _dealerShield))
            {
                _lastShieldDisplay = _dealerShield;
                _playerShieldValue.text = Mathf.CeilToInt(Mathf.Max(0, _dealerShield)).ToString();
            }
        }
    }
}
