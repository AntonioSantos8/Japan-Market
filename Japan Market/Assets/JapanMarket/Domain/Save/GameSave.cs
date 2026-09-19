using System;

namespace JapanMarket.Domain
{
    /// <summary>
    /// O save inteiro, como DADO puro.
    ///
    /// Regras que este arquivo segue, e por quê:
    ///
    ///  • Só tipos que o <c>JsonUtility</c> entende: classes [Serializable] com
    ///    campos PÚBLICOS, arrays, e primitivos. Nada de Dictionary, interface,
    ///    propriedade ou nullable — o JsonUtility ignora tudo isso em silêncio, e
    ///    um save que grava metade dos dados sem erro nenhum é o pior defeito
    ///    possível nesta camada.
    ///
    ///  • Dinheiro viaja como <c>long</c> de ienes, e não como <c>Money</c>.
    ///    Money funcionaria, mas o campo serializado dele chama <c>_yen</c>, e um
    ///    dia alguém abriria o JSON, veria "_yen" e "consertaria" para "Yen" — e
    ///    aí todo save carregaria com zero, sem erro.
    ///
    ///  • Referências a asset viajam como ID ou CHAVE, nunca como referência. Um
    ///    asset apagado do projeto entre duas versões do jogo vira um id que não
    ///    resolve, e quem carrega decide o que fazer — em vez de o save inteiro
    ///    morrer numa NullReferenceException.
    ///
    ///  • Tudo é opcional na leitura. Um save gravado por uma versão anterior não
    ///    tem os campos novos, e eles chegam nulos ou zerados. Quem aplica trata
    ///    nulo como "não havia" e segue.
    /// </summary>
    [Serializable]
    public sealed class GameSave
    {
        /// <summary>
        /// Versão do FORMATO. Sobe quando um campo muda de significado, não
        /// quando um campo novo aparece — campo novo já chega vazio sozinho.
        /// </summary>
        // Zero identifies JSON without a save version. SaveService.Capture sets
        // CurrentVersion explicitly when producing a real save.
        public int Version;

        public const int CurrentVersion = 1;

        public string SavedAtUtc;

        public ClockSave Clock = new();
        public LedgerSave Ledger = new();
        public ProgressSave Progress = new();
        public MarketSave Market = new();
        public ToolsSave Tools = new();

        public PricingSave[] Pricing = Array.Empty<PricingSave>();
        public LoanSave[] Loans = Array.Empty<LoanSave>();
        public ObjectiveSave[] Objectives = Array.Empty<ObjectiveSave>();
        public TrashBagSave[] TrashDock = Array.Empty<TrashBagSave>();
    }

    [Serializable]
    public sealed class ClockSave
    {
        public int Day = 1;
        public float TimeOfDay = 6f;
        public bool StoreOpen;
    }

    [Serializable]
    public sealed class LedgerSave
    {
        public long BalanceYen;
    }

    [Serializable]
    public sealed class ProgressSave
    {
        public int StoreLevel = 1;
        public int StoreXP;

        /// <summary>Flags levantadas. É o que liga objetivo a desbloqueio.</summary>
        public string[] Flags = Array.Empty<string>();
    }

    [Serializable]
    public sealed class PricingSave
    {
        public string ProductId;
        public bool HasCustomPrice;
        public long CustomPriceYen;
        public long LastCostYen;
        public long CurrentCostYen;
        public long TotalSpentYen;
        public int UnitsInAverage;
        public int LastChangeDay;
    }

    [Serializable]
    public sealed class MarketSave
    {
        public int NextOrderId = 1;
        public MarketOrderSave[] Pending = Array.Empty<MarketOrderSave>();

        /// <summary>Caixas já entregues que ainda não apareceram no depósito.</summary>
        public DeliveryLotSave[] Queue = Array.Empty<DeliveryLotSave>();
    }

    [Serializable]
    public sealed class MarketOrderSave
    {
        public int Id;
        public int DayPlaced;

        /// <summary>
        /// Em horas ABSOLUTAS, como o pedido guarda. É o número do relógio de
        /// mundo, e é por isso que o <c>GameClock.Restore</c> precisa reconstruir
        /// o contador com a mesma origem — senão um pedido salvo chega sozinho
        /// no carregamento, ou nunca mais chega.
        /// </summary>
        public float ArrivesAtTotalHours;

        public MarketLineSave[] Lines = Array.Empty<MarketLineSave>();
    }

    [Serializable]
    public sealed class MarketLineSave
    {
        public string ProductId;
        public int Boxes;
        public long BoxCostYen;
        public int UnitsPerBox;
    }

    [Serializable]
    public sealed class DeliveryLotSave
    {
        public string ProductId;
        public int Boxes;
    }

    [Serializable]
    public sealed class LoanSave
    {
        /// <summary>Chave da faixa de empréstimo, não o nome do asset.</summary>
        public string LoanKey;
        public int PaymentsMade;
    }

    [Serializable]
    public sealed class ObjectiveSave
    {
        public string ObjectiveId;
        public int[] Progress = Array.Empty<int>();
        public bool Completed;
    }

    [Serializable]
    public sealed class TrashBagSave
    {
        public string CategoryKey;

        /// <summary>Chave de cada item dentro do saco, na ordem em que entraram.</summary>
        public string[] ItemKeys = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ToolsSave
    {
        /// <summary>Usos restantes por slot, na ordem do layout.</summary>
        public int[] UsesPerSlot = Array.Empty<int>();

        public int SelectedIndex = -1;
    }
}
