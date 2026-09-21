using System.Collections.Generic;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Tooltip("Animator compartilhado por todas as ferramentas. Deve ficar na mão ou em um pai dela.")]
        [SerializeField] private Animator _toolAnimator;

        [Tooltip("Parâmetro inteiro que recebe o índice da ferramenta equipada. -1 = mão vazia.")]
        [SerializeField] private string _equippedToolParameter = "EquippedTool";

        [Tooltip("Bool mantido ativo enquanto a ferramenta está em uso, permitindo animação em loop.")]
        [FormerlySerializedAs("_useTrigger")]
        [SerializeField] private string _usingParameter = "Using";

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
        private readonly Dictionary<ToolDefinition, GameObject> _heldModels = new();
        private Camera _camera;
        private int _equippedToolHash;
        private int _usingParameterHash;
        private bool _hasEquippedToolParameter;
        private bool _hasUsingParameter;
        private bool _subscribed;
        private Grime _activeGrime;
        private float _activePower;
        private bool _isUsing;

        /// <summary>Último resultado de uso. A UI lê para mostrar o aviso certo.</summary>
        public ToolUseResult LastResult { get; private set; }

        private void OnEnable()
        {
            TryInitialize();
        }

        private void Update()
        {
            // O Player pode habilitar antes do GameContext. Nesse caso o
            // serviço ainda não existe no primeiro OnEnable, então concluímos
            // a ligação no primeiro frame em que ele ficar disponível.
            if (!_subscribed) TryInitialize();
            if (_isUsing) ContinueUsing();
        }

        private void OnDisable()
        {
            StopUsing();
            if (_belt == null || !_subscribed) return;

            _belt.SelectionChanged -= OnSelectionChanged;
            _subscribed = false;
        }

        private bool TryInitialize()
        {
            if (_subscribed) return true;
            if (!TryResolve()) return false;

            ResolvePrefabReferences();
            PrepareHeldModels();
            PrepareAnimator();
            _belt.SelectionChanged += OnSelectionChanged;
            _subscribed = true;
            OnSelectionChanged(_belt.Selected);
            return true;
        }

        private void ResolvePrefabReferences()
        {
            Camera playerCamera = GetComponentInChildren<Camera>(true);
            if (_aimOrigin == null && playerCamera != null)
                _aimOrigin = playerCamera.transform;

            if (_hand == null && playerCamera != null)
                _hand = playerCamera.transform.Find("Tool Hand");

            if (_toolAnimator == null && _hand != null)
                _toolAnimator = _hand.GetComponent<Animator>();
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
            StopUsing();
            PrepareHeldModels();

            foreach (GameObject model in _heldModels.Values)
                if (model != null) model.SetActive(false);

            _heldModel = null;

            int slotIndex = slot != null && !slot.IsEmpty ? slot.Index : -1;
            SetEquippedToolParameter(slotIndex);

            if (slotIndex < 0 || _hand == null) return;
            if (!_heldModels.TryGetValue(slot.Tool, out _heldModel) || _heldModel == null) return;

            _heldModel.SetActive(true);
        }

        private void PrepareHeldModels()
        {
            if (_belt == null || _hand == null) return;

            _heldModels.Clear();
            for (int i = 0; i < _belt.Slots.Count; i++)
            {
                ToolSlot slot = _belt.Slots[i];
                ToolDefinition tool = slot?.Tool;
                if (tool == null || tool.HeldPrefab == null)
                    continue;

                Transform model = _hand.Find(tool.HeldPrefab.name);
                if (model != null) _heldModels[tool] = model.gameObject;
            }
        }

        private void PrepareAnimator()
        {
            if (_toolAnimator == null && _hand != null)
                _toolAnimator = _hand.GetComponentInParent<Animator>();

            _equippedToolHash = Animator.StringToHash(_equippedToolParameter);
            if (_usingParameter == "Use") _usingParameter = "Using";
            _usingParameterHash = Animator.StringToHash(_usingParameter);
            _hasEquippedToolParameter = HasAnimatorParameter(
                _equippedToolHash, AnimatorControllerParameterType.Int);
            _hasUsingParameter = HasAnimatorParameter(
                _usingParameterHash, AnimatorControllerParameterType.Bool);
        }

        private bool HasAnimatorParameter(int hash, AnimatorControllerParameterType type)
        {
            if (_toolAnimator == null) return false;

            foreach (AnimatorControllerParameter parameter in _toolAnimator.parameters)
                if (parameter.nameHash == hash && parameter.type == type) return true;

            return false;
        }

        private void SetEquippedToolParameter(int slotIndex)
        {
            if (_hasEquippedToolParameter)
                _toolAnimator.SetInteger(_equippedToolHash, slotIndex);
        }

        // ── uso ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Usa a ferramenta no que estiver na mira. Chamado pelo seu input.
        /// </summary>
        public ToolUseResult UseOnAim()
        {
            StopUsing();
            if (!TryResolve()) return LastResult = ToolUseResult.NoTool;

            Grime target = FindGrime();

            // Sem alvo, a pergunta é "esta ferramenta serve para alguma coisa
            // aqui?" — e a resposta honesta é que não há superfície nenhuma.
            if (target == null) return LastResult = ToolUseResult.WrongSurface;

            ToolUseResult result = _belt.TryUse(target.Surface, out float power);
            LastResult = result;

            if (result != ToolUseResult.Ok) return result;

            _activeGrime = target;
            _activePower = power;
            SetUsing(true);

            return result;
        }

        public void StopUsing()
        {
            _activeGrime = null;
            _activePower = 0f;
            SetUsing(false);
        }

        private void ContinueUsing()
        {
            if (_activeGrime == null || FindGrime() != _activeGrime)
            {
                StopUsing();
                return;
            }

            string grimeName = _activeGrime.name;
            bool finished = _activeGrime.Scrub(_activePower * Time.deltaTime);
            if (!finished) return;

            if (_logUses)
                Debug.Log($"[Ferramenta] {_belt.Selected?.Tool} limpou {grimeName}.", this);

            // Destroy é adiado até o fim do frame. Desligar explicitamente aqui
            // evita que o Animator continue em loop enquanto o botão está preso.
            StopUsing();
        }

        private void SetUsing(bool value)
        {
            _isUsing = value;
            if (_hasUsingParameter)
                _toolAnimator.SetBool(_usingParameterHash, value);
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
