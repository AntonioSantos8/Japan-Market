using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// A ponte entre o jogador e a roda de ferramentas.
    ///
    /// Ela é fina de propósito: o que decide se a ferramenta serve é o
    /// <see cref="IToolBelt"/>, que é C# puro e testado sem cena. Aqui só ficam
    /// as três coisas que exigem cena — trocar o modelo na mão, mirar, e aplicar
    /// o resultado na sujeira.
    ///
    /// Monte: um componente destes no jogador, com <c>_hand</c> apontando para o
    /// transform onde o modelo da ferramenta nasce. Quem chama
    /// <see cref="Select"/> e <see cref="UseOnAim"/> é o seu input — este
    /// componente não lê teclado, para não brigar com o PlayerInput que já existe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolUser : MonoBehaviour
    {
        [Header("Mão")]
        [Tooltip("Onde o modelo da ferramenta selecionada aparece.")]
        [SerializeField] private Transform _hand;

        [Header("Mira")]
        [Tooltip("De onde parte o raio. Vazio usa a câmera principal.")]
        [SerializeField] private Transform _aimOrigin;

        [Min(0.5f)]
        [SerializeField] private float _reach = 3f;

        [SerializeField] private LayerMask _aimMask = ~0;

        [Header("Depuração")]
        [SerializeField] private bool _logUses;

        private IToolBelt _belt;
        private GameObject _heldModel;
        private Camera _camera;

        /// <summary>Último resultado de uso. A UI lê para mostrar o aviso certo.</summary>
        public ToolUseResult LastResult { get; private set; }

        private void OnEnable()
        {
            if (!TryResolve()) return;

            _belt.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged(_belt.Selected);
        }

        private void OnDisable()
        {
            if (_belt == null) return;

            _belt.SelectionChanged -= OnSelectionChanged;
        }

        private bool TryResolve()
        {
            if (_belt != null) return true;

            GameContext game = GameContext.Current;
            if (game == null) return false;

            return game.Services.TryResolve(out _belt);
        }

        // ── seleção ──────────────────────────────────────────────────────────

        public bool Select(int slotIndex) => TryResolve() && _belt.TrySelect(slotIndex);

        /// <summary>
        /// Selects a slot, or puts its tool away when that same slot is already
        /// selected. The belt's TrySelect remains idempotent for non-input callers.
        /// </summary>
        public bool ToggleSelection(int slotIndex)
        {
            if (!TryResolve()) return false;

            if (_belt.SelectedIndex == slotIndex)
            {
                _belt.Deselect();
                return true;
            }

            return _belt.TrySelect(slotIndex);
        }

        public void Deselect()
        {
            if (TryResolve()) _belt.Deselect();
        }

        private void OnSelectionChanged(ToolSlot slot)
        {
            if (_heldModel != null) Destroy(_heldModel);
            _heldModel = null;

            if (slot == null || slot.IsEmpty || _hand == null) return;
            if (slot.Tool.HeldPrefab == null) return;

            _heldModel = Instantiate(slot.Tool.HeldPrefab, _hand.position, _hand.rotation, _hand);
        }

        // ── uso ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Usa a ferramenta no que estiver na mira. Chamado pelo seu input.
        /// </summary>
        public ToolUseResult UseOnAim()
        {
            if (!TryResolve()) return LastResult = ToolUseResult.NoTool;

            Grime target = FindGrime();

            // Sem alvo, a pergunta é "esta ferramenta serve para alguma coisa
            // aqui?" — e a resposta honesta é que não há superfície nenhuma.
            if (target == null) return LastResult = ToolUseResult.WrongSurface;

            ToolUseResult result = _belt.TryUse(target.Surface, out float power);
            LastResult = result;

            if (result != ToolUseResult.Ok) return result;

            bool finished = target.Scrub(power);

            if (_logUses)
                Debug.Log($"[Ferramenta] {_belt.Selected?.Tool} em {target.name}" +
                          $"{(finished ? " — limpou" : "")}.", this);

            return result;
        }

        /// <summary>
        /// O que dá para fazer no que está na mira agora. Para o cursor mudar
        /// antes do clique, em vez de o jogador descobrir errando.
        /// </summary>
        public ToolUseResult PeekAim()
        {
            if (!TryResolve()) return ToolUseResult.NoTool;

            Grime target = FindGrime();
            return target == null ? ToolUseResult.WrongSurface : _belt.Check(target.Surface);
        }

        private Grime FindGrime()
        {
            Transform origin = _aimOrigin != null ? _aimOrigin : AimCamera;
            if (origin == null) return null;

            return Physics.Raycast(origin.position, origin.forward, out RaycastHit hit,
                                   _reach, _aimMask, QueryTriggerInteraction.Ignore)
                && hit.collider.TryGetComponent(out Grime grime)
                ? grime
                : null;
        }

        /// <summary>
        /// A câmera é resolvida por tentativa e guardada. <c>Camera.main</c> é
        /// uma busca por tag e não deve ser chamada por frame.
        /// </summary>
        private Transform AimCamera
        {
            get
            {
                if (_camera == null) _camera = Camera.main;
                return _camera != null ? _camera.transform : null;
            }
        }
    }
}
