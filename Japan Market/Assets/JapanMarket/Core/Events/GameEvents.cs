using UnityEngine;

namespace JapanMarket.Core
{

    public readonly struct BalanceChanged : IGameEvent
    {
        public readonly Money Previous;
        public readonly Money Current;
        public readonly TransactionReason Reason;

        public Money Delta => Current - Previous;

        public BalanceChanged(Money previous, Money current, TransactionReason reason)
        {
            Previous = previous;
            Current  = current;
            Reason   = reason;
        }
    }

    public readonly struct TransactionRecorded : IGameEvent
    {
        public readonly Money Amount;               
        public readonly TransactionReason Reason;
        public readonly int Day;

        public TransactionRecorded(Money amount, TransactionReason reason, int day)
        {
            Amount = amount;
            Reason = reason;
            Day    = day;
        }
    }

    public readonly struct DayStarted : IGameEvent
    {
        public readonly int Day;
        public DayStarted(int day) => Day = day;
    }

    public readonly struct DayEnded : IGameEvent
    {
        public readonly int Day;
        public DayEnded(int day) => Day = day;
    }

    public readonly struct StoreOpenStateChanged : IGameEvent
    {
        public readonly bool IsOpen;
        public StoreOpenStateChanged(bool isOpen) => IsOpen = isOpen;
    }

    public readonly struct CustomerEntered : IGameEvent
    {
        public readonly int CustomerId;
        public readonly Transform Transform;

        public CustomerEntered(int customerId, Transform transform)
        {
            CustomerId = customerId;
            Transform  = transform;
        }
    }

    public readonly struct CustomerLeft : IGameEvent
    {
        public readonly int CustomerId;
        public readonly bool WasSatisfied;
        public readonly CustomerLeaveReason Reason;

        public CustomerLeft(int customerId, bool wasSatisfied, CustomerLeaveReason reason)
        {
            CustomerId   = customerId;
            WasSatisfied = wasSatisfied;
            Reason       = reason;
        }
    }

    public enum CustomerLeaveReason
    {
        Purchased        = 0,
        NothingToBuy     = 1,
        StoreTooDirty    = 2,
        PricesTooHigh    = 3,
        NoCheckout       = 4,
        WaitedTooLong    = 5,
        StoreClosed      = 6,
    }

    public readonly struct SaleCompleted : IGameEvent
    {
        public readonly int CustomerId;
        public readonly FurnitureId StationId;

        public readonly Money Revenue;

        public readonly Money Cost;

        public readonly int ItemCount;
        public readonly PaymentMethod Method;

        public Money Profit => Revenue - Cost;

        public SaleCompleted(int customerId, FurnitureId stationId, Money revenue,
                             Money cost, int itemCount, PaymentMethod method)
        {
            CustomerId = customerId;
            StationId  = stationId;
            Revenue    = revenue;
            Cost       = cost;
            ItemCount  = itemCount;
            Method     = method;
        }
    }

    public readonly struct CleanlinessChanged : IGameEvent
    {
        public readonly int ActiveDirtCount;
        public readonly float Normalized;   

        public CleanlinessChanged(int activeDirtCount, float normalized)
        {
            ActiveDirtCount = activeDirtCount;
            Normalized      = normalized;
        }
    }

    /// <summary>
    /// O jogador fez um pedido de estoque. O dinheiro já saiu.
    ///
    /// Carrega totais, e não a lista de produtos, pela mesma razão do
    /// <see cref="SaleCompleted"/>: Core não enxerga Data. Quem precisa das
    /// linhas assina <c>IMarketOrderService.OrderPlaced</c>.
    /// </summary>
    public readonly struct StockOrderPlaced : IGameEvent
    {
        public readonly int OrderId;
        public readonly Money Total;
        public readonly int BoxCount;
        public readonly int Day;

        public StockOrderPlaced(int orderId, Money total, int boxCount, int day)
        {
            OrderId  = orderId;
            Total    = total;
            BoxCount = boxCount;
            Day      = day;
        }
    }

