using JapanMarket.Core;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;

namespace JapanMarket.UI
{
    [DisallowMultipleComponent]
    public sealed class TrashBagHud : MonoBehaviour
    {
        private PlayerTrashBag _bag;
        private IToolBelt _belt;
        private GameObject _canvas;
        private GameObject _panel;
        private TextMeshProUGUI _capacity;
        private TextMeshProUGUI _scraps;
        private UnityEngine.UI.Image _fill;

        private void Update()
        {
            if (_bag != null) return;
            if (!ServiceContainer.Current.TryResolve(out PlayerTrashBag bag)
                || !ServiceContainer.Current.TryResolve(out IToolBelt belt)) return;
            _bag = bag;
            _belt = belt;
            Build();
            _bag.Changed += Refresh;
            _belt.SelectionChanged += OnSelected;
            Refresh();
        }

        private void OnSelected(ToolSlot _) => Refresh();

        private void Refresh()
        {
            _panel.SetActive(_bag.IsEquipped);
            _capacity.text = $"SACO DE LIXO    {_bag.Count}/{_bag.Capacity}";
            _capacity.color = _bag.IsFull ? new Color(1f, .65f, .3f) : Color.white;
            _scraps.text = $"SCRAPS  {_bag.Scraps}";
            _fill.rectTransform.anchorMax = new Vector2((float)_bag.Count / _bag.Capacity, 1f);
        }

        private void Build()
        {
            _canvas = new GameObject("Trash Bag HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            _canvas.transform.SetParent(transform, false);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = _canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            RectTransform panel = Rect("Capacity", _canvas.transform);
            _panel = panel.gameObject;
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-32, -32);
            panel.sizeDelta = new Vector2(340, 116);
            var background = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(.025f, .12f, .16f, .93f);
            background.raycastTarget = false;
            _capacity = Label("Bag Count", panel, new Vector2(18, -14), 24);
            _scraps = Label("Scraps", panel, new Vector2(18, -67), 20);
            _scraps.color = new Color(.4f, .9f, .8f);

            RectTransform track = Rect("Fill Track", panel);
            track.anchorMin = track.anchorMax = track.pivot = new Vector2(0, 1);
            track.anchoredPosition = new Vector2(18, -51);
            track.sizeDelta = new Vector2(304, 6);
            var trackImage = track.gameObject.AddComponent<UnityEngine.UI.Image>();
            trackImage.color = new Color(.18f, .28f, .3f);
            trackImage.raycastTarget = false;
            RectTransform fill = Rect("Fill", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            _fill = fill.gameObject.AddComponent<UnityEngine.UI.Image>();
            _fill.color = new Color(.1f, .85f, .75f);
            _fill.raycastTarget = false;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, Vector2 position, int size)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(310, 34);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private void OnDisable()
        {
            if (_bag != null) _bag.Changed -= Refresh;
            if (_belt != null) _belt.SelectionChanged -= OnSelected;
            _bag = null;
            _belt = null;
            if (_canvas != null) Destroy(_canvas);
        }
    }
}
