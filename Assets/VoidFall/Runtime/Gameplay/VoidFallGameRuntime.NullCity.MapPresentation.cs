using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private SpriteRenderer _nullCityRoadSurface;
        private readonly Sprite[] _nullCityRoadSprites = new Sprite[5];
        private Canvas _nullCitySignCanvas;
        private Text _nullCitySignText;

        private void RenderNullCityRoadSurface(NullCityPurge purge, Vector2 offset)
        {
            if (!purge.Active || _backdropView.sprite == null) return;
            var index = purge.Lane == 2 && purge.X > 1000 ? 4 : purge.Lane;
            var sprite = _nullCityRoadSprites[index];
            if (sprite == null)
            {
                var plate = _backdropView.sprite;
                var source = plate.rect;
                var rect = new Rect(source.x + (float)purge.X / 1600f * source.width,
                    source.y + (900f - (float)(purge.Y + purge.Height)) / 900f * source.height,
                    (float)purge.Width / 1600f * source.width, (float)purge.Height / 900f * source.height);
                sprite = Sprite.Create(plate.texture, rect, Vector2.one * .5f, plate.pixelsPerUnit,
                    0, SpriteMeshType.FullRect);
                sprite.name = "Null City Active Road " + index;
                _nullCityRoadSprites[index] = sprite;
            }
            if (_nullCityRoadSurface == null) _nullCityRoadSurface = CreateView("Null City Energized Road", sprite, -75);
            _nullCityRoadSurface.sprite = sprite;
            _nullCityRoadSurface.transform.position = NullCityWorld((float)(purge.X + purge.Width * .5),
                (float)(purge.Y + purge.Height * .5)) + offset;
            _nullCityRoadSurface.transform.localScale = new Vector3(
                (float)purge.Width * NullCityRules.WorldScale / sprite.bounds.size.x,
                (float)purge.Height * NullCityRules.WorldScale / sprite.bounds.size.y, 1);
            _nullCityRoadSurface.color = _backdropView.color;
            _nullCityRoadSurface.enabled = true;
        }

        private void ReleaseNullCityRoadSprites()
        {
            ClearNullCityProp(_nullCityRoadSurface);
            for (var i = 0; i < _nullCityRoadSprites.Length; i++)
            {
                if (_nullCityRoadSprites[i] != null) Destroy(_nullCityRoadSprites[i]);
                _nullCityRoadSprites[i] = null;
            }
        }

        private void RenderNullCitySign(bool lockdown)
        {
            if (_nullCitySignCanvas == null)
            {
                var root = new GameObject("Null City LCD Text", typeof(RectTransform), typeof(Canvas));
                root.transform.SetParent(transform, false);
                _nullCitySignCanvas = root.GetComponent<Canvas>();
                _nullCitySignCanvas.renderMode = RenderMode.WorldSpace;
                _nullCitySignCanvas.overrideSorting = true;
                _nullCitySignCanvas.sortingOrder = -85;
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(1060, 180);
                var cover = new GameObject("LCD Screen", typeof(RectTransform), typeof(Image));
                cover.transform.SetParent(root.transform, false);
                var coverRect = (RectTransform)cover.transform;
                coverRect.anchorMin = Vector2.zero; coverRect.anchorMax = Vector2.one;
                coverRect.offsetMin = coverRect.offsetMax = Vector2.zero;
                cover.GetComponent<Image>().color = new Color(.02f, .045f, .08f, 1f);
                cover.GetComponent<Image>().raycastTarget = false;
                _nullCitySignText = CreateText(root.transform, Vector2.zero, Vector2.one * .5f,
                    42, Color.white);
                _nullCitySignText.rectTransform.sizeDelta = rect.sizeDelta;
                _nullCitySignText.alignment = TextAnchor.MiddleCenter;
                _nullCitySignText.raycastTarget = false;
            }
            _nullCitySignCanvas.gameObject.SetActive(true);
            // Keep high-resolution text metrics, but place its 265x45 authored
            // screen in the same coordinate scale as the city artwork.
            _nullCitySignCanvas.transform.localScale = Vector3.one * .25f;
            _nullCitySignCanvas.transform.position = NullCityWorld(1172.5f, 107.5f);
            _nullCitySignText.text = NullCityRules.SignText(lockdown);
            _nullCitySignText.color = lockdown ? new Color(.95f, .58f, .55f) : new Color(.53f, .84f, .81f);
        }
    }
}