    /// <summary>As caixas de um pedido chegaram ao depósito.</summary>
    public readonly struct StockOrderDelivered : IGameEvent
    {
        public readonly int OrderId;
        public readonly int BoxCount;

        public StockOrderDelivered(int orderId, int boxCount)
        {
            OrderId  = orderId;
            BoxCount = boxCount;
        }
    }

    /// <summary>
    /// O nível ou o XP da loja mudou.
    ///
    /// O <c>StoreLevelService</c> já tinha eventos C# para isto, e eles
    /// continuam — quem tem a referência ao serviço usa aqueles. Este existe
    /// para quem NÃO tem: uma condição de objetivo é um ScriptableObject em
    /// Data, e Data não enxerga Domain. Sem o evento no barramento, "chegue ao
    /// nível 5" exigiria que o objetivo conhecesse o serviço — e aí Data
    /// passaria a depender de Domain para sempre.
    /// </summary>
    public readonly struct StoreLevelChanged : IGameEvent
    {
        public readonly int Level;
        public readonly int XP;

        /// <summary>Quantos níveis subiram de uma vez. 0 quando só o XP mudou.</summary>
        public readonly int LevelsGained;

        public StoreLevelChanged(int level, int xp, int levelsGained)
        {
            Level        = level;
            XP           = xp;
            LevelsGained = levelsGained;
        }
    }

    /// <summary>
    /// Um objetivo foi concluído e a recompensa já foi paga.
    ///
    /// Carrega o id e o texto, e não o objetivo: Core não enxerga Data, e o
    /// <c>ObjectiveDefinition</c> mora lá. Quem precisa do objeto inteiro assina
    /// <c>IObjectiveService.ObjectiveCompleted</c>, que é de Domain.
    /// </summary>
    public readonly struct ObjectiveCompleted : IGameEvent
    {
        public readonly ObjectiveId Id;
        public readonly string Title;
        public readonly Money Reward;
        public readonly int XPReward;

        public ObjectiveCompleted(ObjectiveId id, string title, Money reward, int xpReward)
        {
            Id       = id;
            Title    = title;
            Reward   = reward;
            XPReward = xpReward;
        }
    }

    /// <summary>
    /// Um lixo foi para dentro de um saco.
    ///
    /// Carrega a CHAVE da categoria e não a categoria: Core não enxerga Data, e
    /// o <c>TrashCategory</c> mora lá. A chave é a mesma que o save usa, então
    /// um objetivo do tipo "recicle 20 plásticos" consegue filtrar por ela.
    /// </summary>
    public readonly struct TrashDiscarded : IGameEvent
    {
        public readonly string CategoryKey;
        public readonly int BagCount;
        public readonly int BagCapacity;

        public bool BagIsFull => BagCount >= BagCapacity;

        public TrashDiscarded(string categoryKey, int bagCount, int bagCapacity)
        {
            CategoryKey = categoryKey;
            BagCount    = bagCount;
            BagCapacity = bagCapacity;
        }
    }

    /// <summary>Um saco foi deixado nos fundos, esperando o caminhão.</summary>
    public readonly struct TrashBagDeposited : IGameEvent
    {
        public readonly int ItemCount;
        public readonly Money Value;

        public TrashBagDeposited(int itemCount, Money value)
        {
            ItemCount = itemCount;
            Value     = value;
        }
    }

    /// <summary>O caminhão passou e levou tudo o que estava na doca.</summary>
    public readonly struct TrashCollected : IGameEvent
    {
        public readonly int BagCount;
        public readonly Money Total;

        public TrashCollected(int bagCount, Money total)
        {
            BagCount = bagCount;
            Total    = total;
        }
    }

    /// <summary>
    /// Uma ferramenta acabou de quebrar.
    ///
    /// Carrega o nome e o custo, e não a ferramenta: Core não enxerga Data. Quem
    /// precisa do asset assina <c>IToolBelt.Broke</c>, que é de Domain.
    /// </summary>
    public readonly struct ToolBroke : IGameEvent
    {
        public readonly int SlotIndex;
        public readonly string ToolName;
        public readonly Money RepairCost;

        public ToolBroke(int slotIndex, string toolName, Money repairCost)
        {
            SlotIndex  = slotIndex;
            ToolName   = toolName;
            RepairCost = repairCost;
        }
    }
}
