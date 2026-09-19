using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JapanMarket.UI
{
    /// <summary>
    /// A barra de ferramentas no HUD: o que está na mão, o que está travado, e
    /// quanto falta a ferramenta quebrar.
    ///
    /// Barra, e não roda radial. A roda radial precisa de pausa, mira do mouse e
    /// uma animação de abrir; a barra mostra a mesma informação o tempo todo e
    /// casa com o atalho numérico que o jogo já usa. Trocar por uma roda depois
    /// não toca no <c>IToolBelt</c> — esta classe só lê.
    ///
    /// Ela MONTA a si mesma. Ponha o componente num objeto de UI dentro do Canvas
    /// do HUD e não configure mais nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolWheelView : MonoBehaviour
    {
        [Tooltip("Altura de cada slot, em pixels.")]
        [SerializeField] private float _slotSize = 64f;

        [Tooltip("Esconde a barra inteira quando o cinto não tem slot nenhum.")]
        [SerializeField] private bool _hideWhenEmpty = true;

        private readonly List<SlotWidget> _widgets = new();

        private RectTransform _row;
        private IToolBelt _belt;
        private bool _built;

        /// <summary>Os pedaços de um slot, guardados para atualizar sem redesenhar.</summary>
        private sealed class SlotWidget
        {
            public Image Frame;
            public Image Fill;
            public TextMeshProUGUI Number;
            public TextMeshProUGUI Name;
            public Image Condition;
        }

        private void Awake() => BuildFrame();

        private void OnEnable()
        {
            if (!TryResolve()) return;

            _belt.SlotsChanged += Refresh;
            _belt.SelectionChanged += OnSelectionChanged;

            Rebuild();
        }

        private void OnDisable()
        {
            if (_belt == null) return;

            _belt.SlotsChanged -= Refresh;
            _belt.SelectionChanged -= OnSelectionChanged;
        }

        private void Update()
        {
            // O cinto pode não existir ainda no OnEnable: numa cena aditiva o
            // GameContext entra depois. Tentar de novo aqui custa uma comparação
            // por frame e evita um HUD que nunca aparece.
            if (_belt == null && TryResolve())
            {
                _belt.SlotsChanged += Refresh;
                _belt.SelectionChanged += OnSelectionChanged;

                Rebuild();
            }
        }

        private bool TryResolve() => ServiceContainer.Current.TryResolve(out _belt);

        private void OnSelectionChanged(ToolSlot _) => Refresh();

        // ── montagem ─────────────────────────────────────────────────────────

        private void BuildFrame()
        {
            if (_built) return;

            var root = (RectTransform)transform;

            _row = UIKit.Row("Slots", root, _slotSize, 6f, new RectOffset(0, 0, 0, 0));
            _row.anchorMin = new Vector2(0.5f, 0f);
            _row.anchorMax = new Vector2(0.5f, 0f);
            _row.pivot = new Vector2(0.5f, 0f);
            _row.anchoredPosition = new Vector2(0f, 24f);
            _row.sizeDelta = new Vector2(0f, _slotSize);

            var fitter = _row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            _built = true;
        }

        private void Rebuild()
        {
            BuildFrame();
            UIKit.Clear(_row);
            _widgets.Clear();

            if (_belt == null) return;

            IReadOnlyList<ToolSlot> slots = _belt.Slots;

            if (_hideWhenEmpty) _row.gameObject.SetActive(slots.Count > 0);

            for (int i = 0; i < slots.Count; i++) _widgets.Add(BuildSlot(i));

            Refresh();
        }

        private SlotWidget BuildSlot(int index)
        {
            var widget = new SlotWidget();

            widget.Frame = UIKit.Panel($"Slot {index}", _row, UIKit.Surface);
            widget.Frame.Width(_slotSize);

            RectTransform frameRect = (RectTransform)widget.Frame.transform;

            // O preenchimento fica ATRÁS do texto e cobre o quadrado inteiro: é
            // ele que muda de cor para marcar o slot selecionado.
            widget.Fill = UIKit.Panel("Fundo", frameRect, UIKit.SurfaceAlt);
            UIKit.Stretch((RectTransform)widget.Fill.transform, 2f);

            widget.Number = UIKit.Label("Número", frameRect, (index + 1).ToString(),
                                        UIKit.SmallSize, UIKit.TextDim);
            RectTransform number = (RectTransform)widget.Number.transform;
            number.anchorMin = new Vector2(0f, 1f);
            number.anchorMax = new Vector2(0f, 1f);
            number.pivot = new Vector2(0f, 1f);
            number.anchoredPosition = new Vector2(6f, -4f);
            number.sizeDelta = new Vector2(20f, 16f);

            widget.Name = UIKit.Label("Nome", frameRect, string.Empty, UIKit.SmallSize,
                                      UIKit.Text, TextAlignmentOptions.Center);
            RectTransform name = (RectTransform)widget.Name.transform;
            UIKit.Stretch(name, 4f);

            // Barrinha de desgaste, colada na base do quadrado.
            widget.Condition = UIKit.Panel("Desgaste", frameRect, UIKit.Good);
            RectTransform condition = (RectTransform)widget.Condition.transform;
            condition.anchorMin = new Vector2(0f, 0f);
            condition.anchorMax = new Vector2(1f, 0f);
            condition.pivot = new Vector2(0f, 0f);
            condition.offsetMin = new Vector2(2f, 2f);
            condition.offsetMax = new Vector2(-2f, 5f);

            return widget;
        }

        // ── desenho ──────────────────────────────────────────────────────────

        public void Refresh()
        {
            if (_belt == null || _widgets.Count == 0) return;

            IReadOnlyList<ToolSlot> slots = _belt.Slots;

            // O layout do cinto pode ter mudado (save carregado, layout trocado).
            // Reconstruir é mais barato que manter dois caminhos de sincronização.
            if (slots.Count != _widgets.Count) { Rebuild(); return; }

            for (int i = 0; i < slots.Count; i++) Draw(_widgets[i], slots[i]);
        }

        private void Draw(SlotWidget widget, ToolSlot slot)
        {
            bool unlocked = slot.IsUnlocked(UnlockContext);
            bool selected = _belt.SelectedIndex == slot.Index;

            if (!unlocked)
            {
                // Travado é escrito, não deixado em branco: um slot vazio parece
                // bug, e "Travado" parece progressão.
                //
                // Palavra, e não um cadeado desenhado: um emoji que a fonte do
                // projeto não tiver vira um quadradinho vazio, e aí o slot
                // travado passa a parecer defeito de fonte.
                widget.Fill.color = new Color(0f, 0f, 0f, 0.35f);
                widget.Name.text = "Travado";
                widget.Name.color = UIKit.TextDim;
                widget.Condition.gameObject.SetActive(false);
                return;
            }

            widget.Fill.color = selected ? UIKit.AccentDim : UIKit.SurfaceAlt;

            if (slot.IsEmpty)
            {
                widget.Name.text = string.Empty;
                widget.Condition.gameObject.SetActive(false);
                return;
            }

            string label = slot.Tool.DisplayName.IsEmpty
                ? slot.Tool.name
                : slot.Tool.DisplayName.Value;

            widget.Name.text = label;
            widget.Name.color = slot.IsBroken ? UIKit.Bad : UIKit.Text;

            // Ferramenta que não desgasta não ganha barra: uma barra sempre cheia
            // no tablet faria o jogador procurar um desgaste que não existe.
            bool wears = slot.Tool.Wears;
            widget.Condition.gameObject.SetActive(wears);

            if (!wears) return;

            float condition = slot.Condition;
            RectTransform bar = (RectTransform)widget.Condition.transform;
            bar.anchorMax = new Vector2(Mathf.Clamp01(condition), 0f);

            widget.Condition.color =
                condition <= 0f ? UIKit.Bad
                : condition < 0.3f ? new Color(0.95f, 0.75f, 0.35f)
                : UIKit.Good;
        }

        /// <summary>
        /// O contexto de desbloqueio, para saber se o slot abriu. Resolvido a
        /// cada desenho porque a barra desenha raramente — só quando algo muda.
        /// </summary>
        private IUnlockContext UnlockContext =>
            ServiceContainer.Current.TryResolve(out IUnlockContext context) ? context : null;
    }
}
