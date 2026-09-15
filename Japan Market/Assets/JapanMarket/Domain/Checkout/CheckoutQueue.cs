using System;
using System.Collections.Generic;

namespace JapanMarket.Domain
{
    /// <summary>
    /// A fila de UMA estação de checkout. C# puro, testável sem cena.
    ///
    /// Duas diferenças em relação à fila atual, que é um <c>List&lt;NpcTraject&gt;</c>
    /// dentro do CashRegister:
    ///
    /// 1. **Não vive dentro do caixa.** Quando a estação é removida, a fila
    ///    sabe avisar cada cliente em vez de sumir junto com o objeto.
    ///
    /// 2. **Só avisa quem mudou de lugar.** O <c>RefreshQueuePositions</c> atual
    ///    chama SetTarget em TODOS os NPCs sempre que alguém entra ou sai — e
    ///    quem já estava parado no ponto certo recebe isStopped = false seguido
    ///    de SetDestination, anda alguns centímetros e para de novo. É metade da
    ///    causa do NPC "dando passinhos" na fila.
    ///
    /// Também não existe mais array fixo de pontos: a posição sai de âncora +
    /// direção × espaçamento × índice, então a fila tem o tamanho que precisar.
    /// O código atual indexa <c>queuePoints[i]</c> pela contagem de clientes,
    /// sem checar o tamanho do array — mais clientes que pontos é
    /// IndexOutOfRangeException.
    /// </summary>
    public sealed class CheckoutQueue
    {
        private readonly List<ICustomer> _customers = new();

        /// <summary>(cliente, novo índice) — disparado só para quem mudou.</summary>
        public event Action<ICustomer, int> IndexChanged;

        public int Count => _customers.Count;
        public bool IsEmpty => _customers.Count == 0;
        public ICustomer Front => _customers.Count > 0 ? _customers[0] : null;

        public IReadOnlyList<ICustomer> Customers => _customers;

        public bool Contains(ICustomer customer) => IndexOf(customer) >= 0;

        public int IndexOf(ICustomer customer)
        {
            if (customer == null) return -1;

            for (int i = 0; i < _customers.Count; i++)
                if (ReferenceEquals(_customers[i], customer)) return i;

            return -1;
        }

        public bool IsFront(ICustomer customer) =>
            customer != null && ReferenceEquals(Front, customer);

        /// <summary>Entra no fim da fila. Devolve o índice, ou -1 se já estava nela.</summary>
        public int Enqueue(ICustomer customer)
        {
            if (customer == null) return -1;
            if (Contains(customer)) return IndexOf(customer);

            _customers.Add(customer);
            int index = _customers.Count - 1;

            IndexChanged?.Invoke(customer, index);
            return index;
        }

        /// <summary>
        /// Tira alguém da fila e reindexa. Só quem realmente mudou de posição
        /// recebe aviso — quem estava na frente do removido não recebe nada.
        /// </summary>
        public bool Remove(ICustomer customer)
        {
            int index = IndexOf(customer);
            if (index < 0) return false;

            _customers.RemoveAt(index);

            for (int i = index; i < _customers.Count; i++)
                IndexChanged?.Invoke(_customers[i], i);

            return true;
        }

        /// <summary>
        /// Esvazia a fila avisando cada cliente de que o caixa sumiu.
        ///
        /// É o que roda quando o jogador arranca a registradora no meio do
        /// expediente. Cada cliente recebe o sinal e decide sozinho — procurar
        /// outro caixa ou ir embora — em vez de acordar com uma referência
        /// pendurada.
        /// </summary>
        public void DisbandAll()
        {
            if (_customers.Count == 0) return;

            // Cópia antes de limpar: um cliente pode reagir ao sinal tentando
            // sair da fila, o que mutaria a lista durante a iteração.
            ICustomer[] leaving = _customers.ToArray();
            _customers.Clear();

            for (int i = 0; i < leaving.Length; i++)
                leaving[i]?.Notify(CustomerSignal.CheckoutLost);
        }

        /// <summary>
        /// Remove clientes que morreram sem sair da fila direito. Devolve true
        /// se removeu alguém.
        ///
        /// Avisa só de quem realmente andou — a mesma invariante do
        /// <see cref="Remove"/>. Quem estava à frente do primeiro removido não
        /// mudou de lugar e não recebe nada; reindexar a fila inteira aqui
        /// reintroduziria, por uma varredura de manutenção, o mesmo tráfego
        /// inútil que o RefreshQueuePositions atual gera.
        /// </summary>
        public bool PruneDead()
        {
            int firstRemoved = -1;

            for (int i = _customers.Count - 1; i >= 0; i--)
            {
                if (_customers[i] != null && _customers[i].IsAlive) continue;

                _customers.RemoveAt(i);
                firstRemoved = i;
            }

            if (firstRemoved < 0) return false;

            for (int i = firstRemoved; i < _customers.Count; i++)
                IndexChanged?.Invoke(_customers[i], i);

            return true;
        }
    }
}
