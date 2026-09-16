using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// O caixa, agora como capacidade de um móvel qualquer.
    ///
    /// Este componente é o que encerra o item 9 do refatoramento. O que ele
    /// deixou de fazer, em relação ao <c>CashRegister</c> de 700 linhas:
    ///
    ///  • não spawna item, não anima sacola, não toca som, não controla câmera;
    ///  • não conhece o jogador nem lê <c>Input</c>;
    ///  • não mantém <c>_totalExpected</c> e <c>_scannedCount</c> em paralelo;
    ///  • não indexa <c>queuePoints[i]</c> pela contagem de clientes;
    ///  • não chama <c>SetTarget</c> em todos os NPCs a cada entrada e saída;
    ///  • não se registra no ServiceLocator como "o caixa" do jogo.
    ///
    /// E o que sobrou também não mora aqui: fila, venda e estado são um
    /// <see cref="CheckoutDesk"/>, C# puro, testado sem cena. Deste componente
    /// são só a GEOMETRIA (onde a fila começa, para que lado cresce, onde é o
    /// balcão) e o CICLO DE VIDA do Unity — as duas coisas que exigem um
    /// MonoBehaviour. A parte visual e de input é a Fase 5b, em componentes
    /// separados que assinam <see cref="SessionOpened"/>.
    ///
    /// Monte assim: um prefab de móvel com <c>FurnitureInstance</c>, este
    /// componente, e um <c>Transform</c> vazio marcando onde a fila começa.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheckoutStation : FurnitureCapabilityBehaviour, ICheckoutStation
    {
        [Header("Fila")]
        [Tooltip("Onde o primeiro da fila para. Se vazio, usa a frente do móvel.")]
        [SerializeField] private Transform _queueAnchor;

        [Tooltip("Para que lado a fila cresce. Se vazio, cresce para trás do móvel.")]
        [SerializeField] private Transform _queueDirectionMarker;

        [Min(0.4f)]
        [Tooltip("Distância entre um cliente e o seguinte.")]
        [SerializeField] private float _queueSpacing = 1.1f;

        [Min(0)]
        [Tooltip("Tamanho máximo da fila. 0 = sem limite.")]
        [SerializeField] private int _maxQueueLength = 8;

        [Header("Balcão")]
        [Tooltip("Onde o cliente deposita as compras. Se vazio, usa o centro do móvel.")]
        [SerializeField] private Transform _counterPoint;

        [Header("Operação")]
        [Tooltip("Desmarque para fechar este caixa sem removê-lo da loja.")]
        [SerializeField] private bool _open = true;

        [Header("Sandbox")]
        [Tooltip("Segundos até a venda se concluir sozinha, para testar o ciclo " +
                 "sem a interface do caixa. 0 = desligado. Deixe 0 no jogo.")]
        [Min(0f)] [SerializeField] private float _autoServeSeconds;

        private CheckoutDesk _desk;

        private ICheckoutService _service;
        private float _sessionAge;
        private bool _live;
        private bool _shuttingDown;
        private bool _warnedMissingService;

        // ── ICheckoutStation: estado ─────────────────────────────────────────

        public CheckoutStationState State => Desk.State;

        /// <summary>
        /// Repare que isto NÃO consulta <c>isActiveAndEnabled</c>. A estação é
        /// perguntada por quem segura uma referência tipada como interface — o
        /// cliente na fila — e nessa forma o operador == sobrecarregado do Unity
        /// não existe mais: se o móvel foi destruído, qualquer acesso a
        /// propriedade de engine lançaria MissingReferenceException no meio do
        /// Exit de um estado. Os dois sinalizadores são gerenciados, escritos
        /// por Awake/OnEnable/OnDisable, e OnDisable roda antes da destruição.
        /// </summary>
        public bool IsOperational =>
            _open && _live && !_shuttingDown && Owner != null && Owner.IsAlive;

        public bool AcceptsNewCustomers => Desk.AcceptsNewCustomers;

        public event Action<ICheckoutStation> StateChanged;
        public event Action<ICheckoutStation, CheckoutSession> SessionOpened;
        public event Action<ICheckoutStation, CheckoutSession, SessionCloseReason> SessionClosed;

        // ── ICheckoutStation: geometria ──────────────────────────────────────

        public Vector3 QueueAnchor => _queueAnchor != null
            ? _queueAnchor.position
            : transform.position + transform.forward * 1.2f;

        public Vector3 QueueDirection
        {
            get
            {
                if (_queueDirectionMarker != null)
                {
                    Vector3 explicitDirection = _queueDirectionMarker.position - QueueAnchor;
                    explicitDirection.y = 0f;

                    if (explicitDirection.sqrMagnitude > 0.0001f)
                        return explicitDirection.normalized;
                }

                // Sem marcador, a fila cresce afastando-se do móvel.
                Vector3 away = QueueAnchor - transform.position;
                away.y = 0f;

                return away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
            }
        }

        public float QueueSpacing => _queueSpacing;

        public Vector3 CounterPosition =>
            _counterPoint != null ? _counterPoint.position : transform.position;

        public Vector3 GetQueuePosition(int index) =>
            QueueAnchor + QueueDirection * (_queueSpacing * Mathf.Max(0, index));

        // ── ICheckoutStation: delegação ao balcão ────────────────────────────

        public int QueueLength => Desk.QueueLength;
        public int GetQueueIndex(ICustomer customer) => Desk.GetQueueIndex(customer);
        public bool IsFront(ICustomer customer) => Desk.IsFront(customer);
        public CheckoutSession CurrentSession => Desk.CurrentSession;

        public bool TryJoinQueue(ICustomer customer, out int index) =>
            Desk.TryJoinQueue(customer, out index);

        public void LeaveQueue(ICustomer customer) => Desk.LeaveQueue(customer);

        public bool TryOpenSession(ICustomer customer, IReadOnlyList<SaleLine> lines,
                                   PaymentMethod method, out CheckoutSession session)
        {
            bool opened = Desk.TryOpenSession(customer, lines, method, out session);
            if (opened) _sessionAge = 0f;
            return opened;
        }

        public void CloseSession(CheckoutSession session, SessionCloseReason reason) =>
            Desk.CloseSession(session, reason);

        // ── ciclo de vida ────────────────────────────────────────────────────

        /// <summary>
        /// Criado sob demanda para que a ordem de Awake entre componentes do
        /// mesmo GameObject não importe: quem perguntar primeiro constrói.
        /// </summary>
        private CheckoutDesk Desk
        {
            get
            {
                if (_desk != null) return _desk;

                _desk = new CheckoutDesk(() => IsOperational)
                {
                    MaxQueueLength = _maxQueueLength,
                };

                _desk.StateChanged  += _ => StateChanged?.Invoke(this);
                _desk.SessionOpened += (_, s) => SessionOpened?.Invoke(this, s);
                _desk.SessionClosed += (_, s, r) => SessionClosed?.Invoke(this, s, r);

                return _desk;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            // OnEnable só roda para componentes habilitados, e Awake roda para
            // todos. Semear aqui garante que um caixa desabilitado no inspetor
            // nasça marcado como fora de operação, em vez de depender de um
            // OnEnable que nunca virá.
            _live = enabled && gameObject.activeInHierarchy;

            Desk.Refresh();
        }

        /// <summary>
        /// Resolvido sob demanda, e não no Awake, porque um móvel pode ser
        /// colocado pelo jogador antes de o contexto terminar de montar — e um
        /// caixa que nasceu cedo demais ficaria com <c>_service</c> null para
        /// sempre, aceitando fila e nunca fechando venda nenhuma.
        /// </summary>
        private ICheckoutService Service
        {
            get
            {
                if (_service != null) return _service;

                GameContext game = GameContext.Current;
                if (game != null) game.Services.TryResolve(out _service);

                if (_service == null && !_warnedMissingService)
                {
                    _warnedMissingService = true;
                    Debug.LogWarning("[CheckoutStation] Nenhum ICheckoutService registrado. " +
                                     "A fila funciona, mas nenhuma venda vai fechar. " +
                                     "Falta um GameContext na cena?", this);
                }

                return _service;
            }
        }

        private void OnEnable()
        {
            _shuttingDown = false;
            _live = true;
            Desk.Refresh();
        }

        /// <summary>
        /// O caixa está saindo de operação — desligado, removido ou a cena
        /// morrendo. Este é o momento em que o caso "jogador arranca a
        /// registradora no meio do expediente" é resolvido: ainda somos um
        /// objeto válido, então cada cliente recebe um aviso coerente e decide
        /// sozinho. Depois daqui, ninguém segura referência para nós.
        ///
        /// Os dois sinalizadores são escritos ANTES do Shutdown de propósito: o
        /// balcão pergunta "estou operante?" durante o encerramento, e a
        /// resposta precisa já ser não.
        /// </summary>
        private void OnDisable()
        {
            _shuttingDown = true;
            _live = false;

            Desk.Shutdown();
        }

        private void Update()
        {
            // A fila é a única estrutura que guarda clientes entre frames, e um
            // cliente pode morrer por fora (troca de cena, jogador apagando o
            // objeto) sem passar pelo LeaveQueue. Uma varredura por frame numa
            // lista de no máximo oito é mais barata que um evento de morte por
            // NPC — e não depende de ninguém lembrar de chamá-lo.
            Desk.PruneDead();

            if (_autoServeSeconds <= 0f || Desk.CurrentSession == null) return;

            _sessionAge += Time.deltaTime;
            if (_sessionAge >= _autoServeSeconds) AutoServe();
        }

        // ── depuração ────────────────────────────────────────────────────────

        /// <summary>
        /// Passa tudo pelo leitor, paga e fecha. É o atalho que permite verificar
        /// o ciclo completo na Sandbox antes de a interface do caixa existir.
        /// Sai da cena junto com a Fase 5b.
        /// </summary>
        [ContextMenu("Depuração/Concluir venda agora")]
        public void AutoServe()
        {
            // Zerado aqui, e não só no sucesso: sem isto, uma cena sem
            // ICheckoutService tentaria concluir a venda a cada frame e encheria
            // o console de avisos.
            _sessionAge = 0f;

            CheckoutSession session = Desk.CurrentSession;
            if (session == null)
            {
                Debug.Log("[CheckoutStation] Nenhuma venda aberta.", this);
                return;
            }

            while (session.TryScanNext(out _)) { }

            // Só completa o que falta. O cliente normalmente já deixou uma
            // cédula na mão (¥1000 para ¥730) — sobrescrever com o valor exato
            // apagaria o troco e esconderia justamente o que a Fase 5b precisa
            // exercitar.
            if (session.Method == PaymentMethod.Cash && session.AmountTendered < session.Total)
                session.SetAmountTendered(session.Total);

            Money total = session.Total;
            int items = session.Lines.Count;

            ICheckoutService service = Service;
            if (service != null && service.TryCompleteSale(this))
            {
                Debug.Log($"[CheckoutStation] Venda concluída: {items} item(ns), {total}.", this);
                return;
            }

            Debug.LogWarning("[CheckoutStation] Não foi possível concluir a venda " +
                             "(sem ICheckoutService?).", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Mexer no limite pelo inspetor durante o Play tem que chegar ao
            // balcão; senão o campo mente.
            if (_desk != null) _desk.MaxQueueLength = _maxQueueLength;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f);

            Vector3 anchor = QueueAnchor;
            Vector3 direction = QueueDirection;
            int preview = _maxQueueLength > 0 ? Mathf.Min(_maxQueueLength, 6) : 6;

            for (int i = 0; i < preview; i++)
            {
                Vector3 spot = anchor + direction * (_queueSpacing * i);
                Gizmos.DrawWireSphere(spot, 0.2f);
                if (i > 0) Gizmos.DrawLine(spot - direction * _queueSpacing, spot);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(CounterPosition, new Vector3(0.4f, 0.1f, 0.4f));
        }
    }
}
