using System;
using System.Collections.Generic;

namespace JapanMarket.Domain
{

    public sealed class StateMachine<TContext>
    {
        private readonly TContext _context;
        private readonly Dictionary<Type, IState<TContext>> _states = new();
        private readonly Dictionary<Type, List<Transition>> _transitions = new();
        private readonly List<Transition> _anyTransitions = new();

        private static readonly List<Transition> NoTransitions = new();

        public StateMachine(TContext context) => _context = context;

        public Type CurrentStateType { get; private set; }
        public IState<TContext> CurrentState { get; private set; }
        public bool IsRunning => CurrentState != null;

        public event Action<Type, Type> StateChanged;

        public StateMachine<TContext> Add<TState>(TState state) where TState : IState<TContext>
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            _states[state.GetType()] = state;
            return this;
        }

        public StateMachine<TContext> AddTransition<TFrom, TTo>(Func<TContext, bool> when)
            where TFrom : IState<TContext>
            where TTo : IState<TContext>
        {
            if (when == null) throw new ArgumentNullException(nameof(when));

            if (!_transitions.TryGetValue(typeof(TFrom), out List<Transition> list))
            {
                list = new List<Transition>();
                _transitions.Add(typeof(TFrom), list);
            }

            list.Add(new Transition(typeof(TTo), when));
            return this;
        }

        public StateMachine<TContext> AddAnyTransition<TTo>(Func<TContext, bool> when)
            where TTo : IState<TContext>
        {
            if (when == null) throw new ArgumentNullException(nameof(when));
            _anyTransitions.Add(new Transition(typeof(TTo), when));
            return this;
        }

        public void Start<TState>() where TState : IState<TContext> => Enter(typeof(TState));

        public void Tick(float deltaTime)
        {
            if (CurrentState == null) return;

            Type next = Evaluate();
            if (next != null) Enter(next);

            CurrentState?.Tick(_context, deltaTime);
        }

        public void GoTo<TState>() where TState : IState<TContext> => Enter(typeof(TState));

        public void Stop()
        {
            if (CurrentState == null) return;

            CurrentState.Exit(_context);
            CurrentState = null;
            CurrentStateType = null;
        }

        private Type Evaluate()
        {

            for (int i = 0; i < _anyTransitions.Count; i++)
            {
                Transition transition = _anyTransitions[i];
                if (transition.To == CurrentStateType) continue;
                if (transition.Condition(_context)) return transition.To;
            }

            List<Transition> fromCurrent = CurrentStateType != null
                && _transitions.TryGetValue(CurrentStateType, out List<Transition> list)
                ? list : NoTransitions;

            for (int i = 0; i < fromCurrent.Count; i++)
                if (fromCurrent[i].Condition(_context)) return fromCurrent[i].To;

            return null;
        }

        private void Enter(Type stateType)
        {
            if (!_states.TryGetValue(stateType, out IState<TContext> next))
            {
                throw new InvalidOperationException(
                    $"[StateMachine] State '{stateType.Name}' was not registered with Add(). " +
                    "A transition points to a state that does not exist.");
            }

            Type previous = CurrentStateType;

            CurrentState?.Exit(_context);

            CurrentState = next;
            CurrentStateType = stateType;

            StateChanged?.Invoke(previous, stateType);

            CurrentState.Enter(_context);
        }

        private readonly struct Transition
        {
            public readonly Type To;
            public readonly Func<TContext, bool> Condition;

            public Transition(Type to, Func<TContext, bool> condition)
            {
                To = to;
                Condition = condition;
            }
        }
    }
}
