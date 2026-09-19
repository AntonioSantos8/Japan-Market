using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Implementação padrão do relógio. Sem Unity, sem corrotina, sem
    /// <c>Time.deltaTime</c> lido aqui dentro.
    /// </summary>
    public sealed class GameClock : IGameClock
    {
        private readonly IEventBus _events;

        /// <summary>Quanto o relógio precisa andar para valer um aviso de UI.</summary>
        private const float NotifyStepHours = 1f / 60f;   // um minuto de jogo

        private float _lastNotifiedTime;

        /// <summary>
        /// Último dia JÁ encerrado. A trava é o número do dia, e não um bool,
        /// porque um bool precisa ser rearmado — e quem rearmava era o
        /// AdvanceDay, chamado de dentro do próprio fechamento. Resultado: dois
        /// pedidos seguidos de "encerrar o dia" fechavam DOIS dias e cobravam o
        /// aluguel duas vezes, sem nada no console.
        /// </summary>
        private int _lastClosedDay;

        /// <summary>
        /// O dia virou e o relógio ainda não andou nele.
        ///
        /// Só o <see cref="AdvanceDay"/> arma isto — um dia recém-criado POR UM
        /// FECHAMENTO. Uma partida nova não conta: o jogador pode abrir o jogo e
        /// ir dormir na hora, e isso é uma jogada, não um engano.
        /// </summary>
        private bool _freshDay;

        /// <summary>
        /// Estamos dentro do <see cref="AdvanceDay"/>, publicando DayStarted.
        ///
        /// A tela de resumo com "pular o dia" assina DayStarted e pede o fim do
        /// dia dali de dentro. É a mesma chamada que um segundo clique acidental
        /// faria, e as duas precisam de respostas opostas — esta flag é a única
        /// diferença observável entre as duas.
        /// </summary>
        private bool _advancing;

        /// <summary>
        /// Horas de mundo acumuladas. É um CONTADOR, não uma conta a partir de
        /// Day e TimeOfDay.
        ///
        /// A fórmula `(Day-1)*24 + TimeOfDay` parecia equivalente e não era: o
        /// dia só é simulado das 6h às 24h, então cada virada presenteava seis
        /// horas que nunca passaram — e o jogador que apertasse "ir dormir" às
        /// 9h ganhava vinte e uma. Na prática, dormir cancelava qualquer prazo
        /// de entrega.
        /// </summary>
        private float _totalHours;
        private bool _endOfDayLocked;

        public GameClock(IEventBus events, GameClockSettings settings = default)
        {
            _events = events;
            Settings = settings.OrDefault();

            Day = 1;
            TimeOfDay = Settings.DayStartHour;
            _lastNotifiedTime = TimeOfDay;
        }

        public GameClockSettings Settings { get; }

        public int Day { get; private set; }
        public float TimeOfDay { get; private set; }
        public bool StoreIsOpen { get; private set; }
        public bool IsRunning { get; private set; } = true;
        public bool IsEndOfDayLocked => _endOfDayLocked;

        public float TotalHours => _totalHours;

        public float EndDayPromptHour => Settings.EndDayPromptHour;
        public float ClosingHour => Settings.ClosingHour;
        public float EndOfDayHour => Settings.EndOfDayHour;

        public event Action<int> EndOfDayReached;
        public event Action<IGameClock> TimeChanged;

        // ── andamento ────────────────────────────────────────────────────────

        public void Tick(float deltaSeconds)
        {
            if (!IsRunning || deltaSeconds <= 0f) return;

            float elapsed = deltaSeconds * Settings.GameHoursPerRealSecond;

            TimeOfDay += elapsed;
            _totalHours += elapsed;

            // O tutorial precisa do tempo absoluto para as entregas, mas não
            // pode perder o expediente enquanto o jogador lê e monta a loja.
            // Seguramos o relógio visível um minuto antes de fechar; TotalHours
            // continua andando e os pedidos pagos continuam chegando.
            if (_endOfDayLocked)
            {
                float holdAt = Math.Max(Settings.DayStartHour,
                                        Settings.ClosingHour - NotifyStepHours);
                TimeOfDay = Math.Min(TimeOfDay, holdAt);
            }

            // O dia deixou de ser recém-nascido: a partir daqui um pedido de
            // fechamento é uma decisão do jogador, não o eco do clique anterior.
            _freshDay = false;

            if (!_endOfDayLocked && _lastClosedDay != Day
                                     && TimeOfDay >= Settings.EndOfDayHour)
                FireEndOfDay();

            if (Math.Abs(TimeOfDay - _lastNotifiedTime) < NotifyStepHours) return;

            _lastNotifiedTime = TimeOfDay;
            TimeChanged?.Invoke(this);
        }

        public void SetRunning(bool running) => IsRunning = running;

        public void SetEndOfDayLocked(bool locked)
        {
            _endOfDayLocked = locked;

            if (!locked || TimeOfDay < Settings.ClosingHour) return;

            TimeOfDay = Math.Max(Settings.DayStartHour,
                                 Settings.ClosingHour - NotifyStepHours);
            _lastNotifiedTime = TimeOfDay;
            TimeChanged?.Invoke(this);
        }

        // ── porta ────────────────────────────────────────────────────────────

        public bool TryOpenStore()
        {
            if (StoreIsOpen) return true;
            if (TimeOfDay >= Settings.ClosingHour) return false;

            StoreIsOpen = true;
            _events?.Publish(new StoreOpenStateChanged(true));
            return true;
        }

        public void CloseStore()
        {
            if (!StoreIsOpen) return;

            StoreIsOpen = false;
            _events?.Publish(new StoreOpenStateChanged(false));
        }

        // ── virada do dia ────────────────────────────────────────────────────

        /// <summary>
        /// Pede o encerramento do dia. Devolve false quando o pedido foi
        /// recusado — a tela usa isso para não deixar o botão ativo à toa.
        ///
        /// Dois pedidos seguidos, de fora, fecham UM dia. O primeiro encerra o
        /// dia 1 e o relógio vira para o dia 2 às 6h; o segundo chega num dia em
        /// que nada aconteceu ainda, e é quase sempre o mesmo clique chegando
        /// duas vezes. Atendê-lo cobrava dois aluguéis e fechava dois relatórios
        /// no mesmo frame, em silêncio.
        ///
        /// O pedido que vem DE DENTRO do DayStarted é outra coisa: é a tela de
        /// resumo pulando o dia de propósito, e esse é atendido. A diferença
        /// entre os dois é só <see cref="_advancing"/> — do lado de fora as duas
        /// chamadas são idênticas.
        /// </summary>
        public bool RequestEndOfDay()
        {
            if (_endOfDayLocked) return false;
            if (_lastClosedDay == Day) return false;
            if (_freshDay && !_advancing) return false;

            FireEndOfDay();
            return true;
        }

        private void FireEndOfDay()
        {
            // Marcado ANTES de avisar: quem escuta pode pedir o fim do dia de
            // novo por reentrância, e o MESMO dia não pode fechar duas vezes —
            // seriam duas cobranças de aluguel.
            _lastClosedDay = Day;

            CloseStore();
            EndOfDayReached?.Invoke(Day);
        }

        /// <summary>
        /// Vira o dia. Só o <see cref="DayCycle"/> chama, e só depois de cobrar
        /// as contas e fechar o relatório.
        /// </summary>
        public void AdvanceDay()
        {
            // A noite conta como tempo de mundo: do momento em que o dia
            // encerrou até a abertura do dia seguinte. Quem foi dormir às 9h
            // pulou vinte e uma horas, e uma entrega com prazo de doze chega de
            // manhã — em vez de ser cancelada pelo sono, como acontecia quando
            // TotalHours era derivado de Day.
            //
            // O clamp é sobre a soma inteira, e não sobre `24 - TimeOfDay`
            // isolado: o tick pode passar de 24h (um frame longo leva o relógio
            // a 24,8h), e clampando a primeira parcela em zero as seis horas da
            // madrugada eram creditadas por inteiro em cima de um dia que já
            // tinha ido longe demais.
            _totalHours += Math.Max(0f, 24f + Settings.DayStartHour - TimeOfDay);

            Day++;
            TimeOfDay = Settings.DayStartHour;
            _lastNotifiedTime = TimeOfDay;
            _freshDay = true;

            // A janela em que um pedido de fim de dia é "pular o dia" e não um
            // clique repetido. Fecha no finally porque um assinante de
            // DayStarted pode lançar, e uma janela que ficasse aberta faria todo
            // clique repetido a partir dali ser aceito.
            _advancing = true;
            try
            {
                _events?.Publish(new DayStarted(Day));
                TimeChanged?.Invoke(this);
            }
            finally { _advancing = false; }
        }

        /// <summary>Restaura um save. Não dispara virada de dia.</summary>
        public void Restore(int day, float timeOfDay, bool storeOpen)
        {
            Day = day < 1 ? 1 : day;
            TimeOfDay = Math.Max(0f, timeOfDay);
            _lastNotifiedTime = TimeOfDay;

            // Reconstrução aproximada: o save guarda dia e hora, não o contador.
            // Basta ser monotônico e coerente com prazos NOVOS; prazos salvos
            // são restaurados junto com o pedido, não recalculados daqui.
            //
            // O `- DayStartHour` é o que faz a origem bater com a do contador:
            // uma partida nova está no dia 1 às 6h com ZERO hora acumulada, e
            // sem subtrair, salvar e carregar na mesma hora adiantava o relógio
            // em seis horas — o suficiente para uma entrega de duas horas
            // chegar sozinha no carregamento.
            _totalHours = Math.Max(0f, (Day - 1) * 24f + TimeOfDay - Settings.DayStartHour);

            // Se o save foi feito depois do fim do expediente, o dia já estava
            // encerrado; senão ainda está por encerrar.
            _lastClosedDay = TimeOfDay >= Settings.EndOfDayHour ? Day : Day - 1;

            // Carregar um save não é acabar de virar o dia: quem abre a partida
            // e decide ir dormir na hora está tomando uma decisão.
            _freshDay = false;
            _advancing = false;

            StoreIsOpen = storeOpen && TimeOfDay < Settings.ClosingHour;
        }

        public override string ToString()
        {
            int hour = (int)TimeOfDay;
            int minute = (int)((TimeOfDay - hour) * 60f);
            return $"Dia {Day}, {hour:00}:{minute:00}{(StoreIsOpen ? ", aberta" : "")}";
        }
    }

    /// <summary>
    /// Configuração do relógio. Struct para poder ser serializada num
    /// MonoBehaviour sem virar mais um ScriptableObject.
    /// </summary>
    [Serializable]
    public struct GameClockSettings
    {
        public float DayStartHour;
        public float EndDayPromptHour;
        public float ClosingHour;
        public float EndOfDayHour;

        /// <summary>
        /// Quantas horas de jogo passam por segundo real. 0,2 dá um dia de
        /// 06h→24h em noventa segundos.
        /// </summary>
        public float GameHoursPerRealSecond;

        public static GameClockSettings Default => new()
        {
            DayStartHour = 6f,
            EndDayPromptHour = 21f,
            ClosingHour = 24f,
            EndOfDayHour = 24f,
            GameHoursPerRealSecond = 0.2f,
        };

        /// <summary>
        /// Um struct default (tudo zero) faria o dia terminar no primeiro frame.
        /// Este guarda troca zeros por valores utilizáveis.
        /// </summary>
        public GameClockSettings OrDefault()
        {
            GameClockSettings fallback = Default;

            if (GameHoursPerRealSecond <= 0f) GameHoursPerRealSecond = fallback.GameHoursPerRealSecond;
            if (EndOfDayHour <= 0f) EndOfDayHour = fallback.EndOfDayHour;
            if (ClosingHour <= 0f || ClosingHour > EndOfDayHour)
                ClosingHour = Math.Min(fallback.ClosingHour, EndOfDayHour);
            // `<= 0`, e não `< 0`: um struct vindo de uma cena salva antes
            // desta fase chega com tudo zero, e zero aqui faria o dia começar à
            // meia-noite em vez das seis.
            if (DayStartHour <= 0f || DayStartHour >= ClosingHour)
                DayStartHour = Math.Min(fallback.DayStartHour, ClosingHour);
            if (EndDayPromptHour <= DayStartHour || EndDayPromptHour >= EndOfDayHour)
                EndDayPromptHour = Math.Min(fallback.EndDayPromptHour,
                                            EndOfDayHour - (1f / 60f));

            return this;
        }
    }
}
