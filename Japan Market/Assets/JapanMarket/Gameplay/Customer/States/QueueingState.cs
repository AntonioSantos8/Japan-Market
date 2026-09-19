using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Na fila do caixa. O estado onde o jitter era mais visível.
    ///
    /// Três diferenças em relação ao que existe hoje. Primeira: ele para de
    /// verdade — Halt limpa o caminho, zera a velocidade e desliga o avoidance,
    /// então nada empurra o cliente enquanto ele espera. Segunda: ele só se
    /// move de novo se a posição da fila REALMENTE mudou. Hoje o
    /// RefreshQueuePositions chama SetTarget em todo mundo sempre que alguém
    /// entra ou sai, e quem já estava parado no lugar certo recebe um
    /// isStopped = false seguido de SetDestination — anda alguns centímetros,
    /// para de novo, e a animação pisca entre Idle e Walk. Terceira: a posição
    /// dele na fila é CONSULTADA, não recebida; um aviso pode se perder ou
    /// chegar fora de ordem, uma consulta não.
    ///
    /// A "inquietação" de quem espera não sumiu: ela virou variação de idle no
    /// Animator, onde não disputa a posição com o NavMeshAgent.
    /// </summary>
    public sealed class QueueingState : CustomerStateBase
    {
        private const float RepositionThresholdSqr = 0.04f;   // 20 cm

        private Vector3 _slotPosition;

        public override void Enter(CustomerContext context)
        {
            context.SaleFinished = false;
            context.QueueSettled = false;

            if (context.StationLost) return;

            // Entrar na fila pode falhar — ela encheu entre a escolha e a
            // chegada. Nesse caso ele volta a procurar, em vez de ficar parado
            // atrás de uma fila da qual não faz parte.
            if (!context.Station.TryJoinQueue(context.Agent, out int index))
            {
                context.CheckoutLost = true;
                return;
            }

            // Conseguiu entrar: a busca por caixa deu certo e o cronômetro de
            // desistência recomeça do zero na próxima vez que ele precisar
            // procurar.
            context.CheckoutSearchTime = 0f;

            context.QueueIndex = index;
            _slotPosition = context.Station.GetQueuePosition(index);

            if (!context.Locomotion.MoveTo(_slotPosition))
            {
                // Fora da malha: não dá nem para chegar ao lugar. Procura outro
                // caixa em vez de "entrar na fila" de onde estiver parado.
                context.CheckoutLost = true;
            }
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.StationLost) return;

            // Sem caminho até o lugar dele — slot fora da malha, ou outro agente
            // plantado em cima. Isto NÃO pode ser só esperar: se ele é o
            // primeiro da fila, QueueSettled nunca vira true, MyTurn nunca é
            // verdade, e nenhuma venda abre nesta estação enquanto ele estiver
            // aqui. Com QueuePatience = 0 no perfil ("espera para sempre"), o
            // caixa ficaria travado indefinidamente.
            if (context.Locomotion.PathFailed)
            {
                context.CheckoutLost = true;
                return;
            }

            int index = context.Station.GetQueueIndex(context.Agent);
            if (index < 0)
            {
                // Sumiu da fila sem passar por aqui: a estação o removeu. Trata
                // como perda de caixa e deixa a tabela decidir.
                context.CheckoutLost = true;
                return;
            }

            context.QueueIndex = index;

            Vector3 target = context.Station.GetQueuePosition(index);

            if ((target - _slotPosition).sqrMagnitude > RepositionThresholdSqr)
            {
                // A fila andou de verdade.
                _slotPosition = target;
                context.QueueSettled = false;
                context.Locomotion.MoveTo(target);
                return;
            }

            // Cansou de esperar? Registra o motivo; a transição global cuida
            // de levá-lo para o estado de frustração — condição de tabela não
            // tem efeito colateral, quem tem é o Tick.
            if (OutOfPatience(context)) context.Frustrate(CustomerLeaveReason.WaitedTooLong);

            if (context.QueueSettled) return;

            // IsHalted faz HasArrived devolver true na hora. Sem este teste, um
            // MoveTo que falhou no Enter (cliente fora da malha) faria ele
            // "entrar na fila" de onde estivesse parado.
            if (context.Locomotion.IsHalted || !context.Locomotion.HasArrived) return;

            // Chegou: para e encara o balcão. A partir daqui não há mais nenhuma
            // escrita em posição enquanto a fila não mudar.
            context.QueueSettled = true;
            HaltFacing(context, context.Station.CounterPosition);
        }

        // ── condições da tabela ──────────────────────────────────────────────

        /// <summary>
        /// É a vez dele: está na frente e já parou no lugar.
        ///
        /// Exigir <see cref="CustomerContext.QueueSettled"/> evita o caso em que
        /// o primeiro da fila ainda está a cinco metros do balcão — porque
        /// entrou numa fila vazia — e abriria a venda de longe.
        /// </summary>
        public static bool MyTurn(CustomerContext context) =>
            context.QueueSettled
            && !context.StationLost
            && context.Station.IsFront(context.Agent);

        public static bool LostStation(CustomerContext context) =>
            context.StationLost || context.CheckoutLost;

        /// <summary>Cansou de esperar. Paciência 0 no perfil = espera para sempre.</summary>
        public static bool OutOfPatience(CustomerContext context)
        {
            if (context.Profile == null) return false;

            float patience = context.Profile.QueuePatience;
            return patience > 0f && context.StateTime >= patience;
        }
    }
}
