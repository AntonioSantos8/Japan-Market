using System;

namespace JapanMarket.Domain
{
    /// <summary>
    /// O relógio da loja: que dia é, que horas são, e se a porta está aberta.
    ///
    /// Hoje nada disso existe. "A loja está aberta" é um bool dentro do
    /// <c>MarketManager</c>, escrito pela placa; "que dia é" não existe em lugar
    /// nenhum; e despesa diária não tem quando acontecer. Sem um relógio, o item
    /// 10 do refatoramento — "despesas processadas automaticamente pelo sistema
    /// de tempo" — não tem onde se pendurar.
    ///
    /// O relógio é C# puro e é ALIMENTADO de fora (<see cref="Tick"/>), em vez
    /// de ler <c>Time.deltaTime</c>. É isso que permite testar "o que acontece
    /// às 22h" em milissegundos, e que vai permitir pausar, acelerar e carregar
    /// um save no meio do dia sem nenhuma gambiarra.
    /// </summary>
    public interface IGameClock
    {
        /// <summary>Dia de operação. Começa em 1.</summary>
        int Day { get; }

        /// <summary>Hora do dia, 0 a 24. 13,5 é uma e meia da tarde.</summary>
        float TimeOfDay { get; }

        /// <summary>
        /// Horas decorridas desde o início da partida, atravessando a virada do
        /// dia.
        ///
        /// Existe porque prazo não cabe em <see cref="TimeOfDay"/>: uma entrega
        /// pedida às 23h para chegar em duas horas chega às 1h do dia seguinte,
        /// e comparar "1 &gt;= 25" dá falso para sempre. Com um relógio absoluto
        /// o prazo é uma subtração, e o pedido não fica pendente eternamente.
        /// </summary>
        float TotalHours { get; }

        /// <summary>A porta está aberta para clientes?</summary>
        bool StoreIsOpen { get; }

        /// <summary>O relógio anda? Falso durante o fechamento do dia e em menus.</summary>
        bool IsRunning { get; }

        /// <summary>Hora a partir da qual o jogador não pode mais abrir a loja.</summary>
        float ClosingHour { get; }

        /// <summary>Hora em que o dia termina e as contas são cobradas.</summary>
        float EndOfDayHour { get; }

        void Tick(float deltaSeconds);

        /// <summary>
        /// Abre a porta. Falso se já passou do horário — é o que impede o
        /// jogador de abrir às 23h e receber clientes por um minuto.
        /// </summary>
        bool TryOpenStore();

        void CloseStore();

        void SetRunning(bool running);

        /// <summary>
        /// Encerra o dia agora, sem esperar o relógio. É o botão "ir dormir".
        /// Quem escuta é o <see cref="DayCycle"/>, que roda a sequência de
        /// fechamento numa ordem definida.
        ///
        /// Devolve false quando o pedido é recusado: o dia já foi encerrado, ou
        /// é um segundo disparo do mesmo clique num dia que acabou de começar.
        /// A tela usa o retorno para não deixar o botão ativo sem efeito.
        /// </summary>
        bool RequestEndOfDay();

        /// <summary>
        /// Avança para o próximo dia. Chamado pelo <see cref="DayCycle"/> DEPOIS
        /// de cobrar as contas e fechar o relatório — nunca por gameplay.
        /// </summary>
        void AdvanceDay();

        /// <summary>
        /// O dia chegou ao fim (pelo relógio ou por pedido). Disparado UMA vez
        /// por dia, e é o gatilho do fechamento.
        /// </summary>
        event Action<int> EndOfDayReached;

        /// <summary>Hora do dia mudou o bastante para valer redesenhar o relógio.</summary>
        event Action<IGameClock> TimeChanged;
    }
}
