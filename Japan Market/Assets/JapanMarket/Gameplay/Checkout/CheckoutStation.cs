using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    [DisallowMultipleComponent]
    public sealed class CheckoutStation : FurnitureCapabilityBehaviour, ICheckoutStation
    {
        [Header("Queue")]
        [Tooltip("Where the first in line stops. If empty, uses the front of the furniture.")]
        [SerializeField] private Transform _queueAnchor;

        [Tooltip("Which direction the queue grows. If empty, grows behind the furniture.")]
        [SerializeField] private Transform _queueDirectionMarker;

        [Min(0.4f)]
        [Tooltip("Distance between a customer and the next one.")]
        [SerializeField] private float _queueSpacing = 1.1f;

        [Min(0)]
        [Tooltip("Maximum queue length. 0 = unlimited.")]
        [SerializeField] private int _maxQueueLength = 8;

        [Header("Counter")]
        [Tooltip("Where the customer places their purchases. If empty, uses the center of the furniture.")]
        [SerializeField] private Transform _counterPoint;

        [Header("Operation")]
        [Tooltip("Uncheck to close this checkout without removing it from the store.")]
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

        public CheckoutStationState State => Desk.State;

        public bool IsOperational =>
            _open && _live && !_shuttingDown && Owner != null && Owner.IsAlive;

        public bool AcceptsNewCustomers => Desk.AcceptsNewCustomers;

        public event Action<ICheckoutStation> StateChanged;
        public event Action<ICheckoutStation, CheckoutSession> SessionOpened;
        public event Action<ICheckoutStation, CheckoutSession, SessionCloseReason> SessionClosed;

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

            _live = enabled && gameObject.activeInHierarchy;

            Desk.Refresh();
        }

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

        private void OnDisable()
        {
            _shuttingDown = true;
            _live = false;

            Desk.Shutdown();
        }

        private void Update()
        {

            Desk.PruneDead();

            if (_autoServeSeconds <= 0f || Desk.CurrentSession == null) return;

            _sessionAge += Time.deltaTime;
            if (_sessionAge >= _autoServeSeconds) AutoServe();
        }

        [ContextMenu("Debug/Complete sale now")]
        public void AutoServe()
        {

            _sessionAge = 0f;

            CheckoutSession session = Desk.CurrentSession;
            if (session == null)
            {
                Debug.Log("[CheckoutStation] No open sale.", this);
                return;
            }

            while (session.TryScanNext(out _)) { }

            if (session.Method == PaymentMethod.Cash && session.AmountTendered < session.Total)
                session.SetAmountTendered(session.Total);

            Money total = session.Total;
            int items = session.Lines.Count;

            ICheckoutService service = Service;
            if (service != null && service.TryCompleteSale(this))
            {
                Debug.Log($"[CheckoutStation] Sale completed: {items} item(s), {total}.", this);
                return;
            }

            Debug.LogWarning("[CheckoutStation] Não foi possível concluir a venda " +
                             "(sem ICheckoutService?).", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {

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
