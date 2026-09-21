using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;

namespace JapanMarket.UI
{
    /// <summary>
    /// Seletor radial responsivo das ferramentas. A view monta apenas a camada visual;
    /// regras de desbloqueio, desgaste e seleção continuam pertencendo ao IToolBelt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolWheelView : MonoBehaviour
    {
        [Header("Conteúdo")]
        [Tooltip("Inclui um segmento para guardar a ferramenta atual.")]
        [SerializeField] private bool _includeEmptyHand = true;
        [SerializeField] private string _emptyHandName = "Nenhuma";
        [SerializeField] private Sprite _emptyHandIcon;
        [SerializeField] private string _lockedName = "TRANCADO";
        [SerializeField] private bool _hideWhenEmpty = true;

        [Header("Anel responsivo")]
        [Range(0.25f, 0.9f)]
        [Tooltip("Diâmetro como fração do menor lado disponível do Canvas.")]
        [SerializeField] private float _diameterFraction = 0.56f;
        [Min(120f)] [SerializeField] private float _minimumDiameter = 300f;
        [Min(120f)] [SerializeField] private float _maximumDiameter = 720f;
        [Range(0.12f, 0.75f)] [SerializeField] private float _innerRadius = 0.34f;
        [Range(0f, 12f)] [SerializeField] private float _segmentGapDegrees = 2f;
        [Range(8, 64)] [SerializeField] private int _arcResolution = 28;
        [Min(0f)] [SerializeField] private float _outerBorderWidth = 7f;
        [Range(0.25f, 0.8f)] [SerializeField] private float _contentRadius = 0.57f;
        [Range(0.05f, 0.25f)] [SerializeField] private float _iconSize = 0.13f;
        [Range(0.018f, 0.07f)] [SerializeField] private float _fontSize = 0.035f;

        [Header("Cores")]
        [SerializeField] private Color _screenDim = new(0f, 0f, 0f, 0.22f);
        [SerializeField] private Color _normalColor = new(0.025f, 0.16f, 0.21f, 0.98f);
        [SerializeField] private Color _selectedColor = new(0.04f, 0.27f, 0.34f, 1f);
        [SerializeField] private Color _hoverColor = new(0.02f, 0.42f, 0.52f, 1f);
        [SerializeField] private Color _lockedColor = new(0.045f, 0.08f, 0.10f, 0.92f);
        [SerializeField] private Color _borderColor = new(0.05f, 0.86f, 1f, 1f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _lockedTextColor = new(0.68f, 0.72f, 0.75f, 1f);

        [Header("Animação")]
        [Min(0.01f)] [SerializeField] private float _openDuration = 0.14f;
        [Min(0.01f)] [SerializeField] private float _hoverDuration = 0.09f;
        [Range(1f, 1.15f)] [SerializeField] private float _hoverScale = 1.045f;
        [Range(0.5f, 1f)] [SerializeField] private float _openingScale = 0.82f;

        private readonly List<SegmentWidget> _segments = new();
        private RectTransform _overlay;
        private RectTransform _wheel;
        private UnityEngine.UI.Image _dim;
        private CanvasGroup _canvasGroup;
        private Canvas _canvas;
        private IToolBelt _belt;
        private bool _built;
        private bool _isOpen;
        private bool _subscribed;
        private float _openProgress;
        private float _diameter;
        private int _hoveredIndex = -1;

        public bool IsOpen => _isOpen;

        private sealed class SegmentWidget
        {
            public RectTransform Root;
            public RadialToolSegment Graphic;
            public UnityEngine.UI.Image Icon;
            public TextMeshProUGUI Label;
            public int SlotIndex;
            public bool IsEmptyHand;
            public bool IsUnlocked;
            public float HoverProgress;
        }

        private void Awake()
        {
            BuildFrame();
            HideImmediate();
        }

        private void OnEnable()
        {
            TryResolve();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _isOpen = false;
            _hoveredIndex = -1;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_built) ApplyResponsiveLayout();
        }

        private void OnValidate()
        {
            _minimumDiameter = Mathf.Max(120f, _minimumDiameter);
            _maximumDiameter = Mathf.Max(_minimumDiameter, _maximumDiameter);
            _openDuration = Mathf.Max(0.01f, _openDuration);
            _hoverDuration = Mathf.Max(0.01f, _hoverDuration);
        }

        private void Update()
        {
            if (!_isOpen) return;

            float openStep = Time.unscaledDeltaTime / _openDuration;
            _openProgress = Mathf.MoveTowards(_openProgress, 1f, openStep);
            float easedOpen = 1f - Mathf.Pow(1f - _openProgress, 3f);
            _canvasGroup.alpha = easedOpen;
            _wheel.localScale = Vector3.one * Mathf.Lerp(_openingScale, 1f, easedOpen);

            for (int i = 0; i < _segments.Count; i++)
            {
                SegmentWidget segment = _segments[i];
                float target = i == _hoveredIndex ? 1f : 0f;
                segment.HoverProgress = Mathf.MoveTowards(
                    segment.HoverProgress, target, Time.unscaledDeltaTime / _hoverDuration);

                float easedHover = segment.HoverProgress * segment.HoverProgress
                                   * (3f - 2f * segment.HoverProgress);
                segment.Root.localScale = Vector3.one * Mathf.Lerp(1f, _hoverScale, easedHover);
                UpdateSegmentColor(segment, easedHover);
            }
        }

        /// <summary>Abre a roda. Retorna false se o serviço de ferramentas ainda não existe.</summary>
        public bool Open()
        {
            BuildFrame();
            if (!TryResolve()) return false;

            Subscribe();
            int expected = _belt.Slots.Count + (_includeEmptyHand ? 1 : 0);
            if (_segments.Count != expected) Rebuild();
            else Refresh();

            if (_hideWhenEmpty && _belt.Slots.Count == 0) return false;

            _isOpen = true;
            _hoveredIndex = -1;
            _openProgress = 0f;
            _overlay.gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _wheel.localScale = Vector3.one * _openingScale;
            return true;
        }

        /// <summary>Fecha a roda e, opcionalmente, confirma o segmento sob o mouse.</summary>
        public void Close(bool commitSelection)
        {
            if (!_isOpen) return;

            if (commitSelection && _hoveredIndex >= 0 && _hoveredIndex < _segments.Count)
            {
                SegmentWidget hovered = _segments[_hoveredIndex];
                if (hovered.IsUnlocked)
                {
                    if (hovered.IsEmptyHand) _belt.Deselect();
                    else _belt.TrySelect(hovered.SlotIndex);
                }
            }

            _isOpen = false;
            _hoveredIndex = -1;
            HideImmediate();
        }

        /// <summary>Atualiza o hover usando posição real de tela, independente da resolução.</summary>
        public void UpdatePointer(Vector2 screenPosition)
        {
            if (!_isOpen || _segments.Count == 0) return;

            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _wheel, screenPosition, eventCamera, out Vector2 local))
            {
                SetHovered(-1);
                return;
            }

