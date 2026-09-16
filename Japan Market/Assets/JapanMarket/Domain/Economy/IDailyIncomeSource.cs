using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Uma receita apurada no fechamento do dia.
    ///
    /// É o espelho do <see cref="IExpenseSource"/>, e existe pelo mesmo motivo
    /// que ele: a ordem do fechamento importa. O caminhão do lixo passa no fim
    /// do dia, e o que ele paga precisa entrar ANTES de o relatório fechar —
    /// senão a receita aparece no relatório do dia SEGUINTE, e o jogador vê um
    /// dia que rendeu menos do que rendeu.
    ///
    /// Sem esta interface a alternativa seria o serviço de lixo assinar
    /// <c>DayEnded</c>, que o <see cref="DayCycle"/> publica DEPOIS de fechar o
    /// relatório — ou assinar <c>EndOfDayReached</c> e torcer para ter assinado
    /// antes do DayCycle, que é ordem de inscrição e não é controlável.
    /// </summary>
    public interface IDailyIncomeSource
    {
        /// <summary>Como a linha aparece no relatório: "Reciclagem".</summary>
        string Label { get; }

        TransactionReason Reason { get; }

        /// <summary>
        /// Apura e ENTREGA: o que for devolvido é depositado por quem chamou, e
        /// a fonte deve considerar o valor já cobrado — chamar duas vezes no
        /// mesmo dia não pode pagar duas vezes.
        /// </summary>
        Money Collect();
    }
}
