using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JapanMarket.UI
{
    /// <summary>
    /// A tela do computador: barra de apps em cima, app aberto embaixo, saldo e
    /// relógio no canto.
    ///
    /// Ele MONTA a si mesmo. Coloque este componente num GameObject filho da tela
    /// do computador (o mesmo objeto que o <c>Computer</c> legado liga e desliga)
    /// e não configure mais nada — os apps se registram sozinhos.
    ///
    /// Adicionar um app é adicionar um componente que herda de
    /// <see cref="ComputerApp"/> neste mesmo objeto. A barra se monta a partir do
    /// que encontrar; não existe lista para manter, nem enum de abas, nem
    /// <c>switch</c> decidindo qual desenhar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComputerAppHost : MonoBehaviour
    {
        [Tooltip("Quais apps criar. Deixe tudo ligado; desligue um para escondê-lo.")]
        [SerializeField] private bool _market = true;
        [SerializeField] private bool _pricing = true;
        [SerializeField] private bool _bank = true;
        [SerializeField] private bool _report = true;
        [SerializeField] private bool _objectives = true;

        private readonly List<ComputerApp> _apps = new();
        private readonly List<Button> _tabs = new();

        private ComputerApp _open;
        private RectTransform _tabBar;
        private RectTransform _body;
        private TextMeshProUGUI _balance;
        private TextMeshProUGUI _clock;

        private ILedger _ledger;
        private IGameClock _gameClock;

        private void Awake()
        {
            BuildFrame();
            CreateApps();
            BuildTabs();

            if (_apps.Count > 0) Show(_apps[0]);
        }

        private void OnEnable()
        {
            // O computador é ligado e desligado pelo SetActive do objeto pai. Cada
            // vez que ele acende, o app aberto tem que reler — o dia virou, o
            // saldo mudou, a entrega chegou.
            _open?.Open();
            RefreshStatus();
        }

        private void OnDisable() => _open?.Close();

        private void Update()
        {
            // Saldo e relógio mudam sem evento a cada minuto de jogo. Um Update
            // que só escreve duas strings é mais barato que assinar o relógio e
            // redesenhar a barra a cada tique.
            RefreshStatus();
        }

        // ── moldura ──────────────────────────────────────────────────────────

        private void BuildFrame()
        {
            RectTransform root = (RectTransform)transform;

            if (root == null)
            {
                Debug.LogError("[ComputerAppHost] Este componente precisa estar num " +
                               "objeto de UI, dentro de um Canvas.", this);
                enabled = false;
                return;
            }

            Image background = UIKit.Panel("Fundo", root, UIKit.Background);
            UIKit.Stretch((RectTransform)background.transform);

            RectTransform column = UIKit.Column("Tela", background.transform, 0f,
                                                new RectOffset(12, 12, 12, 12));
            UIKit.Stretch(column);

            // Cabeçalho: título, saldo, relógio.
            RectTransform header = UIKit.Row("Cabeçalho", column, 44f);
            UIKit.Label("Título", header, "Computador da Loja", UIKit.TitleSize,
                        UIKit.Text).Grow();
            _balance = UIKit.Label("Saldo", header, "—", UIKit.HeadingSize, UIKit.Good,
                                   TextAlignmentOptions.Right).Width(160f);
            _clock = UIKit.Label("Relógio", header, "—", UIKit.HeadingSize, UIKit.TextDim,
                                 TextAlignmentOptions.Right).Width(140f);

            _tabBar = UIKit.Row("Apps", column, 40f);

            Image bodyPanel = UIKit.Panel("Corpo", column, UIKit.Surface);
            var grow = bodyPanel.gameObject.AddComponent<LayoutElement>();
            grow.flexibleHeight = 1f;

            _body = (RectTransform)bodyPanel.transform;
        }

        private void RefreshStatus()
        {
            if (_balance == null) return;

            if (_ledger == null) ServiceContainer.Current.TryResolve(out _ledger);
            if (_gameClock == null) ServiceContainer.Current.TryResolve(out _gameClock);

            _balance.text = _ledger != null ? _ledger.Balance.ToString() : "—";

            if (_gameClock == null) { _clock.text = "—"; return; }

            int hour = (int)_gameClock.TimeOfDay;
            int minute = (int)((_gameClock.TimeOfDay - hour) * 60f);
            _clock.text = $"Dia {_gameClock.Day}  {hour:00}:{minute:00}";
        }

        // ── apps ─────────────────────────────────────────────────────────────

        private void CreateApps()
        {
            if (_market) Add<MarketApp>();
            if (_pricing) Add<PricingApp>();
            if (_objectives) Add<ObjectivesApp>();
            if (_bank) Add<BankApp>();
            if (_report) Add<ReportApp>();
        }

        private void Add<T>() where T : ComputerApp
        {
            // Cada app vive num objeto próprio dentro do corpo: é o que permite
            // ligar e desligar um sem tocar nos outros, e o que faz a hierarquia
            // ficar legível no Inspector durante o Play.
            RectTransform page = UIKit.Rect(typeof(T).Name, _body);
            UIKit.Stretch(page);

            var app = page.gameObject.AddComponent<T>();
            app.Attach(page);

            page.gameObject.SetActive(false);
            _apps.Add(app);
        }

        private void BuildTabs()
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                ComputerApp app = _apps[i];

                Button tab = UIKit.Button($"Aba {app.Title}", _tabBar, app.Title,
                                          () => Show(app), UIKit.BodySize, UIKit.SurfaceAlt);
                tab.Width(150f);

                _tabs.Add(tab);
            }
        }

        public void Show(ComputerApp app)
        {
            if (app == null || _open == app) return;

            _open?.Close();
            _open = app;
            _open.Open();

            for (int i = 0; i < _tabs.Count; i++)
            {
                bool selected = _apps[i] == app;

                _tabs[i].targetGraphic.color = selected ? UIKit.Accent : UIKit.SurfaceAlt;
                _tabs[i].interactable = !selected;
            }
        }
    }
}
