using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// O fechamento de um dia de operação — a tela "Estatísticas Diárias" da
    /// referência, como dado.
    ///
    /// É uma PROJEÇÃO: nada aqui é contado por um contador espalhado pelo
    /// gameplay. Tudo sai dos eventos que já existem (venda concluída, cliente
    /// saiu, movimentação registrada). É essa diferença que faz o relatório
    /// nunca discordar do saldo — porque ele não tem uma fonte própria de
    /// verdade para discordar.
    /// </summary>
    public sealed class DailyReport
    {
        private readonly Dictionary<CustomerLeaveReason, int> _lostByReason = new();
        private readonly List<ExpenseLine> _expenses = new();

        public DailyReport(int day, Money openingBalance)
        {
            Day = day;
            OpeningBalance = openingBalance;
            ClosingBalance = openingBalance;
        }

        public int Day { get; }

        public Money OpeningBalance { get; }
        public Money ClosingBalance { get; internal set; }

        /// <summary>Quanto os clientes pagaram.</summary>
        public Money Revenue { get; internal set; }

        /// <summary>Quanto as unidades vendidas custaram para comprar.</summary>
        public Money CostOfGoods { get; internal set; }

        /// <summary>Compras de estoque, móveis, upgrades e expansões feitas hoje.</summary>
        public Money Purchases { get; internal set; }

        /// <summary>Contas do fim do dia. Preenchido no fechamento.</summary>
        public Money Expenses { get; internal set; }

        public IReadOnlyList<ExpenseLine> ExpenseLines => _expenses;

        public int CustomersServed { get; internal set; }
        public int CustomersLost { get; internal set; }
        public int ItemsSold { get; internal set; }

        /// <summary>Por que a loja perdeu venda hoje. É o que ensina o jogador.</summary>
        public IReadOnlyDictionary<CustomerLeaveReason, int> LostByReason => _lostByReason;

        /// <summary>Receita menos o custo do que foi vendido.</summary>
        public Money GrossProfit => Revenue - CostOfGoods;

        /// <summary>O número que importa: lucro bruto menos as contas do dia.</summary>
        public Money NetProfit => GrossProfit - Expenses;

        /// <summary>
        /// Quanto o saldo realmente mexeu.
        ///
        /// NÃO é igual ao <see cref="NetProfit"/>, e a diferença não é erro: o
        /// lucro desconta o custo do que foi VENDIDO, o caixa desconta o que foi
        /// COMPRADO. Um dia com ¥1000 de venda e ¥3000 de reposição fecha com
        /// lucro positivo e caixa negativo — e é isso que a tela precisa mostrar
        /// separado, senão o jogador acha que um dos dois está errado.
        /// </summary>
        public Money CashFlow => ClosingBalance - OpeningBalance;

        /// <summary>Ticket médio. Zero sem clientes, e não uma divisão por zero.</summary>
        public Money AverageTicket =>
            CustomersServed > 0 ? Money.FromYen(Revenue.Yen / (double)CustomersServed) : Money.Zero;

        public bool IsClosed { get; internal set; }

        internal void CountLost(CustomerLeaveReason reason)
        {
            CustomersLost++;
            _lostByReason.TryGetValue(reason, out int count);
            _lostByReason[reason] = count + 1;
        }

        internal void SetExpenses(IReadOnlyList<ExpenseLine> lines, Money total)
        {
            _expenses.Clear();
            if (lines != null) _expenses.AddRange(lines);

            Expenses = total;
        }

        public override string ToString() =>
            $"Dia {Day}: receita {Revenue}, custo {CostOfGoods}, despesas {Expenses}, " +
            $"lucro {NetProfit} · {CustomersServed} atendidos, {CustomersLost} perdidos";
    }
}
