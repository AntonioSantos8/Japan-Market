using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Fachada e ciclo de vida de um cliente. Não decide nada e não move nada:
    /// junta as peças, entrega os serviços e repassa o tempo.
    ///
    /// Substitui o NpcTraject e o NpcInstance juntos. O que sumiu, e por quê:
    ///  • a corrotina de compras virou máquina de estados (CustomerBrain);
    ///  • o DOTween no transform virou animação (CustomerAnimation);
    ///  • o polling de 0,5 s no caixa virou sinal (<see cref="Notify"/>);
    ///  • o GameObject.FindGameObjectWithTag("Exit") virou parâmetro de spawn;
    ///  • o ServiceLocator.Get&lt;CashRegister&gt;() cacheado no Start virou
    ///    consulta ao registro, feita no momento em que ela importa.
    /// </summary>
    [RequireComponent(typeof(CustomerLocomotion))]
    [RequireComponent(typeof(CustomerBasket))]
    [DisallowMultipleComponent]
    public sealed class CustomerAgent : MonoBehaviour, ICustomer
    {
        [SerializeField] private CustomerProfileData _profile;

        [Header("Depuração")]
        [Tooltip("Escreve no console cada troca de estado. Essencial na Sandbox.")]
        [SerializeField] private bool _logStateChanges;

        private static int _nextId = 1;

        private CustomerContext _context;
        private CustomerBrain _brain;
        private bool _initialized;

        public int Id { get; private set; }
        public Vector3 Position => transform.position;
        public bool IsAlive => this != null && _initialized && !_context.ReadyToDespawn;

        public CustomerLocomotion Locomotion { get; private set; }
        public CustomerBasket Basket { get; private set; }
        public CustomerAnimation Animation { get; private set; }
        public CustomerProfileData Profile => _profile;

        /// <summary>Nome do estado atual — usado pelo gizmo e pelo log.</summary>
        public string CurrentStateName => _brain?.CurrentStateType?.Name ?? "—";

        // ── montagem ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Id = _nextId++;

            Locomotion = GetComponent<CustomerLocomotion>();
            Basket = GetComponent<CustomerBasket>();
            Animation = GetComponent<CustomerAnimation>();
        }

        /// <summary>
        /// Chamado pelo spawner antes de o cliente começar a agir. Os pontos de
        /// entrada e saída vêm de fora de propósito: o NPC não deve sair
        /// procurando objetos por tag na cena.
        /// </summary>
        public void Initialize(Vector3 entryPoint, Vector3 exitPoint,
                               CustomerProfileData profile = null)
        {
            if (profile != null) _profile = profile;

            if (_profile == null)
            {
                Debug.LogError("[CustomerAgent] Sem CustomerProfileData. O cliente não " +
                               "sabe quanto comprar nem quanto aceita pagar.", this);
                enabled = false;
                return;
            }

            GameContext game = GameContext.Current;
            if (game == null)
            {
                Debug.LogError("[CustomerAgent] Não há GameContext na cena. Sem ele o " +
                               "cliente não encontra prateleiras nem caixas.", this);
                enabled = false;
                return;
            }

            Locomotion.Configure(_profile.MoveSpeed, _profile.TurnSpeedDegrees);

            _context = BuildContext(game, entryPoint, exitPoint);
            _brain = new CustomerBrain(_context);

            if (_logStateChanges)
                _brain.StateChanged += (from, to) =>
                    Debug.Log($"[Cliente {Id}] {from?.Name ?? "—"} → {to.Name}", this);

            _initialized = true;
            _brain.Start();

            game.Events.Publish(new CustomerEntered(Id, transform));
        }

        private CustomerContext BuildContext(GameContext game, Vector3 entry, Vector3 exit)
        {
            IServiceContainer services = game.Services;

            var context = new CustomerContext
            {
                Agent = this,
                Locomotion = Locomotion,
                Animation = Animation,
                Basket = Basket,
                Profile = _profile,
                Events = game.Events,
                Furniture = services.Resolve<IFurnitureRegistry>(),
                EntryPoint = entry,
                ExitPoint = exit,
            };

            // Serviços que ainda não existem em todas as fases entram por
            // TryResolve: ausente é um estado válido, não um erro.
            services.TryResolve(out IPricingService pricing);
            context.Pricing = pricing;

            services.TryResolve(out IStoreCleanliness cleanliness);
            context.Cleanliness = cleanliness;

            services.TryResolve(out ICheckoutService checkout);
            context.Checkout = checkout;

            return context;
        }

        // ── execução ─────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_initialized) return;

            _brain.Tick(Time.deltaTime);

            if (_context.ReadyToDespawn) Despawn();
        }

        private void Despawn()
        {
            _initialized = false;
            Destroy(gameObject);
        }

        /// <summary>
        /// Aqui está a garantia que faltava no sistema antigo: seja qual for o
        /// motivo da destruição — despawn normal, troca de cena, jogador
        /// apagando o objeto — o Stop chama o Exit do estado corrente, e o Exit
        /// libera o slot reservado. Não existe caminho de morte que vaze.
        /// </summary>
        private void OnDestroy()
        {
            if (_brain == null) return;

            _brain.Stop();
            _context?.ReleaseReservation();

            // O Stop acima já chamou o Exit do estado corrente, mas o Exit do
            // QueueingState não sai da fila de propósito (ver ReleaseStation).
            // Sem esta linha, um cliente destruído por fora — troca de cena,
            // jogador apagando o objeto — ficaria pendurado na fila até o
            // PruneDead da estação notar.
            _context?.ReleaseStation();
        }

        // ── ICustomer ────────────────────────────────────────────────────────

        public void Notify(CustomerSignal signal)
        {
            if (!_initialized) return;

            switch (signal)
            {
                case CustomerSignal.StoreClosed:
                    _context.StoreClosed = true;
                    break;

                case CustomerSignal.CheckoutLost:
                    _context.CheckoutLost = true;
                    break;

                case CustomerSignal.SaleFinished:
                    _context.SaleFinished = true;
                    break;
            }
        }

        // ── depuração ────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (!_initialized) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position + Vector3.up, _context.ExitPoint + Vector3.up);

            if (_context.Reservation is { IsValid: true })
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_context.Reservation.WorldPosition, 0.2f);
            }
        }
    }
}
