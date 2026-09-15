using System;
using JapanMarket.Core;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{

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

            WatchWorld();

            _machine.Tick(deltaTime);
        }

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

        private void RegisterTransitions()
        {
            _machine

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

                .AddTransition<QueueingState, AtCounterState>(QueueingState.MyTurn)
                .AddTransition<QueueingState, SeekingCheckoutState>(QueueingState.LostStation)

                .AddTransition<AtCounterState, LeavingState>(AtCounterState.Paid)
                .AddTransition<AtCounterState, SeekingCheckoutState>(AtCounterState.LostStation)

                .AddTransition<FrustratedState, LeavingState>(FrustratedState.ReactionOver);
        }
    }
}
