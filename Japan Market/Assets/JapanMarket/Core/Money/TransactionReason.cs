namespace JapanMarket.Core
{

    public enum TransactionReason
    {
        Unknown            = 0,

        ProductSale        = 100,
        RecycledTrash      = 110,
        LoanReceived       = 120,
        VendingMachine     = 130,

        StockPurchase      = 200,
        FurniturePurchase  = 210,
        UpgradePurchase    = 220,
        ExpansionPurchase  = 230,
        CustomizationCost  = 240,
        LicensePurchase    = 250,

        Rent               = 300,
        Electricity        = 310,
        Salary             = 320,
        LoanInstallment    = 330,

        WrongChangePenalty = 400,
        WrongTrashPenalty  = 410,
    }
}
