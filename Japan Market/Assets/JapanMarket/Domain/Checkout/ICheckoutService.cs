using System;
using UnityEngine;

namespace JapanMarket.Domain
{
    /// <summary>
    /// As duas decisões de checkout que não pertencem a nenhuma estação
    /// específica.
    ///
    /// Este serviço existe por dois problemas concretos, não por padrão:
    ///
    /// 1. **Escolher a caixa.** A política ("a operante mais vazia que dá para
    ///    alcançar") tem que valer igual para todo cliente e ser testável sem
    ///    abrir cena. Se ela morar dentro do estado do NPC, cada mudança de
    ///    regra vira uma mudança na IA — e não há como testá-la sem NavMesh.
    ///
    /// 2. **Fechar a venda.** Concluir uma venda é onde o dinheiro entra, o
    ///    evento é publicado e o cliente é liberado. Uma capacidade de móvel
    ///    (MonoBehaviour) não deve conhecer o livro-razão nem o barramento; a
    ///    estação guarda o estado da venda, o serviço faz o fechamento e avisa
    ///    a estação.
    ///
    /// O que este serviço deliberadamente NÃO faz: mover NPC, desenhar UI,
    /// tocar som, animar sacola. Ele é C# puro e roda em teste de unidade.
    /// </summary>
    public interface ICheckoutService
    {
        /// <summary>
        /// A melhor estação para atender alguém que está em <paramref name="from"/>.
        ///
        /// <paramref name="canReach"/> é opcional e existe para o chamador que
        /// tem navegação (o NPC) poder descartar caixas ilhadas sem que o
        /// serviço conheça NavMesh. Passando null, todas as operantes contam.
        /// </summary>
        bool TryFindBestStation(Vector3 from, Predicate<Vector3> canReach,
                                out ICheckoutStation station);

        /// <summary>
        /// Conclui a venda aberta na estação: marca como paga, libera o cliente
        /// e publica o evento. Quem transforma o evento em saldo é a economia,
        /// na Fase 6 — o checkout não conhece o livro-razão.
        ///
        /// Devolve false se não havia venda, se ela já estava concluída ou se
        /// ainda falta item para passar — nenhum desses casos é erro, são
        /// tentativas fora de hora.
        /// </summary>
        bool TryCompleteSale(ICheckoutStation station);

        /// <summary>
        /// Uma venda foi concluída, com a sessão inteira (linhas, produtos,
        /// preços). É o canal de alta fidelidade, para quem vive em Domain ou
        /// acima: estatísticas por produto, objetivos, lucro da tela de Preços.
        ///
        /// O canal de baixa fidelidade é o evento <c>SaleCompleted</c> no
        /// barramento, que carrega só totais porque Core não enxerga Data e
        /// portanto não pode falar de ItemDefinition.
        /// </summary>
        event Action<CheckoutSession> SaleCompleted;
    }
}