            float outerRadius = _diameter * 0.5f;
            float distance = local.magnitude;
            if (distance < outerRadius * _innerRadius || distance > outerRadius)
            {
                SetHovered(-1);
                return;
            }

            float angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            float clockwiseFromTop = Mathf.Repeat(90f - angle, 360f);
            float slice = 360f / _segments.Count;
            int index = Mathf.FloorToInt((clockwiseFromTop + slice * 0.5f) / slice)
                        % _segments.Count;
            SetHovered(index);
        }

        private void SetHovered(int index)
        {
            if (_hoveredIndex == index) return;
            _hoveredIndex = index;
        }

        private bool TryResolve()
        {
            if (_belt != null) return true;
            return ServiceContainer.Current.TryResolve(out _belt);
        }

        private void Subscribe()
        {
            if (_belt == null || _subscribed) return;
            _belt.SlotsChanged += Refresh;
            _belt.SelectionChanged += OnSelectionChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_belt == null || !_subscribed) return;
            _belt.SlotsChanged -= Refresh;
            _belt.SelectionChanged -= OnSelectionChanged;
            _subscribed = false;
        }

        private void OnSelectionChanged(ToolSlot _) => Refresh();

        private void BuildFrame()
        {
            if (_built) return;

            RectTransform root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            _canvas = GetComponentInParent<Canvas>();
            _overlay = UIKit.Rect("Radial Overlay", root);
            UIKit.Stretch(_overlay);

            _dim = _overlay.gameObject.AddComponent<UnityEngine.UI.Image>();
            _dim.color = _screenDim;
            _dim.raycastTarget = false;

            _canvasGroup = _overlay.gameObject.AddComponent<CanvasGroup>();

            _wheel = UIKit.Rect("Ring", _overlay);
            _wheel.anchorMin = new Vector2(0.5f, 0.5f);
            _wheel.anchorMax = new Vector2(0.5f, 0.5f);
            _wheel.pivot = new Vector2(0.5f, 0.5f);
            _wheel.anchoredPosition = Vector2.zero;

            _built = true;
            ApplyResponsiveLayout();
        }

        private void Rebuild()
        {
            BuildFrame();
            UIKit.Clear(_wheel);
            _segments.Clear();

            if (_belt == null) return;

            int count = _belt.Slots.Count + (_includeEmptyHand ? 1 : 0);
            if (count == 0) return;

            float slice = 360f / count;
            int entry = 0;

            if (_includeEmptyHand)
                BuildSegment(entry++, -1, true, _emptyHandName, _emptyHandIcon, true, 90f, slice);

            for (int i = 0; i < _belt.Slots.Count; i++)
            {
                ToolSlot slot = _belt.Slots[i];
                bool unlocked = slot.IsUnlocked(UnlockContext);
                string label = GetToolName(slot);
                Sprite icon = slot.Tool != null ? slot.Tool.Icon : null;
                float center = 90f - entry * slice;
                BuildSegment(entry++, i, false, label, icon, unlocked, center, slice);
            }

            ApplyResponsiveLayout();
            Refresh();
        }

        private void BuildSegment(int visualIndex, int slotIndex, bool emptyHand,
                                  string label, Sprite icon, bool unlocked,
                                  float centerAngle, float slice)
        {
            RectTransform segmentRoot = UIKit.Rect($"Segment {visualIndex}", _wheel);
            UIKit.Stretch(segmentRoot);

            // RequireComponent não é herdado de Graphic em todas as versões do
            // Unity quando o componente é criado por AddComponent<T>(). Criar
            // explicitamente evita uma janela sem CanvasRenderer no Awake.
            segmentRoot.gameObject.AddComponent<CanvasRenderer>();
            var graphic = segmentRoot.gameObject.AddComponent<RadialToolSegment>();
            graphic.raycastTarget = false;
            graphic.Configure(centerAngle, slice, _innerRadius, _segmentGapDegrees,
                              _outerBorderWidth, _arcResolution, _normalColor, _borderColor);

            RectTransform iconRect = UIKit.Rect("Icon", segmentRoot);
            var iconImage = iconRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.enabled = icon != null;

            TextMeshProUGUI name = UIKit.Label("Name", segmentRoot, label, UIKit.BodySize,
                                                _textColor, TextAlignmentOptions.Center);
            name.enableWordWrapping = false;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.fontStyle = FontStyles.Bold;

            _segments.Add(new SegmentWidget
            {
                Root = segmentRoot,
                Graphic = graphic,
                Icon = iconImage,
                Label = name,
                SlotIndex = slotIndex,
                IsEmptyHand = emptyHand,
                IsUnlocked = unlocked
            });
        }

        private void ApplyResponsiveLayout()
        {
            if (!_built) return;

            RectTransform available = (RectTransform)transform;
            float shortestSide = Mathf.Min(available.rect.width, available.rect.height);
            if (shortestSide <= 0f && _canvas != null)
                shortestSide = Mathf.Min(_canvas.pixelRect.width, _canvas.pixelRect.height);
            if (shortestSide <= 0f) shortestSide = 1080f;

            _diameter = Mathf.Clamp(shortestSide * _diameterFraction,
                                    _minimumDiameter, _maximumDiameter);
            _wheel.sizeDelta = Vector2.one * _diameter;

            float polarDistance = _diameter * 0.5f * _contentRadius;
            float iconPixels = _diameter * _iconSize;
            float fontPixels = Mathf.Clamp(_diameter * _fontSize, 13f, 28f);
            float slice = _segments.Count > 0 ? 360f / _segments.Count : 360f;

            for (int i = 0; i < _segments.Count; i++)
            {
                SegmentWidget segment = _segments[i];
                float centerAngle = 90f - i * slice;
                float radians = centerAngle * Mathf.Deg2Rad;
                Vector2 position = new(Mathf.Cos(radians) * polarDistance,
                                       Mathf.Sin(radians) * polarDistance);

                RectTransform icon = (RectTransform)segment.Icon.transform;
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);
                icon.sizeDelta = Vector2.one * iconPixels;
                icon.anchoredPosition = position + Vector2.up * iconPixels * 0.16f;

                RectTransform label = (RectTransform)segment.Label.transform;
                label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
                label.pivot = new Vector2(0.5f, 0.5f);
                label.sizeDelta = new Vector2(_diameter * 0.28f, fontPixels * 1.6f);
                float labelOffset = segment.Icon.enabled ? iconPixels * 0.48f : 0f;
                label.anchoredPosition = position - Vector2.up * labelOffset;
                segment.Label.fontSize = fontPixels;

                segment.Graphic.Configure(centerAngle, slice, _innerRadius,
                    _segmentGapDegrees, _outerBorderWidth, _arcResolution,
                    segment.Graphic.FillColor, _borderColor);
            }
        }

        public void Refresh()
        {
            if (_belt == null || !_built) return;

            int expected = _belt.Slots.Count + (_includeEmptyHand ? 1 : 0);
            if (_segments.Count != expected)
            {
                if (_isOpen) Rebuild();
                return;
            }

            int firstTool = _includeEmptyHand ? 1 : 0;
            for (int i = 0; i < _segments.Count; i++)
            {
                SegmentWidget widget = _segments[i];
                bool broken = false;
                if (widget.IsEmptyHand)
                {
                    widget.IsUnlocked = true;
                    widget.Label.text = _emptyHandName;
                    widget.Icon.sprite = _emptyHandIcon;
                    widget.Icon.enabled = _emptyHandIcon != null;
                }
                else
                {
                    ToolSlot slot = _belt.Slots[i - firstTool];
                    widget.IsUnlocked = slot.IsUnlocked(UnlockContext);
                    widget.Label.text = widget.IsUnlocked ? GetToolName(slot) : _lockedName;
                    widget.Icon.sprite = slot.Tool != null ? slot.Tool.Icon : null;
                    widget.Icon.enabled = widget.Icon.sprite != null;
                    broken = slot.IsBroken;
                }

                widget.Label.color = !widget.IsUnlocked ? _lockedTextColor
                    : broken ? UIKit.Bad
                    : _textColor;
                UpdateSegmentColor(widget, widget.HoverProgress);
            }
        }

        private void UpdateSegmentColor(SegmentWidget segment, float hover)
        {
            Color baseColor;
            if (!segment.IsUnlocked) baseColor = _lockedColor;
            else
            {
                bool selected = segment.IsEmptyHand
                    ? _belt != null && _belt.SelectedIndex < 0
                    : _belt != null && _belt.SelectedIndex == segment.SlotIndex;
                baseColor = selected ? _selectedColor : _normalColor;
            }

            segment.Graphic.FillColor = Color.Lerp(baseColor, _hoverColor, hover);
        }

        private static string GetToolName(ToolSlot slot)
        {
            if (slot == null || slot.Tool == null) return string.Empty;
            return slot.Tool.DisplayName.IsEmpty ? slot.Tool.name : slot.Tool.DisplayName.Value;
        }

        private void HideImmediate()
        {
            if (_overlay == null) return;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _overlay.gameObject.SetActive(false);
        }

        private IUnlockContext UnlockContext =>
            ServiceContainer.Current.TryResolve(out IUnlockContext context) ? context : null;
    }
}

