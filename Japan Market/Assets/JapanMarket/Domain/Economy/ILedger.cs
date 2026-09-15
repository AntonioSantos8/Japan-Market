using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// O dinheiro da loja e o registro de tudo o que entrou e saiu.
    ///
    /// Substitui o <c>float money</c> do MarketManager, que hoje é saldo, texto
    /// na tela, tween, som e partícula no mesmo lugar. Aqui é só o número e o
    /// histórico; quem quiser desenhar assina o evento.
    ///
    /// Repare na diferença entre <see cref="TryWithdraw"/> e <see cref="Charge"/>.
    /// Ela não é enfeite: comprar uma prateleira sem dinheiro tem que FALHAR e
    /// a loja continua igual; o aluguel do dia é cobrado com dinheiro ou sem, e
    /// é assim que o jogador entra no vermelho e precisa do banco. O
    /// <c>Lose_Money</c> atual faz a coisa mais perigosa possível entre as duas:
    /// devolve void e simplesmente não desconta quando falta saldo, então quem
    /// chamou acha que comprou.
    /// </summary>
    public interface ILedger
    {
        Money Balance { get; }

        /// <summary>Movimentações do dia corrente, na ordem em que aconteceram.</summary>
        IReadOnlyList<Transaction> Today { get; }

        /// <summary>Entrada de dinheiro. Valores não positivos são ignorados.</summary>
        void Deposit(Money amount, TransactionReason reason, string note = null);

        /// <summary>
        /// Saída OPCIONAL: só acontece se houver saldo. Devolve false sem mexer
        /// em nada quando não há — é o contrato de toda compra do jogador.
        /// </summary>
        bool TryWithdraw(Money amount, TransactionReason reason, string note = null);

        /// <summary>
        /// Saída OBRIGATÓRIA: acontece mesmo deixando o saldo negativo. Aluguel,
        /// luz, salário, multa.
        /// </summary>
        void Charge(Money amount, TransactionReason reason, string note = null);

        /// <summary>Consegue pagar isso agora?</summary>
        bool CanAfford(Money amount);

        /// <summary>Uma linha foi registrada. Carrega o sinal no valor.</summary>
        event Action<Transaction> Recorded;
    }
}
