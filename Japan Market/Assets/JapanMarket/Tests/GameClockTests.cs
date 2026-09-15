using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    /// <summary>
    /// O relógio é alimentado de fora justamente para isto: "o que acontece às
    /// 22h" é uma linha de teste, não noventa segundos de Play Mode.
    /// </summary>
    public sealed class GameClockTests
    {
        private static GameClockSettings Fast => new()
        {
            DayStartHour = 6f,
            ClosingHour = 22f,
            EndOfDayHour = 24f,
            GameHoursPerRealSecond = 1f,   // 1 s real = 1 h de jogo
        };

        [Test]
        public void Comeca_no_dia_1_na_hora_de_abertura_e_fechado()
        {
            var clock = new GameClock(new EventBus(), Fast);

            Assert.AreEqual(1, clock.Day);
            Assert.AreEqual(6f, clock.TimeOfDay, 0.001f);
            Assert.IsFalse(clock.StoreIsOpen);
        }

        [Test]
        public void Configuracao_zerada_nao_termina_o_dia_no_primeiro_frame()
        {
            // Um GameClockSettings default() vindo do inspetor tem tudo zero.
            // Sem o OrDefault, EndOfDayHour = 0 faria o dia fechar já no Tick 1
            // — e o aluguel seria cobrado a cada frame.
            var clock = new GameClock(new EventBus(), default);

            int endings = 0;
            clock.EndOfDayReached += _ => endings++;
            clock.Tick(0.016f);

            Assert.AreEqual(0, endings);
            Assert.Greater(clock.EndOfDayHour, 0f);

            // E o dia não pode começar à meia-noite: uma cena salva antes desta
            // fase desserializa o struct inteiro zerado, e zero em DayStartHour
            // passava pelo guard antigo (`< 0`) sem ser corrigido.
            Assert.AreEqual(6f, clock.TimeOfDay, 0.001f);
        }

        [Test]
        public void Abrir_publica_o_evento_de_porta()
        {
            var events = new EventBus();
            var clock = new GameClock(events, Fast);

            bool? state = null;
            using (events.Subscribe<StoreOpenStateChanged>(e => state = e.IsOpen))
            {
                Assert.IsTrue(clock.TryOpenStore());
            }

            Assert.IsTrue(state);
            Assert.IsTrue(clock.StoreIsOpen);
        }

        [Test]
        public void Abrir_duas_vezes_nao_publica_de_novo()
        {
            var events = new EventBus();
            var clock = new GameClock(events, Fast);
            clock.TryOpenStore();

            int published = 0;
            using (events.Subscribe<StoreOpenStateChanged>(_ => published++))
            {
                Assert.IsTrue(clock.TryOpenStore());
            }

            Assert.AreEqual(0, published);
        }

        [Test]
        public void Nao_da_para_abrir_depois_do_horario()
        {
            var clock = new GameClock(new EventBus(), Fast);
            clock.Tick(16f);   // 6h + 16h = 22h

            Assert.IsFalse(clock.TryOpenStore(),
                "Abrir às 22h daria ao jogador um minuto de clientes.");
        }

        [Test]
        public void A_porta_fecha_sozinha_no_horario()
        {
            var clock = new GameClock(new EventBus(), Fast);
            clock.TryOpenStore();

            clock.Tick(16.5f);

            Assert.IsFalse(clock.StoreIsOpen,
                "Quem esquece a placa ligada não pode receber cliente de madrugada.");
        }

        [Test]
        public void O_dia_termina_no_horario_e_uma_vez_so()
        {
            var clock = new GameClock(new EventBus(), Fast);

            int endings = 0;
            clock.EndOfDayReached += _ => endings++;

            clock.Tick(18f);   // 24h
            clock.Tick(5f);    // continua andando

            Assert.AreEqual(1, endings, "Duas viradas seriam dois aluguéis.");
        }

        [Test]
        public void Pedir_o_fim_do_dia_duas_vezes_so_conta_uma()
        {
            var clock = new GameClock(new EventBus(), Fast);

            int endings = 0;
            clock.EndOfDayReached += _ => endings++;

            clock.RequestEndOfDay();
            clock.RequestEndOfDay();

            Assert.AreEqual(1, endings);
        }

        [Test]
        public void Avancar_o_dia_reinicia_a_hora_e_rearma_o_fechamento()
        {
            var events = new EventBus();
            var clock = new GameClock(events, Fast);

            int endings = 0;
            clock.EndOfDayReached += _ => endings++;

            clock.RequestEndOfDay();
            clock.AdvanceDay();

            Assert.AreEqual(2, clock.Day);
            Assert.AreEqual(6f, clock.TimeOfDay, 0.001f);

            clock.Tick(18f);
            Assert.AreEqual(2, endings, "O dia 2 também precisa poder terminar.");
        }

        [Test]
        public void Avancar_o_dia_publica_DayStarted()
        {
            var events = new EventBus();
            var clock = new GameClock(events, Fast);

            int day = 0;
            using (events.Subscribe<DayStarted>(e => day = e.Day))
            {
                clock.AdvanceDay();
            }

            Assert.AreEqual(2, day);
        }

        [Test]
        public void Parado_nao_anda()
        {
            var clock = new GameClock(new EventBus(), Fast);
            clock.SetRunning(false);

            clock.Tick(10f);

            Assert.AreEqual(6f, clock.TimeOfDay, 0.001f);
        }

        [Test]
        public void Restaurar_um_save_no_meio_da_noite_nao_dispara_virada()
        {
            var clock = new GameClock(new EventBus(), Fast);

            int endings = 0;
            clock.EndOfDayReached += _ => endings++;

            clock.Restore(day: 7, timeOfDay: 23f, storeOpen: true);

            Assert.AreEqual(7, clock.Day);
            Assert.IsFalse(clock.StoreIsOpen, "23h já passou do horário de fechar.");
            Assert.AreEqual(0, endings);
        }
    }
}
