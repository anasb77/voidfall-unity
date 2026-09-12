using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    // Explicit CSS-sized glyph quads: the shared UI text scale/line metrics must not reshape the portrait.
    public sealed class DealerPortraitView : MaskableGraphic
    {
        [Serializable] private sealed class PortraitData { public string[] rows; }
        private struct Run { public int Row, Column, Layer; public string Text; }
        private static readonly float[] Depth = { 1, 1.65f, 1.8f, 1.55f, 2.5f, 2.1f, 1.2f, .45f };
        private static string[] _rows;
        private static Font _font;
        // Rasterize near the displayed size, as the browser does. Shrinking a 64px
        // atlas made the hair into thin wire outlines instead of dense hinted glyphs.
        private const int AtlasSize = 12;
        private const string Glyphs = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
        private static readonly Color32[] Tones = {
            new Color32(154,163,168,255), new Color32(217,223,226,255), new Color32(217,223,226,255),
            new Color32(195,203,208,255), new Color32(217,223,226,255), new Color32(195,203,208,255),
            new Color32(140,151,158,255), new Color32(140,151,158,255) };
        private readonly char[] _cells = new char[64 * 34];
        private readonly int[] _layers = new int[64 * 34];
        private float _fontSize, _nextFrame, _phase;
        private float _target, _yaw, _gaze;
        private bool _smile, _reduced;
        private int _variation;
        public override Texture mainTexture => _font != null ? _font.material.mainTexture : Texture2D.whiteTexture;
        public static DealerPortraitView Create(Transform parent, string name, float fontSize, Vector2 size)
        {
            var rect = UIBuilder.CreateRect(parent, name); rect.sizeDelta = size;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            var view = rect.gameObject.AddComponent<DealerPortraitView>();
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New" }, AtlasSize);
            _font.RequestCharactersInTexture(Glyphs, AtlasSize, FontStyle.Normal);
            if (_rows == null)
            {
                var asset = Resources.Load<TextAsset>("VoidFall/Dealer/portrait");
                _rows = asset != null ? JsonUtility.FromJson<PortraitData>(asset.text).rows : new[] { "(  .  )" };
            }
            view._fontSize = fontSize; view.raycastTarget = false;
            return view;
        }
        protected override void OnEnable() { base.OnEnable(); Font.textureRebuilt += FontRebuilt; }
        protected override void OnDisable() { Font.textureRebuilt -= FontRebuilt; base.OnDisable(); }
        private void FontRebuilt(Font font) { if (font == _font) { SetVerticesDirty(); SetMaterialDirty(); } }
        public void SetPose(float look, bool smile, bool reduced, int variation)
        { _target = Mathf.Clamp(look, -1, 1); _smile = smile; _reduced = reduced; _variation = Mathf.Clamp(variation, 0, 3); }
        private void Update()
        {
            if (_font == null || _rows == null) return;
            var dt = Time.unscaledDeltaTime;
            _yaw = _reduced ? _target : Mathf.Lerp(_yaw, _target, Mathf.Min(1, dt * 4));
            _gaze = _reduced ? _target : Mathf.Lerp(_gaze, _target, Mathf.Min(1, dt * 10));
            if (Time.unscaledTime < _nextFrame) return;
            _nextFrame = Time.unscaledTime + 1f / 30;
            _phase = Time.unscaledTime * Mathf.PI * 2 / 4.4f;
            Render(_yaw, _gaze, _smile, _variation); SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (_font == null || _fontSize <= 0) return;
            var unit = _fontSize / AtlasSize; var advance = _fontSize * .5498f;
            var origin = new Vector2(-advance * 32, rectTransform.rect.yMax - _fontSize * .86f);
            var breath = _reduced ? 0 : Mathf.Sin(_phase);
            var pivot = new Vector2(0, rectTransform.rect.yMax - 34 * _fontSize * 1.12f * .85f);
            var tilt = (_variation == 1 ? 4.5f : _variation == 2 ? -1.5f : 0) * Mathf.Deg2Rad;
            var cos = Mathf.Cos(tilt); var sin = Mathf.Sin(tilt);
            Vector2 Pose(Vector2 point)
            {
                point = pivot + (point - pivot) * (1 + breath * .003f) + Vector2.up * (breath * 1.4f);
                var d = point - pivot; return pivot + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
            }
            for (var row = 0; row < 34; row++) for (var col = 0; col < 64; col++)
            {
                var index = row * 64 + col; var ch = _cells[index]; if (ch == ' ' || ch == '\0') continue;
                if (!_font.GetCharacterInfo(ch, out var glyph, AtlasSize)) continue;
                var position = origin + new Vector2(col * advance, -row * _fontSize * 1.12f);
                var layer = _layers[index];
                if (layer == 0 && !_reduced)
                {
                    var length = Mathf.Min(1, row / 30f); var side = col < 32 ? -1 : 1;
                    position.x += Mathf.Sin(_phase * .72f - row * .19f + side * .8f) * (.3f + length * length * 3.2f);
                    position.y -= Mathf.Cos(_phase * .72f - row * .12f) * length * .5f;
                }
                var start = mesh.currentVertCount; var tint = Tones[layer];
                mesh.AddVert(Pose(position + new Vector2(glyph.minX, glyph.minY) * unit), tint, glyph.uvBottomLeft);
                mesh.AddVert(Pose(position + new Vector2(glyph.minX, glyph.maxY) * unit), tint, glyph.uvTopLeft);
                mesh.AddVert(Pose(position + new Vector2(glyph.maxX, glyph.maxY) * unit), tint, glyph.uvTopRight);
                mesh.AddVert(Pose(position + new Vector2(glyph.maxX, glyph.minY) * unit), tint, glyph.uvBottomRight);
                mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
            }
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
        private void Render(float yaw, float gaze, bool smile, int variation)
        {
            const int width = 64, height = 34;
            var cells = _cells;
            for (var i = 0; i < cells.Length; i++) cells[i] = ' ';
            var rows = (string[])_rows.Clone();
            if (rows.Length < height) return;
            if (variation == 1) { Patch(rows, 13, 10, " .######s."); Patch(rows, 14, 26, " .'_____'."); }
            void Stamp(int row, int col, string value, int layer)
            {
                var compress = layer == 0 || layer == 7 ? 0 : -(col < 22 ? -1 : 1) * Mathf.Abs(yaw) * .65f;
                var x = 9 + col + Round(yaw * Depth[layer] * 2.2f + compress);
                for (var i = 0; i < value.Length; i++) if (value[i] != ' ' && x + i >= 0 && x + i < width && row >= 0 && row < height)
                { var cell = row * width + x + i; cells[cell] = value[i]; _layers[cell] = layer; }
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
        }
    }
}
