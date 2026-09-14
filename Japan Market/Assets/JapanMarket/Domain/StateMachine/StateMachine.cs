using System;
using System.Collections.Generic;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Máquina de estados com transições declaradas em tabela.
    ///
    /// Duas propriedades que o "roteiro em corrotina" do NpcTraject não tinha,
    /// e que são a razão de existir desta classe:
    ///
    /// 1. **Transições globais.** Uma condição registrada com
    ///    <see cref="AddAnyTransition"/> vale a partir de QUALQUER estado e é
    ///    avaliada antes das demais. É assim que "a prateleira que eu ia usar
    ///    sumiu" ou "a loja fechou" interrompem o que estiver acontecendo, em
    ///    vez de esperar a corrotina chegar no próximo yield.
    ///
    /// 2. **Saída garantida.** Todo caminho para fora de um estado passa pelo
    ///    Exit dele. Não há como sair sem liberar o que foi reservado.
    ///
    /// Genérica e sem Unity: a mesma classe serve ao NPC agora e aos objetivos
    /// na Fase 8, e roda em teste de unidade.
    /// </summary>
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

        /// <summary>(de, para) — para log e depuração.</summary>
        public event Action<Type, Type> StateChanged;

        // ── montagem ─────────────────────────────────────────────────────────

        public StateMachine<TContext> Add<TState>(TState state) where TState : IState<TContext>
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // GetType() e não typeof(TState): assim registrar por uma variável
            // tipada como IState<T> ainda indexa pela classe concreta.
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

        /// <summary>
        /// Vale a partir de qualquer estado e tem prioridade sobre as demais.
        /// Reserve para o que é emergência: recurso que sumiu, loja que fechou,
        /// caminho que ficou impossível.
        /// </summary>
        public StateMachine<TContext> AddAnyTransition<TTo>(Func<TContext, bool> when)
            where TTo : IState<TContext>
        {
            if (when == null) throw new ArgumentNullException(nameof(when));
            _anyTransitions.Add(new Transition(typeof(TTo), when));
            return this;
        }

        // ── execução ─────────────────────────────────────────────────────────

        public void Start<TState>() where TState : IState<TContext> => Enter(typeof(TState));

        public void Tick(float deltaTime)
        {
            if (CurrentState == null) return;

            Type next = Evaluate();
            if (next != null) Enter(next);

            CurrentState?.Tick(_context, deltaTime);
        }

        /// <summary>
        /// Transição direta, decidida pelo próprio estado. Use quando a decisão
        /// depende de algo que só o estado sabe; para condições observáveis de
        /// fora, prefira declarar na tabela.
        /// </summary>
        public void GoTo<TState>() where TState : IState<TContext> => Enter(typeof(TState));

        /// <summary>Encerra a máquina chamando o Exit do estado corrente.</summary>
        public void Stop()
        {
            if (CurrentState == null) return;

            CurrentState.Exit(_context);
            CurrentState = null;
            CurrentStateType = null;
        }

        // ── internos ─────────────────────────────────────────────────────────

        private Type Evaluate()
        {
            // Globais primeiro: uma emergência não espera a transição normal.
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
                    $"[StateMachine] Estado '{stateType.Name}' não foi registrado com Add(). " +
                    "Uma transição aponta para um estado que não existe.");
            }

            Type previous = CurrentStateType;

            CurrentState?.Exit(_context);

            CurrentState = next;
            CurrentStateType = stateType;

            // O aviso sai ANTES do Enter, de propósito. Quem escuta costuma
            // estar zerando o cronômetro do estado — e se isso acontecesse
            // depois, todo Enter leria o tempo do estado ANTERIOR. Um estado que
            // decide "espero 5 segundos antes de desistir" começaria já vencido
            // se viesse de um estado longo.
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
