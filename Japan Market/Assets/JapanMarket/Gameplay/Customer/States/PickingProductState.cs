using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Parado na frente da prateleira, pegando unidades.
    ///
    /// Este é o estado em que o cliente fica imóvel por segundos — ou seja,
    /// onde o jitter aparecia. Aqui ele para de verdade (Halt zera velocidade,
    /// limpa o caminho e desliga o avoidance) e a única coisa que se move é a
    /// rotação, uma vez, para encarar a prateleira.
    /// </summary>
    public sealed class PickingProductState : CustomerStateBase
    {
        private int _unitsWanted;
        private int _unitsTaken;
        private float _nextPickAt;

        public override void Enter(CustomerContext context)
        {
            _unitsTaken = 0;
            _unitsWanted = context.Profile != null ? context.Profile.RollUnitsPerShelf() : 1;

            float duration = context.Profile != null ? context.Profile.RollBrowseDuration() : 2f;
            context.WaitUntil = duration;

            // Pega as unidades espaçadas ao longo da pausa, não todas de uma vez.
            _nextPickAt = duration * 0.35f;

            if (context.TargetShelfLost) return;

            HaltFacing(context, context.TargetShelf.Owner.Position);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.TargetShelfLost) return;
            if (_unitsTaken >= _unitsWanted) return;
            if (context.StateTime < _nextPickAt) return;

            TakeOne(context);

            float remaining = context.WaitUntil - context.StateTime;
            int left = _unitsWanted - _unitsTaken;
            _nextPickAt = context.StateTime + (left > 0 ? remaining / (left + 1) : remaining);
        }

        private void TakeOne(CustomerContext context)
        {
            if (context.BasketIsFull) { _unitsTaken = _unitsWanted; return; }

            ItemDefinition product = context.TargetShelf.CurrentProduct;
            if (product == null) { _unitsTaken = _unitsWanted; return; }

            // O preço é conferido de novo aqui: o jogador pode ter remarcado a
            // etiqueta entre o cliente escolher a prateleira e chegar nela.
            if (!context.AcceptsPrice(product, out Money price))
            {
                context.Frustrate(CustomerLeaveReason.PricesTooHigh);
                _unitsTaken = _unitsWanted;
                return;
            }

            if (!context.TargetShelf.TryTakeOne(out ItemDefinition taken))
            {
                _unitsTaken = _unitsWanted;   // esvaziou na mão dele
                return;
            }

            context.Basket.Add(taken, price);
            _unitsTaken++;

            context.Animation?.SetCarrying(true);
        }

        public override void Exit(CustomerContext context)
        {
            // Terminou com esta prateleira: libera o slot AQUI, no Exit, por
            // qualquer caminho de saída. É isso que torna o vazamento de slot
            // do FurnitureOccupancy impossível de reproduzir.
            context.ClearShelfTarget();
            context.ShelvesRemaining--;
        }

        public static bool Finished(CustomerContext context) =>
            context.StateTime >= context.WaitUntil || context.TargetShelfLost;
    }
}
