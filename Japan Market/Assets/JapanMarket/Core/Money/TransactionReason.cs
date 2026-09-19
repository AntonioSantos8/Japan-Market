namespace JapanMarket.Core
{
    /// <summary>
    /// Motivo de uma movimentação financeira. É o que permite o relatório diário
    /// ser uma projeção do livro-razão, em vez de um punhado de contadores
    /// espalhados pelos sistemas de gameplay.
    ///
    /// Este é um conjunto controlado pela engenharia (não é conteúdo), então enum
    /// aqui é adequado — ao contrário da identidade de produto. Ainda assim, os
    /// valores são explícitos para que a persistência sobreviva a reordenação.
    /// </summary>
    public enum TransactionReason
    {
        Unknown            = 0,

        // entradas
        ProductSale        = 100,
        RecycledTrash      = 110,
        LoanReceived       = 120,
        VendingMachine     = 130,
        ObjectiveReward    = 140,

        // saídas
        StockPurchase      = 200,
        FurniturePurchase  = 210,
        UpgradePurchase    = 220,
        ExpansionPurchase  = 230,
        CustomizationCost  = 240,
        LicensePurchase    = 250,

        // recorrentes
        Rent               = 300,
        Electricity        = 310,
        Salary             = 320,
        LoanInstallment    = 330,

        // penalidades
        WrongChangePenalty = 400,
        WrongTrashPenalty  = 410,
    }
}
