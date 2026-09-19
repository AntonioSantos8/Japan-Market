using System.Text;
using JapanMarket.Core;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.UI
{
    /// <summary>
    /// Preenche e exibe a tela de resumo quando o relatório diário fecha.
    ///
    /// A hierarquia visual é criada no Inspector. Este componente fica num
    /// objeto sempre ativo, enquanto <see cref="_panel"/> é apenas o filho que
    /// aparece e desaparece.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DaySummaryView : MonoBehaviour
    {
        [Header("Janela")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private UnityEngine.UI.Button _continueButton;
        [SerializeField] private bool _pauseWhileOpen = true;

        [Header("Cabeçalho")]
        [SerializeField] private TMPro.TextMeshProUGUI _titleText;
        [SerializeField] private TMPro.TextMeshProUGUI _dayText;

        [Header("Economia")]
        [SerializeField] private TMPro.TextMeshProUGUI _openingBalanceText;
        [SerializeField] private TMPro.TextMeshProUGUI _revenueText;
        [SerializeField] private TMPro.TextMeshProUGUI _costOfGoodsText;
        [SerializeField] private TMPro.TextMeshProUGUI _grossProfitText;
        [SerializeField] private TMPro.TextMeshProUGUI _purchasesText;
        [SerializeField] private TMPro.TextMeshProUGUI _expensesText;
        [SerializeField] private TMPro.TextMeshProUGUI _netProfitText;
        [SerializeField] private TMPro.TextMeshProUGUI _closingBalanceText;
        [SerializeField] private TMPro.TextMeshProUGUI _cashFlowText;

        [Header("Clientes")]
        [SerializeField] private TMPro.TextMeshProUGUI _customersServedText;
        [SerializeField] private TMPro.TextMeshProUGUI _customersLostText;
        [SerializeField] private TMPro.TextMeshProUGUI _itemsSoldText;
        [SerializeField] private TMPro.TextMeshProUGUI _averageTicketText;
        [SerializeField] private TMPro.TextMeshProUGUI _lossReasonsText;

        private IDailyReportService _reports;
        private IGameClock _clock;
        private bool _subscribed;
        private bool _visible;
        private float _previousTimeScale = 1f;
        private bool _previousClockRunning = true;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_continueButton != null)
                _continueButton.onClick.AddListener(ContinueToNextDay);
        }

        private void Start() => TrySubscribe();

        private void Update()
        {
            if (!_subscribed) TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (!ServiceContainer.Current.TryResolve(out _reports)) return;

            ServiceContainer.Current.TryResolve(out _clock);
            _reports.ReportClosed += Show;
            _subscribed = true;
        }

        private void Show(DailyReport report)
        {
            if (report == null) return;

            Set(_titleText, "RESUMO DO DIA");
            Set(_dayText, $"Dia {report.Day} concluído");

            Set(_openingBalanceText, $"Saldo inicial: {report.OpeningBalance}");
            Set(_revenueText, $"Receita de vendas: {report.Revenue}");
            Set(_costOfGoodsText, $"Custo dos produtos: {report.CostOfGoods}");
            Set(_grossProfitText, $"Lucro bruto: {report.GrossProfit}");
            Set(_purchasesText, $"Compras de estoque: {report.Purchases}");
            Set(_expensesText, $"Despesas do dia: {report.Expenses}");
            Set(_netProfitText, $"Lucro líquido: {report.NetProfit}");
            Set(_closingBalanceText, $"Saldo final: {report.ClosingBalance}");
            Set(_cashFlowText, $"Variação de caixa: {report.CashFlow}");

            Set(_customersServedText, $"Clientes atendidos: {report.CustomersServed}");
            Set(_customersLostText, $"Clientes perdidos: {report.CustomersLost}");
            Set(_itemsSoldText, $"Itens vendidos: {report.ItemsSold}");
            Set(_averageTicketText, $"Ticket médio: {report.AverageTicket}");
            Set(_lossReasonsText, BuildLossReasons(report));

            if (_panel != null)
            {
                _panel.transform.SetAsLastSibling();
                _panel.SetActive(true);
            }

            if (_visible) return;

            _visible = true;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!_pauseWhileOpen) return;

            _previousTimeScale = Time.timeScale;
            _previousClockRunning = _clock == null || _clock.IsRunning;
            Time.timeScale = 0f;
            _clock?.SetRunning(false);
        }

        /// <summary>Ligado automaticamente ao botão atribuído no Inspector.</summary>
        public void ContinueToNextDay()
        {
            if (!_visible)
            {
                if (_panel != null) _panel.SetActive(false);
                return;
            }

            _visible = false;
            if (_panel != null) _panel.SetActive(false);

            if (_pauseWhileOpen)
            {
                Time.timeScale = _previousTimeScale;
                _clock?.SetRunning(_previousClockRunning);
            }

            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }

        private static string BuildLossReasons(DailyReport report)
        {
            if (report.CustomersLost <= 0) return "Motivos de perda: nenhum";

            var text = new StringBuilder("Motivos de perda:");
            foreach (var entry in report.LostByReason)
            {
                if (entry.Value <= 0) continue;
                text.Append("\n• ").Append(Describe(entry.Key)).Append(": ")
                    .Append(entry.Value);
            }
            return text.ToString();
        }

        private static string Describe(CustomerLeaveReason reason) => reason switch
        {
            CustomerLeaveReason.NothingToBuy => "não achou produto",
            CustomerLeaveReason.StoreTooDirty => "loja suja",
            CustomerLeaveReason.PricesTooHigh => "preços altos",
            CustomerLeaveReason.NoCheckout => "sem caixa",
            CustomerLeaveReason.WaitedTooLong => "espera excessiva",
            CustomerLeaveReason.StoreClosed => "loja fechada",
            CustomerLeaveReason.Purchased => "compra concluída",
            _ => reason.ToString(),
        };

        private static void Set(TMPro.TextMeshProUGUI target, string value)
        {
            if (target != null) target.text = value;
        }

        private void OnDestroy()
        {
            if (_subscribed && _reports != null)
                _reports.ReportClosed -= Show;

            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(ContinueToNextDay);

            if (_visible) ContinueToNextDay();
        }
    }
}
