using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JapanMarket.UI
{
    /// <summary>
    /// O app Banco: contratar empréstimo, ver o que está devendo, quitar antes.
    ///
    /// As faixas vêm do <c>ILoanCatalog</c> já filtradas por desbloqueio, como a
    /// vitrine do Mercado. Esta tela não sabe que existe nível de loja nem flag.
    ///
    /// A recusa é explicada, e não escondida: um botão desabilitado sem motivo é
    /// a forma mais rápida de o jogador achar que o jogo travou.
    /// </summary>
    public sealed class BankApp : ComputerApp
    {
        public override string Title => "Banco";

        private RectTransform _offers;
        private RectTransform _active;
        private TextMeshProUGUI _debt;
        private TextMeshProUGUI _daily;
        private TextMeshProUGUI _message;

        private IBankService _bank;
        private ILoanCatalog _catalog;
        private IUnlockContext _unlocks;
        private ILedger _ledger;
        private IEventBus _events;
        private IDisposable _balanceSubscription;

        protected override void Build()
        {
            RectTransform column = UIKit.Column("Banco", Content, 8f,
                                                new RectOffset(10, 10, 10, 10));
            UIKit.Stretch(column);

            RectTransform summary = UIKit.Row("Resumo", column, 34f);
            UIKit.Label("t1", summary, "Dívida total", UIKit.BodySize, UIKit.TextDim).Width(120f);
            _debt = UIKit.Label("Dívida", summary, "¥0", UIKit.HeadingSize, UIKit.Text)
                         .Width(140f);
            UIKit.Label("t2", summary, "Parcela por dia", UIKit.BodySize, UIKit.TextDim)
                 .Width(140f);
            _daily = UIKit.Label("Parcela", summary, "¥0", UIKit.HeadingSize, UIKit.Text).Grow();

            UIKit.Label("h1", column, "CONTRATOS EM ABERTO", UIKit.SmallSize, UIKit.TextDim);

            Image activePanel = UIKit.Panel("Ativos", column, new Color(0f, 0f, 0f, 0.12f));
            activePanel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _active = UIKit.ScrollList("RolagemAtivos", activePanel.transform, out _);

            UIKit.Label("h2", column, "FAIXAS DISPONÍVEIS", UIKit.SmallSize, UIKit.TextDim);

            Image offerPanel = UIKit.Panel("Ofertas", column, new Color(0f, 0f, 0f, 0.12f));
            offerPanel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1.4f;
            _offers = UIKit.ScrollList("RolagemOfertas", offerPanel.transform, out _);

            _message = UIKit.Label("Mensagem", column, string.Empty, UIKit.BodySize, UIKit.TextDim);
            _message.Width(600f);
        }

        protected override void Subscribe()
        {
            if (!TryGet(out _bank)) return;

            TryGet(out _catalog);
            TryGet(out _unlocks);
            TryGet(out _ledger);
            if (TryGet(out _events))
                _balanceSubscription = _events.Subscribe<BalanceChanged>(_ => Refresh());

            _bank.LoanTaken += OnLoansChanged;
            _bank.LoanPaidOff += OnLoansChanged;
        }

        protected override void Unsubscribe()
        {
            _balanceSubscription?.Dispose();
            _balanceSubscription = null;
            _events = null;

            if (_bank == null) return;

            _bank.LoanTaken -= OnLoansChanged;
            _bank.LoanPaidOff -= OnLoansChanged;
        }

        private void OnLoansChanged(ActiveLoan _) => Refresh();

        public override void Refresh()
        {
            if (_offers == null) return;
            if (_bank == null && !TryGet(out _bank)) return;

            _debt.text = _bank.TotalDebt.ToString();
            _daily.text = _bank.DailyDebtService.ToString();
            _debt.color = _bank.TotalDebt.IsPositive ? UIKit.Bad : UIKit.Text;

            DrawActive();
            DrawOffers();
        }

        private void DrawActive()
        {
            UIKit.Clear(_active);

            IReadOnlyList<ActiveLoan> loans = _bank.ActiveLoans;

            if (loans.Count == 0)
            {
                UIKit.Label("Vazio", _active, "Nenhum empréstimo em aberto.", UIKit.BodySize,
                            UIKit.TextDim);
                return;
            }

            // Cópia: quitar remove da lista enquanto ela é percorrida.
            var snapshot = new List<ActiveLoan>(loans);

            for (int i = 0; i < snapshot.Count; i++)
            {
                ActiveLoan loan = snapshot[i];
                if (loan?.Definition == null) continue;

                RectTransform row = UIKit.Row($"Contrato {i}", _active, 36f);

                string name = loan.Definition.DisplayName.IsEmpty
                    ? loan.Definition.name
                    : loan.Definition.DisplayName.Value;

                UIKit.Label("Nome", row, name, UIKit.BodySize, UIKit.Text).Grow();
                UIKit.Label("Parcelas", row,
                            $"{loan.PaymentsMade}/{loan.Definition.TermDays} parcelas",
                            UIKit.BodySize, UIKit.TextDim,
                            TextAlignmentOptions.Right).Width(140f);
                UIKit.Label("Saldo", row, loan.BalanceToPayOff.ToString(), UIKit.BodySize,
                            UIKit.Text, TextAlignmentOptions.Right).Width(110f);

                ActiveLoan captured = loan;
                Button payOff = UIKit.Button("Quitar", row, "Quitar", () => PayOff(captured),
                                             UIKit.SmallSize);
                payOff.Width(90f);

                payOff.interactable = _ledger == null || _ledger.CanAfford(loan.BalanceToPayOff);
            }
        }

        private void DrawOffers()
        {
            UIKit.Clear(_offers);

            if (_catalog == null && !TryGet(out _catalog))
            {
                UIKit.Label("Sem catálogo", _offers,
                            "Nenhum LoanCatalog atribuído no GameContext.", UIKit.BodySize,
                            UIKit.TextDim);
                return;
            }

            List<LoanDefinition> tiers = _catalog.UnlockedFor(_unlocks);

            if (tiers.Count == 0)
            {
                UIKit.Label("Vazio", _offers,
                            "Nenhuma faixa liberada ainda. Suba o nível da loja.",
                            UIKit.BodySize, UIKit.TextDim);
                return;
            }

            for (int i = 0; i < tiers.Count; i++) DrawOffer(tiers[i]);
        }

        private void DrawOffer(LoanDefinition tier)
        {
            RectTransform row = UIKit.Row($"Faixa {tier.name}", _offers, 38f);

            string name = tier.DisplayName.IsEmpty ? tier.name : tier.DisplayName.Value;

            UIKit.Label("Nome", row, name, UIKit.BodySize, UIKit.Text).Grow();
            UIKit.Label("Valor", row, tier.Principal.ToString(), UIKit.BodySize, UIKit.Good,
                        TextAlignmentOptions.Right).Width(110f);
            UIKit.Label("Parcela", row, $"{tier.DailyPayment} × {tier.TermDays}",
                        UIKit.BodySize, UIKit.TextDim, TextAlignmentOptions.Right).Width(150f);
            Money interest = tier.TotalCost - tier.Principal;
            UIKit.Label("Juros", row, $"juros {interest}", UIKit.SmallSize, UIKit.TextDim,
                        TextAlignmentOptions.Right).Width(120f);
            UIKit.Label("Total", row, $"total {tier.TotalCost}", UIKit.SmallSize,
                        UIKit.TextDim, TextAlignmentOptions.Right).Width(120f);

            LoanDefinition captured = tier;
            UIKit.Button("Contratar", row, "Contratar", () => Take(captured), UIKit.SmallSize,
                         UIKit.Accent).Width(110f);
        }

        // ── ações ────────────────────────────────────────────────────────────

        private void Take(LoanDefinition tier)
        {
            if (_bank.TryTakeLoan(tier))
            {
                _message.color = UIKit.Good;
                _message.text = $"Contratado: {tier.Principal} no caixa.";
                return;
            }

            _message.color = UIKit.Bad;
            _message.text = ExplainRefusal(tier);
        }

        /// <summary>
        /// O serviço devolve só um bool, então o motivo é reconstruído aqui na
        /// MESMA ordem em que ele checa. É duplicação de regra, e está aqui
        /// escrito para não passar despercebida: se o BankService ganhar um
        /// motivo tipado como o do Mercado, este método sai.
        /// </summary>
        private string ExplainRefusal(LoanDefinition tier)
        {
            if (tier == null) return "Faixa inválida.";
            if (!tier.IsValid) return "Este contrato está mal configurado e foi recusado.";
            if (!tier.IsUnlocked(_unlocks)) return "Esta faixa ainda não está liberada.";

            for (int i = 0; i < _bank.ActiveLoans.Count; i++)
                if (_bank.ActiveLoans[i].Definition == tier)
                    return "Você já tem um contrato desta faixa em aberto.";

            return _bank.MaxConcurrentLoans > 0
                       && _bank.ActiveLoans.Count >= _bank.MaxConcurrentLoans
                ? "Você já atingiu o limite de empréstimos em aberto."
                : "O banco recusou o contrato.";
        }

        private void PayOff(ActiveLoan loan)
        {
            if (_bank.TryPayOffEarly(loan))
            {
                _message.color = UIKit.Good;
                _message.text = "Contrato quitado.";
                return;
            }

            _message.color = UIKit.Bad;
            _message.text = "Saldo insuficiente para quitar de uma vez.";
        }
    }
}
