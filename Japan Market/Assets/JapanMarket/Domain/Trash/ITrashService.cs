using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Os sacos que já estão nos fundos esperando o caminhão.
    ///
    /// O caminhão não é um objeto na cena: é o fechamento do dia. O serviço é
    /// uma <see cref="IDailyIncomeSource"/> registrada no <c>DayCycle</c>, e é
    /// por isso que a reciclagem aparece como uma linha de receita no relatório
    /// do dia em que foi entregue, e não no seguinte.
    ///
    /// C# puro: nenhum saco, nenhuma lixeira e nenhum caminhão aqui dentro sabem
    /// que existe uma cena.
    /// </summary>
    public interface ITrashService : IDailyIncomeSource
    {
        IReadOnlyList<TrashBag> Pending { get; }

        /// <summary>Quanto o caminhão pagaria se passasse agora.</summary>
        Money PendingValue { get; }

        int PendingBags { get; }

        /// <summary>
        /// Deixa um saco nos fundos. Recusa saco vazio — deixar um saco sem nada
        /// dentro só encheria a doca e renderia ¥0.
        /// </summary>
        bool TryDeposit(TrashBag bag);

        /// <summary>O caminhão passou. Publicado depois do pagamento.</summary>
        event Action<int, Money> Collected;

        /// <summary>Um saco entrou na doca.</summary>
        event Action<TrashBag> Deposited;
    }
}
