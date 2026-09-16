using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>"Venda 50 itens." Conta ITENS, não vendas.</summary>


    /// <summary>
    /// "Fature ¥20.000." Soma a RECEITA das vendas, e não o saldo.
    ///
    /// Receita, de propósito: um objetivo amarrado ao saldo é cumprido pegando
    /// empréstimo, e desfeito pagando o aluguel. O que o jogador fez continua
    /// feito mesmo que o dinheiro já tenha saído.
    /// </summary>


    /// <summary>"Atenda 30 clientes." Só conta quem saiu tendo comprado.</summary>


    /// <summary>"Sobreviva 7 dias." Conta fechamentos de dia, não aberturas.</summary>


    /// <summary>"Receba 20 caixas de estoque."</summary>


    /// <summary>
    /// "Recicle 20 itens." Opcionalmente de uma categoria só.
    ///
    /// O filtro é pela CHAVE da categoria, em texto, e não por referência ao
    /// asset: Data pode referenciar Data, mas o evento vem de Core, que não
    /// enxerga o <c>TrashCategory</c>. Deixar o filtro vazio conta tudo.
    /// </summary>


    /// <summary>
    /// "Chegue ao nível 5."
    ///
    /// Usa <c>Set</c>, e não <c>Add</c>: o nível é um estado, não um acumulado.
    /// Somar cada aviso de mudança faria subir do 1 ao 3 valer três, e o objetivo
    /// "chegue ao nível 3" seria cumprido no nível 3 por coincidência — e o
    /// "chegue ao nível 10" seria cumprido no nível 4.
    /// </summary>

}

