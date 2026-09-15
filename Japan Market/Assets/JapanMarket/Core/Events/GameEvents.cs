using UnityEngine;

namespace JapanMarket.Core
{
    // ─────────────────────────────────────────────────────────────────────────
    // Eventos de domínio.
    //
    // Regra: um evento descreve algo que JÁ ACONTECEU, no passado, e carrega o
    // mínimo necessário para quem reage. Nada de "pedidos" disfarçados de evento
    // ("PleaseOpenStore") — para pedir, chame o serviço.
    //
    // Este arquivo cresce ao longo das fases. Os eventos abaixo são os que as
    // fases 1 a 3 precisam; checkout, economia e objetivos entram nas suas.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>O saldo da loja mudou. Quem desenha número na tela assina isto.</summary>
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

    /// <summary>Uma movimentação foi registrada no livro-razão do dia.</summary>
    public readonly struct TransactionRecorded : IGameEvent
    {
        public readonly Money Amount;               // positivo = entrada
        public readonly TransactionReason Reason;
        public readonly int Day;

        public TransactionRecorded(Money amount, TransactionReason reason, int day)
        {
            Amount = amount;
            Reason = reason;
            Day    = day;
        }
    }

    /// <summary>Um novo dia de operação começou.</summary>
    public readonly struct DayStarted : IGameEvent
    {
        public readonly int Day;
        public DayStarted(int day) => Day = day;
    }

    /// <summary>
    /// O expediente acabou: contas cobradas, relatório fechado.
    ///
    /// Carrega só o número do dia. O relatório em si é um objeto de Domain (fala
    /// de despesas e de motivos de desistência), que Core não enxerga — quem
    /// quiser os números pede ao <c>IDailyReportService</c>.
    /// </summary>
    public readonly struct DayEnded : IGameEvent
    {
        public readonly int Day;
        public DayEnded(int day) => Day = day;
    }

    /// <summary>A loja abriu ou fechou as portas.</summary>
    public readonly struct StoreOpenStateChanged : IGameEvent
    {
        public readonly bool IsOpen;
        public StoreOpenStateChanged(bool isOpen) => IsOpen = isOpen;
    }

    /// <summary>Um cliente entrou na loja.</summary>
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

    /// <summary>Um cliente saiu da loja — satisfeito ou não.</summary>
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

    /// <summary>
    /// Uma venda foi concluída num caixa.
    ///
    /// É o evento central do item 13 do refatoramento: daqui saem economia,
    /// objetivos e estatísticas, sem que o checkout conheça nenhum dos três.
    ///
    /// Carrega totais, e não a lista de produtos, porque Core não enxerga Data
    /// e portanto não pode falar de ItemDefinition. Quem precisa do detalhe por
    /// produto assina <c>ICheckoutService.SaleCompleted</c>, que entrega a
    /// sessão inteira — mesmo fato, duas fidelidades, cada uma na camada que
    /// consegue enxergá-la.
    /// </summary>
    public readonly struct SaleCompleted : IGameEvent
    {
        public readonly int CustomerId;
        public readonly FurnitureId StationId;

        /// <summary>Quanto o cliente pagou.</summary>
        public readonly Money Revenue;

        /// <summary>Quanto as unidades vendidas custaram para comprar.</summary>
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

    /// <summary>O nível de sujeira da loja mudou.</summary>
    public readonly struct CleanlinessChanged : IGameEvent
    {
        public readonly int ActiveDirtCount;
        public readonly float Normalized;   // 0 = limpa, 1 = no limite

        public CleanlinessChanged(int activeDirtCount, float normalized)
        {
            ActiveDirtCount = activeDirtCount;
            Normalized      = normalized;
        }
    }
}
