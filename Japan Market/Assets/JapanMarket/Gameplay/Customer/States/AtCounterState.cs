using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// No balcão, sendo atendido.
    ///
    /// Este estado é o que fecha o ciclo da loja. A diferença mais importante
    /// em relação ao <c>CashRegister</c> atual não é o que ele faz, é o que ele
    /// deixou de fazer: aqui o cliente não spawna item, não anima sacola, não
    /// toca som, não mexe em câmera e não conhece o jogador. Ele abre a venda,
    /// deposita o dinheiro na mão e espera.
    ///
    /// Quem passa os itens pelo leitor e devolve o troco é o jogador, pela
    /// interface da Fase 5b. Enquanto ela não existe, o campo "Sandbox" da
    /// <see cref="CheckoutStation"/> conclui a venda sozinho depois de alguns
    /// segundos — e o ciclo inteiro roda de ponta a ponta.
    /// </summary>
    public sealed class AtCounterState : CustomerStateBase
    {
        /// <summary>Tempo tentando abrir a venda antes de concluir que não vai dar.</summary>
        private const float OpenTimeoutSeconds = 3f;

        private readonly List<SaleLine> _lines = new();

        public override void Enter(CustomerContext context)
        {
            context.SaleFinished = false;
            context.Session = null;

            if (context.StationLost) return;

            HaltFacing(context, context.Station.CounterPosition);
            TryOpen(context);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.StationLost) return;

            if (context.Session == null)
            {
                // A venda pode não ter aberto no Enter: outro cliente ainda
                // ocupava o balcão por um frame. Tenta de novo, e desiste do
                // caixa — não da loja — se ele não liberar.
                if (context.StateTime < OpenTimeoutSeconds) { TryOpen(context); return; }

                context.CheckoutLost = true;
                return;
            }

            // A venda dele foi encerrada por fora — a interface do caixa da Fase
            // 5b pode cancelar uma venda. Sem esta checagem ele ficaria parado
            // no balcão até estourar a paciência, e o caixa travado junto.
            if (!ReferenceEquals(context.Session, context.Station.CurrentSession)
                && !context.Session.IsComplete)
            {
                context.Session = null;
                context.CheckoutLost = true;
                return;
            }

            // Paciência no balcão é a mesma da fila: o cliente não fica eterno
            // esperando o jogador aparecer para atender.
            if (QueueingState.OutOfPatience(context))
                context.Frustrate(CustomerLeaveReason.WaitedTooLong);
        }

        /// <summary>
        /// Só a limpeza que é deste estado. Encerrar a venda e sair da fila é
        /// <see cref="CustomerContext.ReleaseStation"/>, chamada pelos estados
        /// que terminam o episódio de checkout — todo caminho para fora daqui
        /// passa por um deles.
        /// </summary>
        public override void Exit(CustomerContext context)
        {
            _lines.Clear();

            // Pagou: os itens saíram da cesta e foram para a sacola. Quem não
            // pagou leva a cesta embora, e o relatório do dia enxerga isso.
            if (!context.SaleFinished) return;

            context.Basket.Clear();

            // Comparação explícita, e não `?.`: o operador nulo-condicional
            // burla o == sobrecarregado do Unity e chamaria um componente
            // destruído (regra 10 do README).
            if (context.Animation != null) context.Animation.SetCarrying(false);
        }

        private void TryOpen(CustomerContext context)
        {
            if (!context.Station.IsFront(context.Agent)) return;

            BuildLines(context);
            if (_lines.Count == 0)
            {
                // Chegou ao balcão de mãos vazias. Não é venda: é ir embora.
                context.Frustrate(CustomerLeaveReason.NothingToBuy);
                return;
            }

            PaymentMethod method = context.Profile != null && context.Profile.RollPrefersCard()
                ? PaymentMethod.Card
                : PaymentMethod.Cash;

            if (!context.Station.TryOpenSession(context.Agent, _lines, method,
                                                out CheckoutSession session))
                return;

            // O cliente já deixa o dinheiro na mão. O minigame do troco é do
            // jogador: ele vê quanto foi entregue e devolve a diferença.
            if (method == PaymentMethod.Cash)
            {
                session.SetAmountTendered(PaymentProcessor.RollTenderedAmount(
                    session.Total, PaymentProcessor.JapaneseDenominations));
            }

            context.Session = session;
        }

        private void BuildLines(CustomerContext context)
        {
            _lines.Clear();

            IReadOnlyList<CustomerBasket.Entry> entries = context.Basket.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                CustomerBasket.Entry entry = entries[i];
                if (entry.Product == null) continue;   // produto apagado do projeto

                _lines.Add(new SaleLine(entry.Product, entry.PricePaid));
            }
        }

        // ── condições da tabela ──────────────────────────────────────────────

        public static bool Paid(CustomerContext context) => context.SaleFinished;

        public static bool LostStation(CustomerContext context) =>
            context.StationLost || context.CheckoutLost;
    }
}
