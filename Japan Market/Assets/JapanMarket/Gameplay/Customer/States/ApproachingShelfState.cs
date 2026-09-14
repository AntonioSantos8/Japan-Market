namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Caminha até o slot reservado.
    ///
    /// Reconsulta a posição do slot a cada tick porque ela é calculada ao vivo:
    /// se o jogador mover a prateleira no meio do caminho, o destino acompanha
    /// em vez de o cliente andar até onde ela estava.
    /// </summary>
    public sealed class ApproachingShelfState : CustomerStateBase
    {
        private const float RetargetThresholdSqr = 0.09f;   // 30 cm

        private UnityEngine.Vector3 _lastDestination;

        public override void Enter(CustomerContext context)
        {
            if (context.ReservationLost) return;

            _lastDestination = context.Reservation.WorldPosition;
            context.Locomotion.MoveTo(_lastDestination);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.ReservationLost) return;

            UnityEngine.Vector3 current = context.Reservation.WorldPosition;

            // Só reemite o destino se ele realmente andou. Reemitir todo frame é
            // o que a fila do caixa faz hoje, e é metade da causa do NPC dar
            // passinhos e alternar Idle/Walk sem sair do lugar.
            if ((current - _lastDestination).sqrMagnitude > RetargetThresholdSqr)
            {
                _lastDestination = current;
                context.Locomotion.MoveTo(current);
            }
        }

        // Exit não libera a reserva: quem vem depois (PickingProduct) ainda
        // precisa dela. A liberação é de quem termina com a prateleira.

        public static bool Arrived(CustomerContext context) =>
            !context.ReservationLost && context.Locomotion.HasArrived;

        public static bool LostTarget(CustomerContext context) =>
            context.TargetShelfLost || context.ReservationLost || context.Locomotion.PathFailed;
    }
}
