using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    public sealed class DealerPortraitView : MonoBehaviour
    {
        [Serializable] private sealed class PortraitData { public string[] rows; }
        private struct Run { public int Row, Column, Layer; public string Text; }
        private static readonly float[] Depth = { 1, 1.65f, 1.8f, 1.55f, 2.5f, 2.1f, 1.2f, .45f };
        private static string[] _rows;
        private static Font _font;
        private Text _hair, _features;
        private float _target, _yaw, _gaze;
        private bool _smile, _reduced;
        private int _variation, _key = int.MinValue;
        public static DealerPortraitView Create(Transform parent, string name, float fontSize, Vector2 size)
        {
            var rect = UIBuilder.CreateRect(parent, name); rect.sizeDelta = size;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            var view = rect.gameObject.AddComponent<DealerPortraitView>();
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New" }, 16);
            if (_rows == null)
            {
                var asset = Resources.Load<TextAsset>("VoidFall/Dealer/portrait");
                _rows = asset != null ? JsonUtility.FromJson<PortraitData>(asset.text).rows : new[] { "(  .  )" };
            }
            view._hair = view.Layer("Hair", fontSize, new Color(.60f, .64f, .66f));
            view._features = view.Layer("Features", fontSize, new Color(.85f, .88f, .89f));
            return view;
        }
        private Text Layer(string name, float size, Color color)
        {
            var text = UIBuilder.CreateText(transform, name, "", size, color, TextAnchor.UpperCenter, false);
            UIBuilder.Stretch(text.rectTransform); text.font = _font; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; text.lineSpacing = .9f; return text;
        }
        public void SetPose(float look, bool smile, bool reduced, int variation)
        { _target = Mathf.Clamp(look, -1, 1); _smile = smile; _reduced = reduced; _variation = Mathf.Clamp(variation, 0, 3); }
        private void Update()
        {
            if (_hair == null) return;
            var dt = Time.unscaledDeltaTime;
            _yaw = _reduced ? _target : Mathf.Lerp(_yaw, _target, Mathf.Min(1, dt * 4));
            _gaze = _reduced ? _target : Mathf.Lerp(_gaze, _target, Mathf.Min(1, dt * 10));
            var pose = Mathf.RoundToInt(_yaw * 5); var gaze = Mathf.RoundToInt(_gaze * 2);
            var key = pose + gaze * 20 + (_smile ? 200 : 0) + _variation * 500;
            if (_key != key)
            {
                _key = key; Render(pose / 5f, gaze / 2f, _smile, _variation, out var hair, out var face);
                _hair.text = hair; _features.text = face;
            }
            var t = Time.unscaledTime;
            var breath = _reduced ? 0 : Mathf.Sin(t * Mathf.PI * 2 / 4.4f) * 2;
            _hair.rectTransform.anchoredPosition = new Vector2(_reduced ? 0 : Mathf.Sin(t * .72f) * 1.8f, breath);
            _features.rectTransform.anchoredPosition = new Vector2(0, breath);
            transform.localRotation = Quaternion.Euler(0, 0, _variation == 1 ? 4.5f : _variation == 2 ? -1.5f : 0);
        }
        private static int LayerAt(int row, int col)
        {
            if (row < 13) return 0;
            if (row >= 30) return row == 30 && col > 33 ? 0 : 7;
            if (row == 15 && col >= 9 && col <= 38) return 2;
            if (col < 9 || col > 35) return 0;
            if (row < 15) return 1;
            if (row == 15) return 2;
            if (row == 16) return 3;
            if (row <= 20) return col >= 16 && col <= 27 ? 4 : 3;
            if (row == 23 || row == 24) return col >= 14 && col <= 29 ? 5 : 3;
            return 6;
        }
        private static int Round(float value) => (int)Math.Floor(value + .5f);
        private static void Patch(string[] rows, int row, int col, string value)
        { rows[row] = rows[row].Substring(0, col) + value + rows[row].Substring(Math.Min(rows[row].Length, col + value.Length)); }
        private static void Render(float yaw, float gaze, bool smile, int variation, out string hair, out string face)
        {
            const int width = 64, height = 34;
            var cells = new char[width * height]; var isHair = new bool[cells.Length];
            for (var i = 0; i < cells.Length; i++) cells[i] = ' ';
            var rows = (string[])_rows.Clone();
            if (rows.Length < height) { hair = ""; face = rows[0]; return; }
            if (variation == 1) { Patch(rows, 13, 10, " .######s."); Patch(rows, 14, 26, " .'_____'."); }
            void Stamp(int row, int col, string value, int layer)
            {
                var compress = layer == 0 || layer == 7 ? 0 : -(col < 22 ? -1 : 1) * Mathf.Abs(yaw) * .65f;
                var x = 9 + col + Round(yaw * Depth[layer] * 2.2f + compress);
                for (var i = 0; i < value.Length; i++) if (value[i] != ' ' && x + i >= 0 && x + i < width && row >= 0 && row < height)
                { var cell = row * width + x + i; cells[cell] = value[i]; isHair[cell] = layer == 0; }
            }
            var runs = new List<Run>();
            for (var row = 0; row < height; row++)
            {
                for (var start = 0; start < rows[row].Length;)
                {
                    var layer = LayerAt(row, start); var end = start + 1;
                    while (end < rows[row].Length && !((layer == 1 || layer == 2) && end == 22) && LayerAt(row, end) == layer) end++;
                    if (!((smile || variation == 2) && layer == 5)) runs.Add(new Run { Row = row, Column = start, Text = rows[row].Substring(start, end - start), Layer = layer });
                    start = end;
                }
            }
            runs.Sort((a, b) => Depth[a.Layer].CompareTo(Depth[b.Layer]));
            foreach (var run in runs) Stamp(run.Row, run.Column, run.Text, run.Layer);
            var eyeIndex = 0;
            foreach (Match socket in Regex.Matches(rows[15], @"\(( +)\)"))
            {
                var bias = variation == 1 ? (eyeIndex == 0 ? -.9f : .9f) : variation == 2 && eyeIndex == 0 ? -.75f : 0;
                var offset = Round((Mathf.Clamp(gaze + bias, -1, 1) + 1) * .5f * (socket.Groups[1].Length - 2));
                Stamp(15, socket.Index + 1 + offset, "##", 2); eyeIndex++;
            }
            if (smile) { Stamp(23, 14, "\\ .__________. /", 5); Stamp(24, 15, "'|_|_|_|_|_|'", 5); Stamp(25, 16, "`-..___..-'", 5); }
            else if (variation == 2) { Stamp(23, 14, "   __..---..__  ", 5); Stamp(24, 14, "`-..________..-'", 5); Stamp(25, 20, "---", 5); }
            if (variation == 3)
            {
                Stamp(8,27,"\\#",0); Stamp(9,28,"\\##",0); Stamp(10,28,"|##",0); Stamp(11,29,"\\##",0);
                Stamp(12,30,"\\##",0); Stamp(13,30,":###",0); Stamp(14,31,"|###",0); Stamp(15,31,":###",0);
                Stamp(16,32,"\\##",0); Stamp(17,33,":#",0); Stamp(18,34,".",0);
            }
            var h = new StringBuilder(cells.Length + height); var f = new StringBuilder(cells.Length + height);
            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++) { var i = row * width + col; h.Append(isHair[i] ? cells[i] : ' '); f.Append(isHair[i] ? ' ' : cells[i]); }
                h.Append('\n'); f.Append('\n');
            }
            hair = h.ToString(); face = f.ToString();
        }
    }
}