namespace JapanMarket.UI
{
    /// <summary>Graphic procedural de um segmento de anel; não precisa de sprite dedicado.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadialToolSegment : UnityEngine.UI.MaskableGraphic
    {
        private float _centerAngle = 90f;
        private float _arcAngle = 60f;
        private float _innerRadius = 0.34f;
        private float _gapDegrees = 2f;
        private float _borderWidth = 7f;
        private int _resolution = 28;
        private Color _fillColor = Color.white;
        private Color _borderColor = Color.cyan;
        private CanvasRenderer _renderer;

        // Graphic.mainTexture pode acabar sem uma textura válida em subclasses
        // procedurais. O shader padrão de UI multiplica os vértices por ela;
        // sem o texel branco, a malha existe mas fica completamente invisível.
        public override Texture mainTexture => Texture2D.whiteTexture;

        public Color FillColor
        {
            get => _fillColor;
            set
            {
                if (_fillColor == value) return;
                _fillColor = value;
                SetVerticesDirty();
            }
        }

        public void Configure(float centerAngle, float arcAngle, float innerRadius,
                              float gapDegrees, float borderWidth, int resolution,
                              Color fillColor, Color borderColor)
        {
            _centerAngle = centerAngle;
            _arcAngle = Mathf.Clamp(arcAngle, 1f, 360f);
            _innerRadius = Mathf.Clamp(innerRadius, 0.01f, 0.95f);
            _gapDegrees = Mathf.Clamp(gapDegrees, 0f, _arcAngle * 0.8f);
            _borderWidth = Mathf.Max(0f, borderWidth);
            _resolution = Mathf.Clamp(resolution, 3, 128);
            _fillColor = fillColor;
            _borderColor = borderColor;
            EnsureVisibleRenderer();
            SetAllDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureVisibleRenderer();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureVisibleRenderer();
            SetAllDirty();
        }

        private void EnsureVisibleRenderer()
        {
            if (_renderer == null) _renderer = GetComponent<CanvasRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<CanvasRenderer>();

            color = Color.white;
            _renderer.cullTransparentMesh = false;
            _renderer.SetColor(Color.white);
            _renderer.SetTexture(Texture2D.whiteTexture);
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect bounds = rectTransform.rect;
            float outer = Mathf.Min(bounds.width, bounds.height) * 0.5f;
            if (outer <= 0f) return;

            float inner = outer * _innerRadius;
            float fillOuter = Mathf.Max(inner, outer - _borderWidth);
            float halfArc = (_arcAngle - _gapDegrees) * 0.5f;
            float start = _centerAngle - halfArc;
            float end = _centerAngle + halfArc;
            int steps = Mathf.Max(2, Mathf.CeilToInt(_resolution * _arcAngle / 360f));

            AddBand(vertexHelper, inner, fillOuter, start, end, steps, _fillColor);
            if (_borderWidth > 0f)
                AddBand(vertexHelper, fillOuter, outer, start, end, steps, _borderColor);
        }

        private static void AddBand(UnityEngine.UI.VertexHelper vh, float inner, float outer,
                                    float start, float end, int steps, Color color)
        {
            int baseVertex = vh.currentVertCount;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float radians = Mathf.Lerp(start, end, t) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
                var innerVertex = UIVertex.simpleVert;
                innerVertex.position = direction * inner;
                innerVertex.color = color;
                innerVertex.uv0 = new Vector2(t, 0f);
                vh.AddVert(innerVertex);

                var outerVertex = UIVertex.simpleVert;
                outerVertex.position = direction * outer;
                outerVertex.color = color;
                outerVertex.uv0 = new Vector2(t, 1f);
                vh.AddVert(outerVertex);
            }

            for (int i = 0; i < steps; i++)
            {
                int index = baseVertex + i * 2;
                vh.AddTriangle(index, index + 1, index + 3);
                vh.AddTriangle(index, index + 3, index + 2);
            }
        }
    }
}
