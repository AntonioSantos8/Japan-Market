namespace JapanMarket.Core
{
    /// <summary>
    /// Como o cliente paga.
    ///
    /// Mora em Core, junto de <see cref="Money"/>, e não em Domain, porque o
    /// evento <c>SaleCompleted</c> precisa carregá-lo — e Core não pode
    /// referenciar Domain sem inverter a direção das dependências.
    ///
    /// A diferença é de jogabilidade, não de contabilidade: dinheiro abre o
    /// minigame do troco, cartão só exige digitar o valor certo. O caixa recebe
    /// o mesmo total nos dois casos.
    /// </summary>
    public enum PaymentMethod
    {
        Cash = 0,
        Card = 1,
    }
}
