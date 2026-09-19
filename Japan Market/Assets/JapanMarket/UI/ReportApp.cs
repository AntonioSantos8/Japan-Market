using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JapanMarket.UI
{
    /// <summary>
    /// O app Estatísticas: o dia de hoje até agora, e o histórico dos fechados.
    ///
    /// O relatório já existia como DADO desde a Fase 6 — o <c>DailyReport</c> é
    /// uma projeção do livro-razão, não um punhado de contadores espalhados pelos
    /// sistemas. Esta tela só desenha. É por isso que ela não precisou de uma
    /// linha nova em nenhum sistema de gameplay para existir.
    /// </summary>
    public sealed class ReportApp : ComputerApp
    {
        public override string Title => "Estatísticas";

        private RectTransform _today;
        private RectTransform _history;

        private IDailyReportService _reports;

        protected override void Build()
        {
            RectTransform column = UIKit.Column("Relatório", Content, 8f,
                                                new RectOffset(10, 10, 10, 10));
            UIKit.Stretch(column);

            UIKit.Label("h1", column, "HOJE", UIKit.SmallSize, UIKit.TextDim);

            Image todayPanel = UIKit.Panel("Hoje", column, new Color(0f, 0f, 0f, 0.12f));
            todayPanel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1.3f;
            _today = UIKit.ScrollList("RolagemHoje", todayPanel.transform, out _);

            UIKit.Label("h2", column, "DIAS FECHADOS", UIKit.SmallSize, UIKit.TextDim);

            Image historyPanel = UIKit.Panel("Histórico", column, new Color(0f, 0f, 0f, 0.12f));
            historyPanel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _history = UIKit.ScrollList("RolagemHistórico", historyPanel.transform, out _);
        }

        protected override void Subscribe()
        {
            if (!TryGet(out _reports)) return;

            _reports.ReportClosed += OnReportClosed;
        }

        protected override void Unsubscribe()
        {
            if (_reports == null) return;

            _reports.ReportClosed -= OnReportClosed;
        }

        private void OnReportClosed(DailyReport _) => Refresh();

        public override void Refresh()
        {
            if (_today == null) return;
            if (_reports == null && !TryGet(out _reports)) return;

            DrawToday();
            DrawHistory();
        }

        private void DrawToday()
        {
            UIKit.Clear(_today);

            DailyReport report = _reports.Current;

            if (report == null)
            {
                UIKit.Label("Vazio", _today, "O dia ainda não começou.", UIKit.BodySize,
                            UIKit.TextDim);
                return;
            }

            Line("Receita de vendas", report.Revenue, UIKit.Good);
            Line("Custo da mercadoria vendida", -report.CostOfGoods, UIKit.TextDim);
            Line("Lucro bruto", report.GrossProfit,
                 report.GrossProfit.IsNegative ? UIKit.Bad : UIKit.Good);

            Separator();

            Line("Compras de estoque", -report.Purchases, UIKit.TextDim);
            Line("Despesas do dia", -report.Expenses, UIKit.TextDim);

            // As linhas de despesa vêm do próprio relatório: aluguel, luz,
            // parcela de empréstimo. Uma despesa nova aparece aqui sozinha,
            // porque ela é registrada como fonte e não escrita nesta tela.
            IReadOnlyList<ExpenseLine> lines = report.ExpenseLines;
            for (int i = 0; i < lines.Count; i++)
                Line($"    {lines[i].Label}", -lines[i].Amount, UIKit.TextDim,
                     UIKit.SmallSize);

            Separator();

            Line("Lucro líquido", report.NetProfit,
                 report.NetProfit.IsNegative ? UIKit.Bad : UIKit.Good, UIKit.HeadingSize);

            Separator();

            Count("Clientes atendidos", report.CustomersServed);
            Count("Clientes perdidos", report.CustomersLost,
                  report.CustomersLost > 0 ? UIKit.Bad : UIKit.TextDim);
            Count("Itens vendidos", report.ItemsSold);
            Line("Ticket médio", report.AverageTicket, UIKit.TextDim);

            // Por que o cliente foi embora é a informação mais acionável da tela:
            // "loja suja" e "preço alto" pedem coisas opostas do jogador.
            foreach (KeyValuePair<CustomerLeaveReason, int> entry in report.LostByReason)
            {
                if (entry.Value <= 0) continue;

                Count($"    {Describe(entry.Key)}", entry.Value, UIKit.TextDim, UIKit.SmallSize);
            }
        }

        private void DrawHistory()
        {
            UIKit.Clear(_history);

            IReadOnlyList<DailyReport> closed = _reports.ClosedReports;

            if (closed.Count == 0)
            {
                UIKit.Label("Vazio", _history, "Nenhum dia fechado ainda.", UIKit.BodySize,
                            UIKit.TextDim);
                return;
            }

            // Do mais recente para o mais antigo: o jogador quer ver ontem, não
            // o primeiro dia da partida.
            for (int i = closed.Count - 1; i >= 0; i--)
            {
                DailyReport report = closed[i];
                RectTransform row = UIKit.Row($"Dia {report.Day}", _history, 28f);

                UIKit.Label("Dia", row, $"Dia {report.Day}", UIKit.BodySize, UIKit.Text)
                     .Width(80f);
                UIKit.Label("Receita", row, report.Revenue.ToString(), UIKit.BodySize,
                            UIKit.TextDim, TextAlignmentOptions.Right).Grow();
                UIKit.Label("Clientes", row, $"{report.CustomersServed} clientes",
                            UIKit.BodySize, UIKit.TextDim, TextAlignmentOptions.Right)
                     .Width(120f);
                UIKit.Label("Lucro", row, report.NetProfit.ToString(), UIKit.BodySize,
                            report.NetProfit.IsNegative ? UIKit.Bad : UIKit.Good,
                            TextAlignmentOptions.Right).Width(110f);
            }
        }

        // ── linhas ───────────────────────────────────────────────────────────

        private void Line(string label, Money value, Color color, int size = UIKit.BodySize)
        {
            RectTransform row = UIKit.Row(label, _today, size + 8f, 8f,
                                          new RectOffset(4, 4, 0, 0));

            UIKit.Label("Nome", row, label, size, UIKit.Text).Grow();
            UIKit.Label("Valor", row, value.ToString(), size, color,
                        TextAlignmentOptions.Right).Width(140f);
        }

        private void Count(string label, int value, Color? color = null,
                           int size = UIKit.BodySize)
        {
            RectTransform row = UIKit.Row(label, _today, size + 8f, 8f,
                                          new RectOffset(4, 4, 0, 0));

            UIKit.Label("Nome", row, label, size, UIKit.Text).Grow();
            UIKit.Label("Valor", row, value.ToString(), size, color ?? UIKit.Text,
                        TextAlignmentOptions.Right).Width(140f);
        }

        private void Separator()
        {
            Image line = UIKit.Panel("Separador", _today, new Color(1f, 1f, 1f, 0.08f));
            line.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;
        }

        private static string Describe(CustomerLeaveReason reason) => reason switch
        {
            CustomerLeaveReason.Purchased => "comprou e saiu",
            CustomerLeaveReason.NothingToBuy => "não achou o que comprar",
            CustomerLeaveReason.StoreTooDirty => "loja suja demais",
            CustomerLeaveReason.PricesTooHigh => "preço alto demais",
            CustomerLeaveReason.NoCheckout => "não achou caixa",
            CustomerLeaveReason.WaitedTooLong => "esperou demais na fila",
            CustomerLeaveReason.StoreClosed => "a loja fechou",
            _ => reason.ToString(),
        };
    }
}
