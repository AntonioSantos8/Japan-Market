using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Móvel que gasta energia. O <c>ExpenseService</c> (Fase 6) soma todos os
    /// registrados no fim do dia.
    ///
    /// O aviso da referência — "quantos mais dispositivos elétricos, mais caras
    /// as despesas" — deixa de ser uma regra escrita à parte e passa a ser
    /// consequência do modelo: a conta de luz é literalmente
    /// <c>registry.WithCapability&lt;IPowerConsumer&gt;()</c> somado.
    /// </summary>
    public interface IPowerConsumer : IFurnitureCapability
    {
        /// <summary>Custo por dia enquanto ligado.</summary>
        Money DailyCost { get; }

        /// <summary>Desligado não consome. O jogador pode querer economizar à noite.</summary>
        bool IsPoweredOn { get; }

        void SetPowered(bool on);
    }
}
