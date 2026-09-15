using System;
using System.Collections.Generic;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    /// <summary>
    /// A máquina de estados é C# puro, então o comportamento do NPC pode ser
    /// verificado sem abrir cena, sem NavMesh e sem Play Mode.
    ///
    /// Os dois testes que mais importam aqui são o da prioridade das transições
    /// globais e o da saída garantida: são eles que descrevem, em código
    /// executável, por que o NPC não fica mais preso quando uma prateleira some
    /// no meio da compra, e por que ele nunca mais vaza uma reserva de slot.
    /// </summary>
    public sealed class StateMachineTests
    {
        private sealed class Ctx
        {
            public readonly List<string> Log = new();
            public bool GoToB;
            public bool Emergency;
        }

        /// <summary>
        /// Base dos estados de teste: só anota o que aconteceu. Abstrata, e não
        /// selada, porque as três subclasses abaixo existem apenas para dar
        /// TIPOS distintos à máquina — ela indexa estados por
        /// <c>GetType()</c>, então "A", "B" e "emergência" precisam ser classes
        /// diferentes, não três instâncias da mesma.
        /// </summary>
        private abstract class Recorder : IState<Ctx>
        {
            private readonly string _name;
            protected Recorder(string name) => _name = name;

            public void Enter(Ctx c) => c.Log.Add($"enter:{_name}");
            public void Tick(Ctx c, float dt) => c.Log.Add($"tick:{_name}");
            public void Exit(Ctx c) => c.Log.Add($"exit:{_name}");
        }

        private sealed class StateA : Recorder { public StateA() : base("A") { } }
        private sealed class StateB : Recorder { public StateB() : base("B") { } }
        private sealed class StateEmergency : Recorder { public StateEmergency() : base("E") { } }

        private static StateMachine<Ctx> Build(Ctx context)
        {
            var machine = new StateMachine<Ctx>(context);
            machine.Add(new StateA()).Add(new StateB()).Add(new StateEmergency());
            return machine;
        }

        [Test]
        public void Start_entra_no_estado_inicial()
        {
            var ctx = new Ctx();
            StateMachine<Ctx> machine = Build(ctx);

            machine.Start<StateA>();

            Assert.AreEqual(typeof(StateA), machine.CurrentStateType);
            CollectionAssert.AreEqual(new[] { "enter:A" }, ctx.Log);
        }

        [Test]
        public void Transicao_sai_do_antigo_antes_de_entrar_no_novo()
        {
            var ctx = new Ctx();
            StateMachine<Ctx> machine = Build(ctx);
            machine.AddTransition<StateA, StateB>(c => c.GoToB);

            machine.Start<StateA>();
            ctx.Log.Clear();

            ctx.GoToB = true;
            machine.Tick(0.1f);

            CollectionAssert.AreEqual(new[] { "exit:A", "enter:B", "tick:B" }, ctx.Log,
                "A ordem importa: o Exit precisa rodar antes do Enter do próximo, " +
                "senão o novo estado reserva antes de o antigo liberar.");
        }

        [Test]
        public void Transicao_global_tem_prioridade_sobre_a_normal()
        {
            // O caso real: a prateleira sumiu enquanto o cliente caminhava até
            // ela. A emergência precisa ganhar da transição normal do estado.
            var ctx = new Ctx { GoToB = true, Emergency = true };
            StateMachine<Ctx> machine = Build(ctx);

            machine.AddTransition<StateA, StateB>(c => c.GoToB);
            machine.AddAnyTransition<StateEmergency>(c => c.Emergency);

            machine.Start<StateA>();
            machine.Tick(0.1f);

            Assert.AreEqual(typeof(StateEmergency), machine.CurrentStateType);
        }

        [Test]
        public void Transicao_global_nao_reentra_no_estado_atual()
        {
            var ctx = new Ctx { Emergency = true };
            StateMachine<Ctx> machine = Build(ctx);
            machine.AddAnyTransition<StateEmergency>(c => c.Emergency);

            machine.Start<StateEmergency>();
            ctx.Log.Clear();

            machine.Tick(0.1f);
            machine.Tick(0.1f);

            CollectionAssert.DoesNotContain(ctx.Log, "enter:E",
                "Condição ainda verdadeira não pode reiniciar o estado a cada frame.");
        }

        [Test]
        public void Stop_chama_o_Exit_do_estado_corrente()
        {
            // Esta é a garantia contra vazamento de reserva: seja qual for o
            // motivo do fim — despawn, troca de cena, objeto destruído — o Exit
            // roda, e é nele que o slot é liberado.
            var ctx = new Ctx();
            StateMachine<Ctx> machine = Build(ctx);

            machine.Start<StateA>();
            ctx.Log.Clear();

            machine.Stop();

            CollectionAssert.AreEqual(new[] { "exit:A" }, ctx.Log);
            Assert.IsFalse(machine.IsRunning);
        }

        [Test]
        public void Stop_duas_vezes_nao_chama_Exit_duas_vezes()
        {
            var ctx = new Ctx();
            StateMachine<Ctx> machine = Build(ctx);
            machine.Start<StateA>();
            ctx.Log.Clear();

            machine.Stop();
            machine.Stop();

            Assert.AreEqual(1, ctx.Log.Count);
        }

        [Test]
        public void GoTo_transiciona_direto()
        {
            var ctx = new Ctx();
            StateMachine<Ctx> machine = Build(ctx);
            machine.Start<StateA>();

            machine.GoTo<StateB>();

            Assert.AreEqual(typeof(StateB), machine.CurrentStateType);
        }

        [Test]
        public void Transicao_para_estado_nao_registrado_lanca_com_o_nome()
        {
            var ctx = new Ctx { GoToB = true };
            var machine = new StateMachine<Ctx>(ctx);
            machine.Add(new StateA());                       // StateB de fora
            machine.AddTransition<StateA, StateB>(c => c.GoToB);

            machine.Start<StateA>();

            var ex = Assert.Throws<InvalidOperationException>(() => machine.Tick(0.1f));
            StringAssert.Contains(nameof(StateB), ex.Message);
        }

        [Test]
        public void Tick_sem_Start_nao_faz_nada()
        {
            var ctx = new Ctx();
            StateMachine<Ctx> machine = Build(ctx);

            Assert.DoesNotThrow(() => machine.Tick(0.1f));
            Assert.AreEqual(0, ctx.Log.Count);
        }

        [Test]
        public void StateChanged_reporta_de_e_para()
        {
            var ctx = new Ctx();
            StateMachine<Ctx> machine = Build(ctx);
            machine.AddTransition<StateA, StateB>(c => c.GoToB);

            Type from = null, to = null;
            machine.StateChanged += (f, t) => { from = f; to = t; };

            machine.Start<StateA>();
            Assert.IsNull(from, "Na entrada inicial não há estado anterior.");
            Assert.AreEqual(typeof(StateA), to);

            ctx.GoToB = true;
            machine.Tick(0.1f);

            Assert.AreEqual(typeof(StateA), from);
            Assert.AreEqual(typeof(StateB), to);
        }
    }
}
