using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Composition root. Constrói e registra os serviços do jogo numa ordem
    /// explícita, e derruba tudo quando a cena morre.
    ///
    /// Substitui o problema de ordem de inicialização do ServiceLocator atual,
    /// onde treze classes se registram no Awake e quatro no Start, e alguém que
    /// consome no Start pode perder a corrida. O caso concreto é o StoreSign:
    /// ele busca MarketManager e NpcManager no Start(), e os dois se registram
    /// no Start() também. Perdendo o sorteio, os dois campos ficam null para
    /// sempre e a primeira abertura da loja lança NullReferenceException.
    ///
    /// [DefaultExecutionOrder(-10000)] garante que este Awake roda antes de
    /// qualquer outro — inclusive antes dos MonoBehaviours legados.
    ///
    /// Coloque UM destes na cena, num GameObject vazio chamado "— Game Context —".
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GameContext : MonoBehaviour
    {
        [Header("Catálogos")]
        [Tooltip("O único catálogo de produtos do projeto. Populado automaticamente " +
                 "pelo import — não arraste produtos à mão.")]
        [SerializeField] private ItemCatalog _itemCatalog;

        [Tooltip("O único catálogo de móveis. Mesma regra: populado pelo import.")]
        [SerializeField] private FurnitureCatalog _furnitureCatalog;

        [Tooltip("O único catálogo de objetivos. Mesma regra: populado pelo import.")]
        [SerializeField] private ObjectiveCatalog _objectiveCatalog;

        [Tooltip("O único catálogo de lixo. Mesma regra: populado pelo import.")]
        [SerializeField] private TrashCatalog _trashCatalog;

        [Tooltip("O único catálogo de empréstimos. Mesma regra: populado pelo import.")]
        [SerializeField] private LoanCatalog _loanCatalog;

        [Header("Economia")]
        [Tooltip("Saldo com que a loja começa uma partida nova, em ienes.")]
        [SerializeField] private long _openingBalanceYen = 8000;

        [Tooltip("Aluguel cobrado todo fim de dia, em ienes.")]
        [SerializeField] private long _dailyRentYen = 100;

        [Header("Tempo")]
        [SerializeField] private GameClockSettings _clock = GameClockSettings.Default;

        [Header("Compras")]
        [Min(1)]
        [Tooltip("Quantos pedidos de estoque cabem a caminho ao mesmo tempo.")]
        [SerializeField] private int _maxPendingOrders = 3;

        [Min(0f)]
        [Tooltip("Prazo de entrega, em horas de jogo. 0 entrega na hora.")]
        [SerializeField] private float _deliveryHours = 2f;

        [Header("Banco")]
        [Min(0)]
        [Tooltip("Empréstimos abertos ao mesmo tempo. 0 = sem limite.")]
        [SerializeField] private int _maxConcurrentLoans = 1;

        [Header("Objetivos")]
        [Min(0)]
        [Tooltip("Objetivos valendo ao mesmo tempo. 0 = sem limite.")]
        [SerializeField] private int _maxActiveObjectives = 3;

        [Header("Ferramentas e limpeza")]
        [Tooltip("A roda de ferramentas. Sem ela o jogador joga de mão vazia.")]
        [SerializeField] private ToolBeltLayout _toolBelt;

        [Min(1)]
        [Tooltip("Com quantas sujeiras a loja está no limite do tolerável. " +
                 "Acima disso os clientes começam a ir embora.")]
        [SerializeField] private int _dirtTolerance = 12;

        [Header("Save")]
        [Tooltip("Carrega o save automaticamente ao entrar em Play, se existir.")]
        [SerializeField] private bool _loadOnStart;

        [Tooltip("Salva automaticamente no fechamento de cada dia.")]
        [SerializeField] private bool _saveOnDayEnd = true;

        [Header("Diagnóstico")]
        [Tooltip("Valida o catálogo ao entrar em Play e lista os problemas no console.")]
        [SerializeField] private bool _validateCatalogOnPlay = true;

        private ServiceContainer _container;
        private EventBus _events;
        private StoreLevelService _storeLevel;
        private StoreProgress _progress;
        private FurnitureRegistry _furniture;
        private PricingService _pricing;
        private CheckoutService _checkout;

        private GameClock _gameClock;
        private Ledger _ledger;
        private ExpenseService _expenses;
        private DailyReportService _reports;
        private SalesAccountant _accountant;
        private BankService _bank;
        private MarketOrderService _market;
        private ObjectiveService _objectives;
        private TrashService _trash;
        private ToolBelt _tools;
        private CleanlinessService _cleanliness;
        private DayCycle _dayCycle;
        private SaveService _save;
        private IDisposable _daySubscription;
        private IDisposable _saveSubscription;

        public static GameContext Current { get; private set; }

        public IServiceContainer Services => _container;
        public IEventBus Events => _events;
        public IStoreLevelService StoreLevel => _storeLevel;
        public StoreProgress Progress => _progress;
        public IFurnitureRegistry Furniture => _furniture;
        public IPricingService Pricing => _pricing;
        public ICheckoutService Checkout => _checkout;
        public IGameClock Clock => _gameClock;
        public ILedger Ledger => _ledger;
        public IExpenseService Expenses => _expenses;
        public IBankService Bank => _bank;
        public IMarketOrderService Market => _market;
        public IObjectiveService Objectives => _objectives;
        public ITrashService Trash => _trash;
        public IToolBelt Tools => _tools;
        public IStoreCleanliness Cleanliness => _cleanliness;
        public IDailyReportService Reports => _reports;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError("[GameContext] Já existe um GameContext nesta cena. " +
                               "Este objeto será destruído.", this);
                Destroy(gameObject);
                return;
            }

            Current = this;

            _container = new ServiceContainer();
            _events = new EventBus();
            _storeLevel = new StoreLevelService(_events);
            _progress = new StoreProgress(_storeLevel);
            _furniture = new FurnitureRegistry();
            _pricing = new PricingService();
            _checkout = new CheckoutService(_furniture, _events);

            BuildEconomy();
            BuildObjectives();
            BuildToolsAndCleaning();

            ServiceContainer.SetCurrent(_container);

            RegisterCoreServices();

            BuildSave();
        }

        /// <summary>
        /// O save é construído por último, no fim do Awake, porque ele consulta
        /// TODOS os serviços — inclusive os catálogos, que só entram no container
        /// no <see cref="RegisterCoreServices"/>.
        ///
        /// O carregamento acontece no Start, e não aqui, por um motivo de ordem:
        /// [DefaultExecutionOrder(-10000)] faz este Awake rodar antes de todos os
        /// outros, e restaurar o estado agora significaria publicar eventos que
        /// ninguém ainda assinou — a tela e os móveis da cena só existem no Start
        /// deles. Carregar cedo demais é carregar para ninguém.
        /// </summary>
        private void BuildSave()
        {
            _save = new SaveService(this);

            if (_saveOnDayEnd && _events != null)
                _saveSubscription = _events.Subscribe<DayEnded>(_ => SaveNow());
        }

        private void Start()
        {
            if (SaveSession.TryConsumeStartRequest(out SaveStartMode startMode))
            {
                if (startMode == SaveStartMode.LoadGame)
                    TryLoadNow();
                else if (startMode == SaveStartMode.NewGame)
                    SaveNow();

                return;
            }

            if (_loadOnStart) TryLoadNow();
        }

        /// <summary>Salva por cima do arquivo padrão. Devolve false se falhou.</summary>
        [ContextMenu("Save/Salvar agora")]
        public bool SaveNow()
        {
            if (_save == null) return false;

            return SaveFile.TryWrite(_save.Capture(), SaveSession.CurrentFileName);
        }

        /// <summary>
        /// Carrega o arquivo padrão. Devolve false quando não existe ou está
        /// corrompido — e nesses casos a partida em andamento continua intacta,
        /// em vez de virar um estado meio carregado.
        /// </summary>
        [ContextMenu("Save/Carregar agora")]
        public bool TryLoadNow()
        {
            if (_save == null) return false;
            if (!SaveFile.TryRead(out GameSave data, SaveSession.CurrentFileName)) return false;

            _save.Apply(data);
            return true;
        }

        [ContextMenu("Save/Apagar o arquivo")]
        public void DeleteSave() => SaveFile.TryDelete(SaveSession.CurrentFileName);

        /// <summary>
        /// Os objetivos são construídos DEPOIS da economia porque pagam em
        /// dinheiro e em XP: o livro-razão e o nível da loja já precisam existir.
        ///
        /// O <see cref="ObjectiveRunner"/> é adicionado aqui, e não deixado para
        /// o designer colocar na cena, porque ele não é uma peça de cenário — é o
        /// "depois do frame" de que o serviço precisa para pagar a recompensa
        /// fora do despacho do evento. Uma cena sem ele teria objetivos que
        /// enchem a barra e nunca concluem, e nada no console explicaria.
        /// </summary>
        private void BuildObjectives()
        {
            // Mesma armadilha do catálogo de produtos: um asset apagado do
            // projeto chega como "null falso" da Unity, e uma vez convertido em
            // referência de interface o `== null` lá dentro deixa de funcionar.
            _objectives = new ObjectiveService(
                _objectiveCatalog != null ? _objectiveCatalog : null,
                _events, _ledger, _storeLevel, _progress, _progress,
                _maxActiveObjectives);

            gameObject.AddComponent<ObjectiveRunner>().Bind(_objectives);
        }

        /// <summary>
        /// A roda de ferramentas e o contador de sujeira.
        ///
        /// Sem layout o cinto nasce com zero slots em vez de nulo: um jogador de
        /// mão vazia é um estado jogável, e um serviço nulo faria toda tentativa
        /// de usar ferramenta virar NullReferenceException na cena de teste.
        ///
        /// O contador de limpeza é o que faltava para o <c>IStoreCleanliness</c>
        /// que a IA do cliente consulta desde a Fase 4 — até agora ele resolvia
        /// para nulo, e "cliente vai embora de loja suja" nunca acontecia.
        /// </summary>
        private void BuildToolsAndCleaning()
        {
            _tools = new ToolBelt(_toolBelt != null ? _toolBelt : null, _progress, _events);

            _cleanliness = new CleanlinessService(_events)
            {
                ToleranceReference = _dirtTolerance,
            };
        }

        private void BuildEconomy()
        {
            _gameClock = new GameClock(_events, _clock);
            _ledger = new Ledger(_events, _gameClock, Money.FromYen(_openingBalanceYen));
            _expenses = new ExpenseService(_ledger);
            _reports = new DailyReportService(_events, _ledger, _gameClock.Day);

            _accountant = new SalesAccountant(_events, _ledger);
            _bank = new BankService(_ledger, _expenses, _progress, _events)
            {
                MaxConcurrentLoans = _maxConcurrentLoans,
            };

            // O relógio e o contexto de desbloqueio não são opcionais aqui: sem
            // o primeiro nada faz o prazo de entrega vencer, e sem o segundo um
            // produto de nível 10 é comprável no nível 1.
            //
            // O catálogo entra pelo ternário, e não direto: um asset arrastado
            // no Inspector e depois apagado do projeto chega aqui como o "null
            // falso" da Unity. Passado assim para o serviço, ele vira uma
            // referência de INTERFACE — que perde a sobrecarga de `==` — e o
            // guard `_catalog == null` lá dentro daria false para um objeto
            // destruído, estourando na primeira leitura da lista de produtos.
            _market = new MarketOrderService(_ledger, _pricing, _gameClock, _progress,
                                             _itemCatalog != null ? _itemCatalog : null,
                                             _events)
            {
                MaxPendingOrders = _maxPendingOrders,
                DeliveryHours = _deliveryHours,
            };
            _dayCycle = new DayCycle(_gameClock, _expenses, _reports, _events, _ledger);

            // O caminhão do lixo é o fechamento do dia, não um objeto na cena.
            // Registrado como fonte de RECEITA para que o que a doca acumulou
            // entre no relatório do dia em que foi entregue — assinar DayEnded
            // jogaria o dinheiro no relatório do dia seguinte.
            _trash = new TrashService(_events);
            _dayCycle.RegisterIncome(_trash);

            if (_dailyRentYen > 0)
                _expenses.Register(new FlatExpense(
                    "Aluguel", TransactionReason.Rent, Money.FromYen(_dailyRentYen)));

            _expenses.Register(new PowerExpense(_furniture));

            _daySubscription = _events.Subscribe<DayStarted>(e => _progress.SetDay(e.Day));
        }

        private void RegisterCoreServices()
        {
            _container.Register<IEventBus>(_events);
            _container.Register<IStoreLevelService>(_storeLevel);
            _container.Register<IUnlockContext>(_progress);
            _container.Register<IFurnitureRegistry>(_furniture);

            _container.Register<IPricingService>(_pricing);
            _container.Register<ICheckoutService>(_checkout);

            _container.Register<IGameClock>(_gameClock);
            _container.Register<ILedger>(_ledger);
            _container.Register<IExpenseService>(_expenses);
            _container.Register<IBankService>(_bank);
            _container.Register<IMarketOrderService>(_market);
            _container.Register<IObjectiveService>(_objectives);
            _container.Register<ITrashService>(_trash);
            _container.Register<IToolBelt>(_tools);
            _container.Register<IDailyReportService>(_reports);

            // Os dois lados da limpeza, registrados separados: o cliente recebe
            // só a leitura, e quem registra sujeira recebe só a escrita.
            _container.Register<IStoreCleanliness>(_cleanliness);
            _container.Register<IDirtRegistry>(_cleanliness);

            if (_itemCatalog != null)
            {
                _itemCatalog.Rebuild();
                _container.Register<IItemCatalog>(_itemCatalog);
            }
            else WarnMissingCatalog("ItemCatalog", "produtos");

            if (_furnitureCatalog != null)
            {
                _furnitureCatalog.Rebuild();
                _container.Register<IFurnitureCatalog>(_furnitureCatalog);
            }
            else WarnMissingCatalog("FurnitureCatalog", "móveis");

            // Sem aviso quando falta: uma loja sem objetivos é um estado
            // jogável, ao contrário de uma sem produtos. Reclamar aqui seria
            // barulho em toda cena de teste.
            if (_objectiveCatalog != null)
            {
                _objectiveCatalog.Rebuild();
                _container.Register<IObjectiveCatalog>(_objectiveCatalog);
            }

            if (_trashCatalog != null)
            {
                _trashCatalog.Rebuild();
                _container.Register<ITrashCatalog>(_trashCatalog);
            }

            if (_loanCatalog != null)
            {
                _loanCatalog.Rebuild();
                _container.Register<ILoanCatalog>(_loanCatalog);
            }

            if (!_validateCatalogOnPlay) return;

            if (_itemCatalog != null) Validate(_itemCatalog);
            if (_furnitureCatalog != null) Validate(_furnitureCatalog);
            if (_objectiveCatalog != null) Validate(_objectiveCatalog);
            if (_trashCatalog != null) Validate(_trashCatalog);
            if (_loanCatalog != null) Validate(_loanCatalog);
        }

        private void WarnMissingCatalog(string assetType, string what) =>
            Debug.LogWarning($"[GameContext] Nenhum {assetType} atribuído — os sistemas " +
                             $"novos rodam sem catálogo de {what}. Crie um em " +
                             "Assets → Create → Japan Market e arraste aqui.", this);

        private void Validate(IValidatableCatalog catalog)
        {
            List<CatalogProblem> problems = catalog.Validate();
            if (problems.Count == 0)
            {
                Debug.Log($"[Catálogo] {catalog.CatalogName}: {catalog.EntryCount} " +
                          "entradas, nenhum problema.", this);
                return;
            }

            foreach (CatalogProblem problem in problems)
            {

                UnityEngine.Object context =
                    problem.Asset != null ? problem.Asset : (UnityEngine.Object)this;
                Debug.LogWarning($"[Catálogo] {catalog.CatalogName} · " +
                                 $"{problem.AssetName}: {problem.Message}", context);
            }
        }

        /// <summary>
        /// A derrubada é deliberadamente passiva — [DefaultExecutionOrder(-10000)]
        /// adianta o OnDestroy junto com o Awake, e vários MonoBehaviours legados
        /// consultam serviços ao morrer.
        ///
        /// A exceção são as assinaturas de objetos de Domain: elas não tocam em
        /// serviço nenhum que o OnDestroy legado vá consultar, e a regra 3 do
        /// README manda soltá-las aqui — TODAS, e não três das oito. Soltar um
        /// subconjunto é pior que não soltar nenhuma: dá a impressão de que a
        /// derrubada está coberta, e o próximo serviço adicionado aqui entra
        /// fora da lista sem que nada acuse.
        /// </summary>
        private void OnDestroy()
        {
            DisposeDomain();

            if (Current == this) Current = null;
        }

        /// <summary>
        /// Solta todas as assinaturas de Domain. Idempotente: cada Dispose aqui
        /// ou zera o campo que guarda a inscrição, ou remove um handler de
        /// evento — as duas coisas aguentam ser chamadas de novo, que é o que
        /// acontece quando <see cref="Teardown"/> roda antes do OnDestroy.
        /// </summary>
        private void DisposeDomain()
        {
            _daySubscription?.Dispose();
            _saveSubscription?.Dispose();
            _dayCycle?.Dispose();
            _accountant?.Dispose();
            _market?.Dispose();
            _objectives?.Dispose();
            _tools?.Dispose();
            _reports?.Dispose();
            _ledger?.Dispose();
            _storeLevel?.Dispose();
            _bank?.Dispose();
        }

        public void Teardown()
        {
            DisposeDomain();

            _events?.Clear();
            _container?.Clear();
            if (_container != null) ServiceContainer.ClearCurrent(_container);
            if (Current == this) Current = null;
        }
    }
}
