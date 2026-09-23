using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JapanMarket.UI
{
    /// <summary>
    /// Gestão concentra as decisões de crescimento da loja. Funcionários já
    /// têm sua área e seus cargos apresentados, mas a contratação fica
    /// deliberadamente desabilitada até existir a IA de trabalho.
    /// </summary>
    public sealed class ManagementApp : ComputerApp
    {
        public override string Title => "Gestão";

        private RectTransform _employeesPanel;
        private RectTransform _sectionsPanel;
        private RectTransform _sectionsList;
        private TextMeshProUGUI _message;
        private Button _employeesTab;
        private Button _sectionsTab;

        private IStoreExpansionService _expansions;
        private IStoreLevelService _storeLevel;
        private ILedger _ledger;
        private IEventBus _events;
        private IDisposable _balanceSubscription;

        protected override void Build()
        {
            RectTransform column = UIKit.Column("Gestão", Content, 8f,
                                                new RectOffset(10, 10, 10, 10));
            UIKit.Stretch(column);

            RectTransform tabs = UIKit.Row("Abas", column, 42f, 8f,
                                           new RectOffset(0, 0, 0, 0));
            _employeesTab = UIKit.Button("Funcionários", tabs, "Funcionários",
                                         ShowEmployees, UIKit.BodySize).Grow();
            _sectionsTab = UIKit.Button("Expansões", tabs, "Expansões",
                                        ShowSections, UIKit.BodySize).Grow();

            Image employeesBackground = UIKit.Panel("Painel Funcionários", column,
                                                     new Color(0f, 0f, 0f, 0.12f));
            employeesBackground.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _employeesPanel = (RectTransform)employeesBackground.transform;
            BuildEmployees();

            Image sectionsBackground = UIKit.Panel("Painel Expansões", column,
                                                    new Color(0f, 0f, 0f, 0.12f));
            sectionsBackground.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _sectionsPanel = (RectTransform)sectionsBackground.transform;
            BuildSections();

            _message = UIKit.Label("Mensagem", column, string.Empty, UIKit.BodySize,
                                    UIKit.TextDim);
            _message.Width(700f);

            ShowEmployees();
        }

        private void BuildEmployees()
        {
            RectTransform list = UIKit.ScrollList("Rolagem Funcionários", _employeesPanel,
                                                   out _, spacing: 6f);

            UIKit.Label("Introdução", list,
                        "Contrate a equipe que vai operar os caixas e repor as prateleiras.",
                        UIKit.BodySize, UIKit.TextDim);
            UIKit.Label("Caixas", list, "CAIXAS", UIKit.SmallSize, UIKit.TextDim);

            for (int i = 1; i <= 4; i++)
                DrawEmployee(list, $"Caixa {i}", "Automatiza o atendimento no checkout");

            UIKit.Label("Repositores", list, "REPOSITORES", UIKit.SmallSize, UIKit.TextDim);

            for (int i = 1; i <= 4; i++)
                DrawEmployee(list, $"Repositor {i}", "Repõe os produtos nas prateleiras");
        }

        private static void DrawEmployee(Transform parent, string employeeName, string role)
        {
            RectTransform row = UIKit.Row(employeeName, parent, 48f);

            UIKit.Label("Nome", row, employeeName, UIKit.HeadingSize, UIKit.Text).Width(160f);
            UIKit.Label("Função", row, role, UIKit.BodySize, UIKit.TextDim).Grow();
            UIKit.Label("Estado", row, "Contratação em preparação", UIKit.SmallSize,
                        UIKit.TextDim, TextAlignmentOptions.Right).Width(190f);

            Button hire = UIKit.Button("Contratar", row, "Em breve", null,
                                       UIKit.SmallSize, UIKit.AccentDim);
            hire.Width(110f);
            hire.interactable = false;
        }

        private void BuildSections()
        {
            RectTransform column = UIKit.Column("Conteúdo Expansões", _sectionsPanel, 8f,
                                                new RectOffset(8, 8, 8, 8));
            UIKit.Stretch(column);

            UIKit.Label("Introdução", column,
                        "Escolha qualquer expansão disponível. Cada uma tem preço próprio " +
                        "e libera sua área da loja independentemente das demais.",
                        UIKit.BodySize, UIKit.TextDim);

            Image listBackground = UIKit.Panel("Lista", column, new Color(0f, 0f, 0f, 0.12f));
            listBackground.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _sectionsList = UIKit.ScrollList("Rolagem Expansões", listBackground.transform,
                                             out _, spacing: 6f);
        }

        protected override void Subscribe()
        {
            TryGet(out _expansions);
            TryGet(out _storeLevel);
            TryGet(out _ledger);

            if (_expansions != null) _expansions.SectionPurchased += OnSectionPurchased;
            if (_storeLevel != null) _storeLevel.LevelChanged += OnLevelChanged;

            if (TryGet(out _events))
                _balanceSubscription = _events.Subscribe<BalanceChanged>(_ => Refresh());
        }

        protected override void Unsubscribe()
        {
            if (_expansions != null)
                _expansions.SectionPurchased -= OnSectionPurchased;
            if (_storeLevel != null)
                _storeLevel.LevelChanged -= OnLevelChanged;

            _balanceSubscription?.Dispose();
            _balanceSubscription = null;
            _events = null;
        }

        private void OnSectionPurchased(int section)
        {
            _message.color = UIKit.Good;
            _message.text = $"Seção {section} comprada. A nova área da loja foi liberada.";
            Refresh();
        }

        private void OnLevelChanged(int _) => Refresh();

        public override void Refresh()
        {
            if (_sectionsList == null) return;
            if (_expansions == null && !TryGet(out _expansions))
            {
                UIKit.Clear(_sectionsList);
                UIKit.Label("Indisponível", _sectionsList,
                            "As expansões não estão disponíveis nesta cena.",
                            UIKit.BodySize, UIKit.TextDim);
                return;
            }

            if (_storeLevel == null) TryGet(out _storeLevel);
            if (_ledger == null) TryGet(out _ledger);

            DrawSections();
        }

        private void DrawSections()
        {
            UIKit.Clear(_sectionsList);

            IReadOnlyList<StoreSectionOffer> offers = _expansions.Offers;

            for (int i = 0; i < offers.Count; i++)
            {
                StoreSectionOffer offer = offers[i];
                RectTransform row = UIKit.Row($"Seção {offer.Section}", _sectionsList, 58f);

                RectTransform description = UIKit.Column("Descrição", row, 1f,
                    new RectOffset(0, 0, 0, 0));
                description.Grow();

                UIKit.Label("Nome", description, $"Seção {offer.Section}",
                            UIKit.HeadingSize, UIKit.Text);
                UIKit.Label("Detalhe", description,
                            "Expansão permanente da área utilizável da loja",
                            UIKit.SmallSize, UIKit.TextDim);

                UIKit.Label("Preço", row, offer.Price.ToString(), UIKit.HeadingSize,
                            UIKit.Text, TextAlignmentOptions.Right).Width(110f);

                bool owned = _expansions.IsOwned(offer.Section);
                bool levelReady = (_storeLevel?.CurrentLevel ?? 1) >= offer.RequiredStoreLevel;
                bool affordable = _ledger == null || _ledger.CanAfford(offer.Price);

                string status = owned
                    ? "Comprada"
                    : !levelReady
                        ? $"Nível {offer.RequiredStoreLevel}"
                        : !affordable ? "Saldo insuficiente" : "Disponível";

                UIKit.Label("Requisito", row, status, UIKit.SmallSize,
                            owned ? UIKit.Good : UIKit.TextDim,
                            TextAlignmentOptions.Right).Width(140f);

                StoreSectionOffer captured = offer;
                Button buy = UIKit.Button("Comprar", row, owned ? "Comprada" : "Comprar",
                    () => Purchase(captured), UIKit.SmallSize,
                    owned ? UIKit.SurfaceAlt : UIKit.Accent);
                buy.Width(110f);
                buy.interactable = !owned && levelReady && affordable;
            }
        }

        private void Purchase(StoreSectionOffer offer)
        {
            ExpansionPurchaseResult result = _expansions.TryPurchase(offer.Section);
            if (result == ExpansionPurchaseResult.Ok) return;

            _message.color = UIKit.Bad;
            _message.text = result switch
            {
                ExpansionPurchaseResult.AlreadyOwned => "Esta seção já foi comprada.",
                ExpansionPurchaseResult.StoreLevelRequired =>
                    $"A seção {offer.Section} exige nível {offer.RequiredStoreLevel} da loja.",
                ExpansionPurchaseResult.NotEnoughMoney => "Saldo insuficiente para a expansão.",
                ExpansionPurchaseResult.Unavailable => "O sistema de expansões está indisponível.",
                _ => "Não foi possível comprar esta seção.",
            };

            Refresh();
        }

        private void ShowEmployees()
        {
            if (_employeesPanel != null) _employeesPanel.gameObject.SetActive(true);
            if (_sectionsPanel != null) _sectionsPanel.gameObject.SetActive(false);
            SetTabColors(employeesSelected: true);
        }

        private void ShowSections()
        {
            if (_employeesPanel != null) _employeesPanel.gameObject.SetActive(false);
            if (_sectionsPanel != null) _sectionsPanel.gameObject.SetActive(true);
            SetTabColors(employeesSelected: false);
            Refresh();
        }

        private void SetTabColors(bool employeesSelected)
        {
            SetButtonColor(_employeesTab, employeesSelected ? UIKit.Accent : UIKit.SurfaceAlt);
            SetButtonColor(_sectionsTab, employeesSelected ? UIKit.SurfaceAlt : UIKit.Accent);
        }

        private static void SetButtonColor(Button button, Color color)
        {
            if (button != null && button.targetGraphic is Graphic graphic)
                graphic.color = color;
        }
    }
}
