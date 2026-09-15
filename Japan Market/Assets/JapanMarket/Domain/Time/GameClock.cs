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

        public float ClosingHour => Settings.ClosingHour;
        public float EndOfDayHour => Settings.EndOfDayHour;

        public event Action<int> EndOfDayReached;
        public event Action<IGameClock> TimeChanged;

        // ── andamento ────────────────────────────────────────────────────────

        public void Tick(float deltaSeconds)
        {
            if (!IsRunning || deltaSeconds <= 0f) return;

            TimeOfDay += deltaSeconds * Settings.GameHoursPerRealSecond;

            // A porta fecha sozinha no horário. Sem isto, o jogador que esquece
            // a placa ligada recebe clientes de madrugada.
            if (StoreIsOpen && TimeOfDay >= Settings.ClosingHour) CloseStore();

            if (_lastClosedDay != Day && TimeOfDay >= Settings.EndOfDayHour) FireEndOfDay();

            if (Math.Abs(TimeOfDay - _lastNotifiedTime) < NotifyStepHours) return;

            _lastNotifiedTime = TimeOfDay;
            TimeChanged?.Invoke(this);
        }

        public void SetRunning(bool running) => IsRunning = running;

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

        public void RequestEndOfDay()
        {
            if (_lastClosedDay == Day) return;
            FireEndOfDay();
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
            Day++;
            TimeOfDay = Settings.DayStartHour;
            _lastNotifiedTime = TimeOfDay;

            _events?.Publish(new DayStarted(Day));
            TimeChanged?.Invoke(this);
        }

        /// <summary>Restaura um save. Não dispara virada de dia.</summary>
        public void Restore(int day, float timeOfDay, bool storeOpen)
        {
            Day = day < 1 ? 1 : day;
            TimeOfDay = Math.Max(0f, timeOfDay);
            _lastNotifiedTime = TimeOfDay;

            // Se o save foi feito depois do fim do expediente, o dia já estava
            // encerrado; senão ainda está por encerrar.
            _lastClosedDay = TimeOfDay >= Settings.EndOfDayHour ? Day : Day - 1;

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
            ClosingHour = 22f,
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
            if (ClosingHour <= 0f || ClosingHour > EndOfDayHour) ClosingHour = Math.Min(fallback.ClosingHour, EndOfDayHour);
            // `<= 0`, e não `< 0`: um struct vindo de uma cena salva antes
            // desta fase chega com tudo zero, e zero aqui faria o dia começar à
            // meia-noite em vez das seis.
            if (DayStartHour <= 0f || DayStartHour >= ClosingHour)
                DayStartHour = Math.Min(fallback.DayStartHour, ClosingHour);

            return this;
        }
    }
}
