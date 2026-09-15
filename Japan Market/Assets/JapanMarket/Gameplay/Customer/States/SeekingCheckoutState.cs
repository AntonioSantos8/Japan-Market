using JapanMarket.Core;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Procura um caixa para pagar.
    ///
    /// O cliente nunca guarda referência a um caixa específico: ele pergunta ao
    /// <see cref="ICheckoutService"/> qual é a melhor estação agora, e volta
    /// para cá se aquela sumir. É essa indireção que faz "remover a caixa
    /// registradora durante o expediente" ser um caso normal em vez de uma
    /// cascata de MissingReferenceException.
    ///
    /// A política de escolha não mora aqui de propósito. Ela vale para todo
    /// cliente, muda com o tempo (fila mais curta, mais perto, preferida do
    /// jogador) e precisa ser testável sem NavMesh — então é do serviço. O que é
    /// deste estado é só o que depende de NAVEGAÇÃO: o filtro de alcance.
    /// </summary>
    public sealed class SeekingCheckoutState : CustomerStateBase
    {
        /// <summary>Tempo total procurando caixa antes de desistir da loja.</summary>
        private const float GiveUpAfterSeconds = 8f;

        /// <summary>Intervalo entre buscas. Cada busca calcula caminhos.</summary>
        private const float SearchIntervalSeconds = 0.5f;

        private float _lastSearchTime;

        /// <summary>
        /// Guardado no campo para não alocar um closure por consulta. Cada
        /// cliente tem a própria instância do estado, então isto é rascunho de
        /// um cliente só.
        /// </summary>
        private System.Predicate<UnityEngine.Vector3> _canReach;
        private CustomerContext _context;

        public override void Enter(CustomerContext context)
        {
            // Solta qualquer estação anterior antes de procurar outra: se ele
            // veio da fila de um caixa que foi desligado, ainda está registrado
            // lá dentro.
            context.ReleaseStation();
            context.CheckoutLost = false;
            context.Locomotion.Halt();

            _context = context;
            if (_canReach == null) _canReach = CanReach;

            _lastSearchTime = context.CheckoutSearchTime;
            TryFindStation(context);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            context.CheckoutSearchTime += deltaTime;

            // A pergunta certa é "ainda tenho um caixa USÁVEL?", não "o campo
            // está preenchido". Testar `Station != null` deixaria o cliente
            // congelado para sempre com uma referência para um caixa que o
            // jogador desligou: a transição não dispara (FoundStation é falso) e
            // a busca não roda de novo, então nem o timeout de desistência é
            // alcançado.
            if (!context.StationLost) return;

            // ReleaseStation, e não `Station = null`: um caixa que o jogador
            // apenas DESLIGOU continua sendo um objeto válido, e largar a
            // referência sem avisá-lo deixaria o cliente pendurado na fila dele.
            context.ReleaseStation();

            // Um caixa pode ser ligado ou colocado enquanto ele espera, mas
            // procurar custa um cálculo de caminho por candidato. Uma vez a cada
            // meio segundo é reação suficiente e não pesa com a loja cheia.
            if (context.CheckoutSearchTime - _lastSearchTime < SearchIntervalSeconds) return;

            _lastSearchTime = context.CheckoutSearchTime;
            TryFindStation(context);
        }

        public override void Exit(CustomerContext context) => _context = null;

        private void TryFindStation(CustomerContext context)
        {
            if (context.Checkout != null
                && context.Checkout.TryFindBestStation(
                       context.Agent.Position, _canReach, out ICheckoutStation station))
            {
                context.Station = station;
                context.QueueIndex = station.QueueLength;
                return;
            }

            // Só reclama depois de esperar um pouco — evita o cliente desistir
            // no frame em que o jogador está reposicionando o caixa.
            //
            // O relógio é o CheckoutSearchTime do contexto, e não o StateTime,
            // porque este último zera a cada troca de estado: um cliente preso
            // no vaivém Seeking → Queueing → Seeking (fila que enche entre a
            // escolha e a chegada) nunca acumularia os cinco segundos e ficaria
            // oscilando sem nem enfileirar nem desistir.
            if (context.CheckoutSearchTime >= GiveUpAfterSeconds)
                context.Frustrate(CustomerLeaveReason.NoCheckout);
        }

        private bool CanReach(UnityEngine.Vector3 point) =>
            _context != null && _context.Locomotion.CanReach(point);

        public static bool FoundStation(CustomerContext context) => !context.StationLost;
    }
}
