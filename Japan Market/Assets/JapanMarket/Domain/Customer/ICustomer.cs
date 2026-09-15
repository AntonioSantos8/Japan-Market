using UnityEngine;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Um cliente, visto pelos sistemas que precisam falar com ele — fila do
    /// caixa, estatísticas, objetivos.
    ///
    /// Ninguém de fora comanda o cliente diretamente. Quem quer que ele faça
    /// algo <see cref="Notify"/> um fato, e o cérebro dele decide o que fazer.
    /// É essa inversão que evita o que acontece hoje, onde o CashRegister chama
    /// <c>SetTarget</c> em todos os NPCs da fila e reinicia quem já tinha
    /// chegado no lugar certo.
    /// </summary>
    public interface ICustomer
    {
        int Id { get; }
        Vector3 Position { get; }
        bool IsAlive { get; }

        /// <summary>Informa um fato do mundo. O cliente decide como reagir.</summary>
        void Notify(CustomerSignal signal);
    }

    /// <summary>
    /// Fatos que o mundo comunica ao cliente.
    ///
    /// O conjunto é deliberadamente pequeno: só entra aqui o que o cliente NÃO
    /// consegue descobrir perguntando. "Sou o primeiro da fila?" e "em que
    /// posição eu estou?" ele pergunta à estação todo frame, e por isso não são
    /// sinais — um aviso pode se perder ou chegar fora de ordem, uma consulta
    /// não. Já "o caixa sumiu" precisa ser empurrado, porque quando o cliente
    /// fosse perguntar já não haveria a quem.
    /// </summary>
    public enum CustomerSignal
    {
        /// <summary>A loja fechou as portas.</summary>
        StoreClosed = 0,

        /// <summary>A estação onde ele estava na fila deixou de existir.</summary>
        CheckoutLost = 1,

        /// <summary>A venda dele foi concluída.</summary>
        SaleFinished = 3,
    }
}
