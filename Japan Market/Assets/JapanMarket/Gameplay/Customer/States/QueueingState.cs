using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Na fila do caixa. O estado onde o jitter era mais visível.
    ///
    /// Duas diferenças em relação ao que existe hoje. Primeira: ele para de
    /// verdade — Halt limpa o caminho, zera a velocidade e desliga o avoidance,
    /// então nada empurra o cliente enquanto ele espera. Segunda: ele só se
    /// move de novo se a posição da fila REALMENTE mudou. Hoje o
    /// RefreshQueuePositions chama SetTarget em todo mundo sempre que alguém
    /// entra ou sai, e quem já estava parado no lugar certo recebe um
    /// isStopped = false seguido de SetDestination — anda alguns centímetros,
    /// para de novo, e a animação pisca entre Idle e Walk.
    ///
    /// A "inquietação" de quem espera não sumiu: ela virou variação de idle no
    /// Animator, onde não disputa a posição com o NavMeshAgent.
    /// </summary>
    public sealed class QueueingState : CustomerStateBase
    {
        private const float RepositionThresholdSqr = 0.04f;   // 20 cm

        private Vector3 _slotPosition;
        private bool _settled;

        public override void Enter(CustomerContext context)
        {
            context.SaleFinished = false;
            _settled = false;

            if (context.StationLost) return;

            _slotPosition = SlotPositionFor(context);
            context.Locomotion.MoveTo(_slotPosition);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.StationLost) return;

            Vector3 target = SlotPositionFor(context);

            if ((target - _slotPosition).sqrMagnitude > RepositionThresholdSqr)
            {
                // A fila andou de verdade.
                _slotPosition = target;
                _settled = false;
                context.Locomotion.MoveTo(target);
                return;
            }

            // Cansou de esperar? Registra o motivo; a transição global cuida
            // de levá-lo para o estado de frustração — condição de tabela não
            // tem efeito colateral, quem tem é o Tick.
            if (OutOfPatience(context)) context.Frustrate(CustomerLeaveReason.WaitedTooLong);

            if (_settled) return;

            // IsHalted faz HasArrived devolver true na hora. Sem este teste, um
            // MoveTo que falhou no Enter (cliente fora da malha) faria ele
            // "entrar na fila" de onde estivesse parado.
            if (context.Locomotion.IsHalted || !context.Locomotion.HasArrived) return;

            // Chegou: para e encara o balcão. A partir daqui não há mais nenhuma
            // escrita em posição enquanto a fila não mudar.
            _settled = true;
            HaltFacing(context, context.Station.CounterPosition);
        }

        private static Vector3 SlotPositionFor(CustomerContext context)
        {
            return context.Station.QueueAnchor
                 + context.Station.QueueDirection.normalized
                 * (context.Station.QueueSpacing * context.QueueIndex);
        }

        public static bool Paid(CustomerContext context) => context.SaleFinished;

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
