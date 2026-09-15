using System;
using JapanMarket.Core;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Monta e roda a máquina de estados do cliente.
    ///
    /// A tabela abaixo é o documento mais importante do sistema de NPC: ela é a
    /// lista COMPLETA de como um cliente pode se mover entre situações. No
    /// código atual essa informação não existe em lugar nenhum — está diluída
    /// numa corrotina de 60 linhas e em seis flags, e a única forma de saber se
    /// um caso foi tratado é ler tudo e simular de cabeça.
    /// </summary>
    public sealed class CustomerBrain
    {
        private readonly CustomerContext _context;
        private readonly StateMachine<CustomerContext> _machine;

        public CustomerBrain(CustomerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _machine = new StateMachine<CustomerContext>(context);

            RegisterStates();
            RegisterTransitions();

            _machine.StateChanged += (_, __) => _context.StateTime = 0f;
        }

        public Type CurrentStateType => _machine.CurrentStateType;
        public bool IsRunning => _machine.IsRunning;

        public event Action<Type, Type> StateChanged
        {
            add => _machine.StateChanged += value;
            remove => _machine.StateChanged -= value;
        }

        public void Start() => _machine.Start<EnteringState>();
        public void Stop() => _machine.Stop();

        public void Tick(float deltaTime)
        {
            _context.StateTime += deltaTime;

            // O mundo é observado ANTES da máquina: assim uma loja que ficou
            // suja ou fechou é notada no mesmo frame, e não no próximo.
            WatchWorld();

            _machine.Tick(deltaTime);
        }

        /// <summary>
        /// Condições do ambiente que valem em qualquer estado.
        ///
        /// Fica aqui, e não dentro das condições da tabela, porque estas TÊM
        /// efeito colateral (registram o motivo). Condição de transição precisa
        /// ser pura — se ela mudar o mundo, avaliar a tabela duas vezes dá
        /// resultados diferentes.
        /// </summary>
        private void WatchWorld()
        {
            if (_context.IsLeaving) return;

            if (_context.StoreIsTooDirty)
                _context.Frustrate(CustomerLeaveReason.StoreTooDirty);
        }

        private void RegisterStates()
        {
            _machine
                .Add(new EnteringState())
                .Add(new BrowsingState())
                .Add(new ApproachingShelfState())
                .Add(new PickingProductState())
                .Add(new SeekingCheckoutState())
                .Add(new QueueingState())
                .Add(new AtCounterState())
                .Add(new FrustratedState())
                .Add(new LeavingState());
        }

        /// <summary>
        /// ─────────────────────────────────────────────────────────────────────
        ///  GLOBAIS (avaliadas antes de tudo, valem de qualquer estado)
        ///    *                 → Frustrated   motivo registrado, ainda não reagiu, não pagou
        ///    *                 → Leaving      loja fechou
        ///
        ///  NORMAIS
        ///    Entering          → Browsing         chegou e cumpriu a pausa
        ///    Browsing          → ApproachingShelf achou prateleira com slot livre
        ///    Browsing          → SeekingCheckout  cesta cheia ou lista terminada
        ///    ApproachingShelf  → PickingProduct   chegou no slot
        ///    ApproachingShelf  → Browsing         perdeu o alvo ou o caminho
        ///    PickingProduct    → Browsing         pegou o que queria
        ///    SeekingCheckout   → Queueing         achou caixa operante
        ///    Queueing          → AtCounter        é o primeiro e já parou
        ///    Queueing          → SeekingCheckout  o caixa sumiu
        ///    AtCounter         → Leaving          pagou
        ///    AtCounter         → SeekingCheckout  o caixa sumiu no meio
        ///    Frustrated        → Leaving          terminou de reclamar
        /// ─────────────────────────────────────────────────────────────────────
        /// </summary>
        private void RegisterTransitions()
        {
            _machine
                // `!c.SaleFinished` não é detalhe. A global é avaliada ANTES das
                // transições normais, então sem ela a ordem cuidadosa de
                // AtCounter (Paid antes de LostStation) seria contornada por
                // cima: um cliente que estourou a paciência no mesmo frame em
                // que a venda fechou iria para Frustrated, a loja receberia o
                // dinheiro, e ele sairia publicado como insatisfeito.
                .AddAnyTransition<FrustratedState>(c =>
                    c.PendingFrustration.HasValue && !c.FrustrationDone
                    && !c.IsLeaving && !c.SaleFinished)
                .AddAnyTransition<LeavingState>(c => c.StoreClosed);

            _machine
                .AddTransition<EnteringState, BrowsingState>(EnteringState.Finished)

                .AddTransition<BrowsingState, ApproachingShelfState>(BrowsingState.FoundShelf)
                .AddTransition<BrowsingState, SeekingCheckoutState>(BrowsingState.DoneShopping)

                .AddTransition<ApproachingShelfState, BrowsingState>(ApproachingShelfState.LostTarget)
                .AddTransition<ApproachingShelfState, PickingProductState>(ApproachingShelfState.Arrived)

                .AddTransition<PickingProductState, BrowsingState>(PickingProductState.Finished)

                .AddTransition<SeekingCheckoutState, QueueingState>(SeekingCheckoutState.FoundStation)

                // Paid ANTES de LostStation: se a venda fecha no mesmo frame em
                // que o jogador arranca o caixa, as duas condições ficam
                // verdadeiras — e Evaluate devolve a primeira. Na ordem inversa
                // o cliente já pago voltava a procurar caixa e ia embora
                // marcado como insatisfeito.
                .AddTransition<QueueingState, AtCounterState>(QueueingState.MyTurn)
                .AddTransition<QueueingState, SeekingCheckoutState>(QueueingState.LostStation)

                // Mesma ordem, e pelo mesmo motivo: a venda fechada tem
                // prioridade sobre o caixa ter sumido no mesmo frame. Quem já
                // pagou vai embora satisfeito, não volta a procurar caixa.
                .AddTransition<AtCounterState, LeavingState>(AtCounterState.Paid)
                .AddTransition<AtCounterState, SeekingCheckoutState>(AtCounterState.LostStation)

                .AddTransition<FrustratedState, LeavingState>(FrustratedState.ReactionOver);
        }
    }
}
