using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        // Browser study 02 at 60%, then the owner's relative +10% revision.
        private const float ApprovedHudScale = .66f;
        private Font _approvedHudFont, _approvedHudBold;
        private Text _approvedScoreLabel, _approvedArsenalLabel, _approvedPassiveLabel, _approvedManualLabel;
        private Image _approvedLevelFrame, _approvedTooltip;
        private Text _approvedTooltipText;
        private readonly Dictionary<string, Texture2D> _approvedIcons = new Dictionary<string, Texture2D>();
        private readonly List<ApprovedHudSlot> _approvedSlots = new List<ApprovedHudSlot>();
        private readonly Image[] _approvedEmptySlots = new Image[5];
        private Image _approvedManualSlot;
        private RawImage _approvedManualIcon;
        private Text _approvedManualRank;
        private ApprovedHudSlot _approvedSecondWind;
        private int _approvedCooldownSeconds = -1;
        private float _approvedLayoutWidth = -1;

        private void SetupApprovedHud()
        {
            _approvedHudFont = Resources.Load<Font>("VoidFall/ApprovedHud/ChakraPetch-Regular");
            _approvedHudBold = Resources.Load<Font>("VoidFall/ApprovedHud/ChakraPetch-Bold");
            if (_approvedHudFont == null || _approvedHudBold == null)
                throw new InvalidOperationException("Approved HUD fonts missing from player resources.");
            _healthPanel.color = _clockPanel.color = _metricsPanel.color = Color.clear;
            _clockPanel.GetComponentInChildren<LegacyLevelBadge>().enabled = false;
            _healthBarFill.color = _xpBarFill.color = Color.white;
            _healthBarFill.GetComponent<LegacyHudGradient>().Configure(
                ParseColor("#f43f5e", Color.red), ParseColor("#fdba74", Color.white));
            _xpBarFill.GetComponent<LegacyHudGradient>().Configure(
                ParseColor("#10b981", Color.green), ParseColor("#bef264", Color.white));
            _approvedLevelFrame = CreateHudImage(_canvas.transform, "Approved Level Badge");
            _approvedLevelFrame.sprite = UISprites.Rounded(2, new Color(.04f,.17f,.12f,.6f),
                new Color(.04f,.17f,.12f,.6f), new Color(.43f,.91f,.72f,.3f));
            _approvedLevelFrame.type = Image.Type.Sliced;
            _approvedLevelFrame.enabled = true;
            _approvedLevelFrame.transform.SetSiblingIndex(_levelText.transform.GetSiblingIndex());
            _approvedScoreLabel = ApprovedLabel("Score Heading", "S C O R E");
            _approvedArsenalLabel = ApprovedLabel("Arsenal Heading", "ARSENAL");
            _approvedPassiveLabel = ApprovedLabel("Passive Heading", "PASSIVES");
            _approvedManualLabel = ApprovedLabel("Legendary Heading", "LEGENDARY");
            _healthIcon.texture = ApprovedIcon("heart"); _healthIcon.uvRect = new Rect(0,0,1,1);
            for (var i = 0; i < 2; i++)
            { _metricIcons[i].texture = ApprovedIcon(i == 0 ? "skull" : "scrap"); _metricIcons[i].uvRect = new Rect(0,0,1,1); }
            _metricIcons[2].enabled = false;
            foreach (var divider in _metricDividers) if (divider != null) divider.enabled = false;
            foreach (var image in _weaponChipBackgrounds) PrepareApprovedSlot(image);
            foreach (var image in _supportChipBackgrounds) PrepareApprovedSlot(image);
            foreach (var image in _lateChipBackgrounds) PrepareApprovedSlot(image);
            for (var i = 0; i < 5; i++)
            {
                CreateBuildChipView(_canvas.transform, "Empty Arsenal Slot " + i, Vector2.zero,
                    Vector2.zero, Vector2.one, out var bg, out var bar, out var icon, out var name, out var rank);
                _approvedEmptySlots[i] = bg; PrepareApprovedSlot(bg);
                bar.enabled = name.enabled = false;
                icon.texture = ApprovedIcon(i == 4 ? "lock" : "plus"); icon.uvRect = new Rect(0,0,1,1);
                icon.color = new Color(.5f,.57f,.67f,.6f); icon.enabled = true;
                rank.text = i == 4 ? "LOCKED" : "EMPTY"; rank.enabled = true;
            }
            CreateBuildChipView(_canvas.transform, "Manual Legendary Slot", Vector2.zero,
                Vector2.zero, Vector2.one, out _approvedManualSlot, out var manualBar,
                out _approvedManualIcon, out var manualName, out _approvedManualRank);
            PrepareApprovedSlot(_approvedManualSlot); manualName.enabled = false;
            _approvedTooltip = CreateHudImage(_canvas.transform, "Build Slot Details");
            _approvedTooltip.sprite = UISprites.Rounded(5, new Color(.03f,.06f,.10f,.98f),
                new Color(.03f,.06f,.10f,.98f), new Color(.33f,.5f,.55f,.6f));
            _approvedTooltip.type = Image.Type.Sliced;
            _approvedTooltip.enabled = true;
            _approvedTooltipText = ApprovedLabel("Slot Description", "");
            _approvedTooltipText.transform.SetParent(_approvedTooltip.transform, false);
            _approvedTooltipText.alignment = TextAnchor.UpperLeft;
            _approvedTooltipText.supportRichText = true;
            _approvedTooltip.gameObject.SetActive(false);
            foreach (var text in new[] { _timeText, _metricValues[2] })
            {
                var glow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
                glow.effectColor = new Color(.13f,.83f,.93f,.35f); glow.effectDistance = new Vector2(0,-1);
            }
            UpdatePressureHud();
            LayoutApprovedHud(); RefreshApprovedBuildHud();
        }

        private Text ApprovedLabel(string name, string value)
        {
            var text = CreateText(_canvas.transform, Vector2.zero, Vector2.zero, 10, new Color(.5f,.59f,.67f));
            text.name = name; text.text = value; text.font = _approvedHudFont; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.resizeTextForBestFit = false;
            return text;
        }

        private Texture2D ApprovedIcon(string id)
        {
            if (!_approvedIcons.TryGetValue(id, out var texture))
            { texture = Resources.Load<Texture2D>("VoidFall/ApprovedHud/Icon-" + id); _approvedIcons[id] = texture; }
            return texture;
        }

        private void PrepareApprovedSlot(Image image)
        {
            image.sprite = UISprites.Rounded(5, new Color(.063f,.106f,.173f,.91f),
                new Color(.027f,.043f,.098f,.96f), new Color(.25f,.32f,.41f,.6f));
            image.type = Image.Type.Sliced; image.color = Color.white; image.raycastTarget = true;
            foreach (var child in new[] { "Chip Border", "Rank Background" })
                image.transform.Find(child).gameObject.SetActive(false);
            var slot = image.gameObject.AddComponent<ApprovedHudSlot>(); slot.Initialize(_approvedHudFont);
            slot.Inspect = ShowApprovedSlot; slot.Dismiss = HideApprovedSlot;
            _approvedSlots.Add(slot);
        }

        private float ApprovedUnit => ((RectTransform)_canvas.transform).rect.width * .01f * ApprovedHudScale;
        private void ApprovedRect(RectTransform r, Vector2 anchor, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.localScale = Vector3.one;
            r.anchoredPosition = new Vector2(x,y) * ApprovedUnit;
            r.sizeDelta = new Vector2(w,h) * ApprovedUnit;
        }
        private void ApprovedText(Text text, Vector2 anchor, float x,float y,float w,float h,float font, bool bold = false)
        {
            ApprovedRect(text.rectTransform, anchor,x,y,w,h);
            text.font = bold ? _approvedHudBold : _approvedHudFont; text.fontStyle = FontStyle.Normal;
            text.resizeTextForBestFit = false; text.fontSize = Mathf.Max(7, Mathf.RoundToInt(font * ApprovedUnit));
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void LayoutApprovedHud()
        {
            _ui?.LayoutMuteControl();
            if (_approvedHudFont == null) return;
            var width = ((RectTransform)_canvas.transform).rect.width;
            if (Mathf.Approximately(width, _approvedLayoutWidth)) return;
            _approvedLayoutWidth = width;
            var tl = new Vector2(0,1); var tc = new Vector2(.5f,1); var tr = Vector2.one;
            foreach (var xp in new[] { _xpBarBackground, _xpBarFill })
            { xp.rectTransform.offsetMin = new Vector2(0, -.5f*ApprovedUnit); xp.rectTransform.offsetMax = Vector2.zero; }
            ApprovedRect(_healthIcon.rectTransform,tl,1.8f,-1.75f,1.1f,1.1f);
            ApprovedText(_healthLabelText,tl,3.35f,-1.75f,4,1.2f,.92f);
            _healthLabelText.alignment = TextAnchor.UpperLeft;
            ApprovedText(_healthValueText,tl,10.8f,-1.75f,10,1.3f,1.06f,true);
            _healthValueText.alignment = TextAnchor.UpperRight;
            foreach(var hp in new[] { _healthBarBackground,_healthBarGhost,_healthBarFill })
                ApprovedRect(hp.rectTransform,tl,1.8f,-3.37f,19,1.2f);
            ApprovedText(_timeText,tc,0,-1.2f,20,3.71f,3.22f,true);
            _timeText.alignment = TextAnchor.UpperCenter;
            ApprovedRect(_approvedLevelFrame.rectTransform,tc,0,-5.35f,5.5f,1.7f);
            ApprovedText(_levelText,tc,0,-5.35f,5.5f,1.7f,1f);
            _levelText.alignment = TextAnchor.MiddleCenter;
            _pressureText.transform.SetParent(_canvas.transform, false);
            ApprovedText(_pressureText,tc,0,-7.7f,20,1.4f,.85f);
            _pressureText.color = new Color(.68f,.72f,.73f);
            ApprovedText(_objectiveText,tl,1.8f,-6.2f,49,4.6f,1.45f,true);
            _objectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _objectiveText.alignment = TextAnchor.UpperLeft;
            _objectiveText.color = new Color(.90f,.96f,1f);
            var objectiveShadow = _objectiveText.GetComponent<Shadow>() ?? _objectiveText.gameObject.AddComponent<Shadow>();
            objectiveShadow.effectColor = new Color(0,0,0,.85f); objectiveShadow.effectDistance = new Vector2(1,-1);
            ApprovedText(_approvedScoreLabel,tr,-6.1f,-1.7f,18,1f,.76f);
            _approvedScoreLabel.alignment = TextAnchor.UpperRight;
            ApprovedText(_metricValues[2],tr,-6.1f,-2.65f,24,3,2.4f,true);
            _metricValues[2].alignment = TextAnchor.UpperRight;
            _metricValues[2].color = ParseColor("#a5f3fc",Color.cyan);
            for(var i=0;i<2;i++)
            {
                var right = 6.1f + (1-i)*7;
                ApprovedRect(_metricIcons[i].rectTransform,tr,-right-5.5f,-6.05f,1,1);
                ApprovedText(_metricValues[i],tr,-right,-5.9f,5.1f,1.4f,1);
                _metricValues[i].alignment = TextAnchor.UpperRight;
                _metricValues[i].color = _metricIcons[i].color = i==0 ? new Color(.99f,.8f,.83f,.8f) : new Color(.99f,.85f,.58f);
            }
            ApprovedRect((RectTransform)_pauseButton.transform,tr,-1.5f,-1.7f,3.4f,3.4f);
            _pauseButtonIcon.rectTransform.sizeDelta = Vector2.one * 1.6f*ApprovedUnit;
            _pauseButtonText.rectTransform.sizeDelta = Vector2.one * 3.4f*ApprovedUnit;
            LayoutApprovedMetrics();
            RefreshApprovedBuildHud();
        }

        private void LayoutApprovedMetrics()
        {
            if (_approvedHudFont == null) return;
            var right = 6.1f;
            for (var i = 1; i >= 0; i--)
            {
                var width = Mathf.Max(1, _metricValues[i].preferredWidth / ApprovedUnit);
                ApprovedRect(_metricValues[i].rectTransform, Vector2.one, -right, -5.9f, width, 1.4f);
                ApprovedRect(_metricIcons[i].rectTransform, Vector2.one, -right-width-.38f, -6.05f, 1, 1);
                right += width + 2.08f;
            }
        }

        private void StyleApprovedSlot(Image bg, RawImage icon, Text rankText, Image accentBar,
            Vector2 anchor, float x,float y,float w,float h, string id,string title,string detail,int rank,int max,Color accent,bool empty=false)
        {
            bg.gameObject.SetActive(true); bg.enabled = true;
            ApprovedRect(bg.rectTransform,anchor,x,y,w,h);
            bg.color = empty ? new Color(1,1,1,.55f) : Color.white;
            if (accentBar != null)
            {
                accentBar.enabled = !empty; accentBar.color = accent;
                ApprovedRect(accentBar.rectTransform,new Vector2(.5f,1),0,0,w*.64f,.16f);
            }
            var key = ApprovedIconKey(id);
            if (!string.IsNullOrEmpty(key)) { icon.texture = ApprovedIcon(key); icon.uvRect = new Rect(0,0,1,1); }
            icon.enabled = true; icon.color = accent;
            ApprovedRect(icon.rectTransform,new Vector2(.5f,.5f),0,.25f,w*.57f,w*.57f);
            ApprovedText(rankText, new Vector2(1,0),-.35f,.25f,empty?w-.4f:1.4f,.95f,empty?.5f:.7f);
            rankText.alignment = empty ? TextAnchor.LowerCenter : TextAnchor.LowerRight;
            rankText.color = accent; rankText.enabled = true;
            if (!empty) rankText.text = rank <= 6 ? new[] { "", "I", "II", "III", "IV", "V", "VI" }[Mathf.Max(0,rank)] : rank.ToString();
            bg.GetComponent<ApprovedHudSlot>().Bind(title, detail,rank,max,accent,w*ApprovedUnit,ApprovedUnit/10.56f);
        }

        private static string ApprovedIconKey(string id)
        {
            if (id.StartsWith("split-",StringComparison.Ordinal)) return "split";
            switch(id)
            {
                case "scattergun": return "scatter"; case "railgun": return "rail";
                case "phaseRounds": return "phase"; case "giantSlayer": return "giant"; case "secondWind": return "wind";
                case "collector": return "magnet"; case "calibration": case "output": return "power";
                case "regenerator": return "regen"; case "plating": case "frame": return "armor";
                case "mobility": case "adrenal": case "projectileSpeed": return "speed";
                case "cycling": case "cooling": return "cool"; case "amplifier": return "radius";
                case "optics": return "crit"; case "soundBlade": return "sound";
                case "chargedRifle": return "rail";
                case "pistol": case "clock": case "blades": case "lock": case "plus": return id;
                default: return null; // Preserve native art for content absent from the browser study.
            }
        }

        private void RefreshApprovedBuildHud()
        {
            if (_approvedHudFont == null || _upgradeProgress == null) return;
            var owned=0; var muted = new Color(.5f,.57f,.67f,.65f);
            for(var i=0;i<_weaponChipBackgrounds.Length;i++)
            {
                var rank=_upgradeProgress.WeaponRanks[i]; var active=rank>0;
                _weaponChipBackgrounds[i].gameObject.SetActive(active);
                _weaponChipNames[i].enabled=false; if(!active) continue;
                var weapon=ContentCatalog.Weapons[i]; var evolved=_upgradeProgress.Evolved[i];
                var accent=ParseColor(WeaponDisplayAccent(i,evolved),Color.cyan);
                _weaponChipIcons[i].texture=BuildChipIconTexture(); _weaponChipIcons[i].uvRect=BuildChipIconUv(weapon.Id);
                if(ArsenalContent.IsArsenalWeapon(weapon.Id)) { _weaponChipIcons[i].texture=ProceduralSpriteFactory.ArsenalWeapon(weapon.Id,rank,evolved).texture; _weaponChipIcons[i].uvRect=new Rect(0,0,1,1); }
                StyleApprovedSlot(_weaponChipBackgrounds[i],_weaponChipIcons[i],_weaponChipRanks[i],_weaponChipAccentBars[i],
                    Vector2.zero,1.8f+owned*4.95f,1.7f,4.45f,4.9f,weapon.Id,WeaponDisplayName(i,evolved),
                    "AUTOMATIC · RANK "+rank+(evolved?" · EVOLVED":""),rank,weapon.Ranks.Length,accent);
                owned++;
            }
            var limit=ProgressionRules.WeaponSlotLimit(_upgradeProgress.WeaponRanks);
            for(var i=0;i<5;i++)
            {
                var bg=_approvedEmptySlots[i]; bg.gameObject.SetActive(i>=owned); if(i<owned) continue;
                var icon=bg.GetComponentInChildren<RawImage>(); var rank=bg.transform.Find(bg.name+" Rank")?.GetComponent<Text>();
                // The existing factory names text generically; the last text is the rank view.
                var texts=bg.GetComponentsInChildren<Text>(true); rank=texts[1];
                var locked=i>=limit; rank.text=locked?"LOCKED":"EMPTY";
                StyleApprovedSlot(bg,icon,rank,null,Vector2.zero,1.8f+i*4.95f,1.7f,4.45f,4.9f,locked?"lock":"plus",
                    locked?"Fifth weapon slot":"Empty weapon slot",locked?"Reach rank VI on two weapons to unlock this slot.":"Choose an automatic weapon when you level up.",0,0,muted,true);
            }
            ApprovedText(_approvedArsenalLabel,Vector2.zero,1.8f,7.25f,24.25f,1.5f,1.15f,true);
            _approvedArsenalLabel.color = _approvedManualLabel.color = _approvedPassiveLabel.color = new Color(.82f,.91f,.98f);
            _approvedArsenalLabel.text="ARSENAL    "+owned+" / "+limit;
            ApprovedText(_approvedManualLabel,Vector2.zero,28.65f,7.25f,9,1.5f,1.15f,true);
            var manual=_legendaryWeapon!=LegendaryWeaponId.None; _approvedManualRank.text=manual?"":"FRAGMENTS";
            StyleApprovedSlot(_approvedManualSlot,_approvedManualIcon,_approvedManualRank,null,Vector2.zero,28.65f,1.7f,4.45f,4.9f,
                manual?(_legendaryWeapon==LegendaryWeaponId.SoundBlade?"soundBlade":"chargedRifle"):"lock",
                manual?(_legendaryWeapon==LegendaryWeaponId.SoundBlade?"Sound Blade":"Charged Rifle"):"Legendary slot",
                manual?"Hold LMB / F / right trigger to attack.":"Assemble three fragments at the dealer.",_legendaryRank,manual?3:0,new Color(.84f,.71f,.4f),!manual);
            var total=0; foreach(var rank in _upgradeProgress.SupportRanks) if(rank>0)total++;
            foreach(var rank in _upgradeProgress.LateRanks) if(rank>0)total++;
            var dense=total>10; var columns=dense?10:Mathf.Max(1,total); var cw=dense?3f:3.5f; var ch=dense?3.45f:4.9f; var gap=dense?.3f:.45f;
            var rows=Mathf.Max(1,Mathf.CeilToInt(total/(float)columns)); var stripWidth=columns*(cw+gap)-gap;
            ApprovedText(_approvedPassiveLabel,new Vector2(1,0),-1.8f,1.7f+rows*(ch+gap)+.3f,Mathf.Max(12,stripWidth),1.5f,1.15f,true);
            _approvedPassiveLabel.text="PASSIVES  "+total.ToString("00"); _approvedPassiveLabel.alignment=TextAnchor.LowerLeft;
            _approvedSecondWind=null; var ordinal=0;
            var supports=ExtendedCatalog.AllSupports();
            for(var i=0;i<_supportChipBackgrounds.Length;i++)
            {
                var rank=_upgradeProgress.SupportRanks[i]; _supportChipBackgrounds[i].gameObject.SetActive(rank>0);
                _supportChipNames[i].enabled=false; if(rank<=0)continue;
                var card=supports[i]; var detail=card.Descriptions[Mathf.Clamp(rank-1,0,card.Descriptions.Length-1)];
                PlaceApprovedCard(_supportChipBackgrounds[i],_supportChipIcons[i],_supportChipRanks[i],_supportChipAccentBars[i],
                    ordinal++,columns,rows,cw,ch,gap,card.Id,card.Name,detail,rank,card.MaxRank,ParseColor(card.Accent,Color.cyan));
                if(card.Id=="secondWind") _approvedSecondWind=_supportChipBackgrounds[i].GetComponent<ApprovedHudSlot>();
            }
            for(var i=0;i<_lateChipBackgrounds.Length;i++)
            {
                var rank=_upgradeProgress.LateRanks[i]; _lateChipBackgrounds[i].gameObject.SetActive(rank>0);
                _lateChipNames[i].enabled=false; if(rank<=0)continue;
                var card=ContentCatalog.LateUpgrades[i];
                PlaceApprovedCard(_lateChipBackgrounds[i],_lateChipIcons[i],_lateChipRanks[i],_lateChipAccentBars[i],
                    ordinal++,columns,rows,cw,ch,gap,card.Id,card.Name,"LATE UPGRADE · RANK "+rank,rank,card.MaxRank,ParseColor(card.Accent,Color.cyan));
            }
            _approvedCooldownSeconds=-1;
        }

        private void PlaceApprovedCard(Image bg,RawImage icon,Text rankText,Image bar,int index,int columns,int rows,
            float w,float h,float gap,string id,string title,string detail,int rank,int max,Color accent)
        {
            icon.texture=BuildChipIconTexture(); icon.uvRect=BuildChipIconUv(id);
            StyleApprovedSlot(bg,icon,rankText,bar,new Vector2(1,0),-1.8f-(columns-1-index%columns)*(w+gap),
                1.7f+(rows-1-index/columns)*(h+gap),w,h,id,title,detail,rank,max,accent);
        }
        private void UpdateApprovedHudCooldown()
        {
            if(_approvedSecondWind==null)return;
            var seconds=Mathf.CeilToInt(_secondWindRemaining); if(seconds==_approvedCooldownSeconds)return;
            _approvedCooldownSeconds=seconds; _approvedSecondWind.SetCooldown(seconds);
        }
        private void ShowApprovedSlot(ApprovedHudSlot slot)
        {
            if(_approvedTooltip==null)return;
            _approvedTooltip.gameObject.SetActive(true); _approvedTooltip.transform.SetAsLastSibling();
            var corners=new Vector3[4]; ((RectTransform)slot.transform).GetWorldCorners(corners);
            var local=((RectTransform)_canvas.transform).InverseTransformPoint(corners[1]);
            var canvasRect=((RectTransform)_canvas.transform).rect; var width=24*ApprovedUnit;
            var r=_approvedTooltip.rectTransform; r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,0);
            r.sizeDelta=new Vector2(width,10*ApprovedUnit);
            r.anchoredPosition=new Vector2(Mathf.Clamp(local.x-canvasRect.xMin,12,canvasRect.width-width-12),local.y-canvasRect.yMin+12);
            _approvedTooltipText.text="<b>"+slot.Title+"</b>\n"+slot.Detail;
            _approvedTooltipText.fontSize=Mathf.Max(11,Mathf.RoundToInt(.92f*ApprovedUnit));
            var t=_approvedTooltipText.rectTransform;t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;
            t.offsetMin=Vector2.one*10;t.offsetMax=-Vector2.one*10;
            _approvedTooltipText.horizontalOverflow=HorizontalWrapMode.Wrap;
        }
        private void HideApprovedSlot() { if(_approvedTooltip!=null) _approvedTooltip.gameObject.SetActive(false); }
    }
}
