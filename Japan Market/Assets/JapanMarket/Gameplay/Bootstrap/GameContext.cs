using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GameContext : MonoBehaviour
    {
        [Header("Catalogs")]
        [Tooltip("O único catálogo de produtos do projeto. Populado automaticamente " +
                 "pelo import — não arraste produtos à mão.")]
        [SerializeField] private ItemCatalog _itemCatalog;

        [Tooltip("The only furniture catalog. Same rule: populated by the import.")]
        [SerializeField] private FurnitureCatalog _furnitureCatalog;

        [Header("Economy")]
        [Tooltip("Starting balance for the store in a new game, in yen.")]
        [SerializeField] private long _openingBalanceYen = 8000;

        [Tooltip("Rent charged at the end of every day, in yen.")]
        [SerializeField] private long _dailyRentYen = 100;

        [Header("Time")]
        [SerializeField] private GameClockSettings _clock = GameClockSettings.Default;

        [Header("Diagnosis")]
        [Tooltip("Validates the catalog when entering Play and lists problems in the console.")]
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
        private DayCycle _dayCycle;
        private IDisposable _daySubscription;

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

            ServiceContainer.SetCurrent(_container);

            RegisterCoreServices();
        }

        private void BuildEconomy()
        {
            _gameClock = new GameClock(_events, _clock);
            _ledger = new Ledger(_events, _gameClock, Money.FromYen(_openingBalanceYen));
            _expenses = new ExpenseService(_ledger);
            _reports = new DailyReportService(_events, _ledger, _gameClock.Day);

            _accountant = new SalesAccountant(_events, _ledger);
            _bank = new BankService(_ledger, _expenses, _progress, _events);
            _market = new MarketOrderService(_ledger, _pricing);
            _dayCycle = new DayCycle(_gameClock, _expenses, _reports, _events);

            if (_dailyRentYen > 0)
                _expenses.Register(new FlatExpense(
                    "Rent", TransactionReason.Rent, Money.FromYen(_dailyRentYen)));

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
            _container.Register<IDailyReportService>(_reports);

            if (_itemCatalog != null)
            {
                _itemCatalog.Rebuild();
                _container.Register<IItemCatalog>(_itemCatalog);
            }
            else WarnMissingCatalog("ItemCatalog", "products");

            if (_furnitureCatalog != null)
            {
                _furnitureCatalog.Rebuild();
                _container.Register<IFurnitureCatalog>(_furnitureCatalog);
            }
            else WarnMissingCatalog("FurnitureCatalog", "furniture");

            if (!_validateCatalogOnPlay) return;

            if (_itemCatalog != null) Validate(_itemCatalog);
            if (_furnitureCatalog != null) Validate(_furnitureCatalog);
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

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void Teardown()
        {
            _daySubscription?.Dispose();
            _dayCycle?.Dispose();
            _accountant?.Dispose();
            _reports?.Dispose();
            _ledger?.Dispose();
            _storeLevel?.Dispose();
            _bank?.Dispose();

            _events?.Clear();
            _container?.Clear();
            if (_container != null) ServiceContainer.ClearCurrent(_container);
            if (Current == this) Current = null;
        }
    }
}
