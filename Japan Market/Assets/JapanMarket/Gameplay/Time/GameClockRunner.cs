using System;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Empurra o tempo real para dentro do relógio da loja.
    ///
    /// É tudo o que este componente faz, e é o ponto: o relógio inteiro —
    /// abertura, fechamento, virada do dia, cobrança das contas — é C# puro e
    /// testável. A única coisa que exigia um MonoBehaviour era alguém chamar
    /// <c>Tick</c> com o <c>Time.deltaTime</c>, e é só isso que mora aqui.
    ///
    /// Coloque um na cena, ao lado do GameContext.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class GameClockRunner : MonoBehaviour
    {
        [Tooltip("Pausa o relógio sem pausar o jogo. Útil para depurar.")]
        [SerializeField] private bool _running = true;

        [Header("Depuração")]
        [Tooltip("Escreve no console cada virada de dia e o resumo do fechamento.")]
        [SerializeField] private bool _logDayCycle = true;

        private IGameClock _clock;
        private DailyReportService _reports;
        private Action<DailyReport> _logHandler;
        private bool _lastRunningApplied = true;

        public IGameClock Clock => _clock;

        private void Update()
        {
            if (_clock == null) { TryResolve(); return; }

            // Só empurra quando o CAMPO muda. Comparar com _clock.IsRunning
            // faria este componente desfazer, no frame seguinte, qualquer pausa
            // que um menu ou uma cutscene tivesse pedido — e o SetRunning da
            // interface seria inútil para todo mundo que não fosse ele.
            if (_running != _lastRunningApplied)
            {
                _lastRunningApplied = _running;
                _clock.SetRunning(_running);
            }

            _clock.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Resolvido por tentativa a cada frame, e não uma vez só no Start: o
        /// GameContext pode entrar depois numa cena carregada aditivamente, e um
        /// relógio que desistiu na primeira tentativa nunca mais anda.
        /// </summary>
        private void TryResolve()
        {
            GameContext game = GameContext.Current;
            if (game == null) return;

            game.Services.TryResolve(out _clock);

            game.Services.TryResolve(out IDailyReportService reports);
            _reports = reports as DailyReportService;

            if (_clock == null || _reports == null || _logHandler != null || !_logDayCycle) return;

            // Guardado num campo, e não numa lambda anônima: sem a referência
            // não há como desassinar, e cada ciclo desativar/reativar deixaria
            // mais um handler pendurado num serviço que vive no GameContext —
            // capturando um MonoBehaviour já destruído.
            _logHandler = report => Debug.Log($"[Dia] {report}", this);
            _reports.ReportClosed += _logHandler;
        }

        private void OnDisable()
        {
            // Nada a desassinar do relógio: quem assina o fechamento é o
            // DayCycle, que vive no contexto. Aqui só paramos de empurrar tempo
            // e devolvemos o handler de log.
            if (_reports != null && _logHandler != null) _reports.ReportClosed -= _logHandler;

            _logHandler = null;
            _reports = null;
            _clock = null;
        }

        // ── atalhos de depuração ─────────────────────────────────────────────

        [ContextMenu("Depuração/Abrir a loja")]
        public void OpenStore()
        {
            TryResolve();
            if (_clock != null && !_clock.TryOpenStore())
                Debug.LogWarning("[Relógio] Tarde demais para abrir hoje.", this);
        }

        [ContextMenu("Depuração/Fechar a loja")]
        public void CloseStore()
        {
            TryResolve();
            _clock?.CloseStore();
        }

        [ContextMenu("Depuração/Encerrar o dia agora")]
        public void EndDay()
        {
            TryResolve();
            _clock?.RequestEndOfDay();
        }

        private void OnGUI()
        {
            if (!_logDayCycle || _clock == null) return;

            // Relógio de canto, só enquanto a interface de verdade não existe.
            GUI.Label(new Rect(10f, 10f, 320f, 22f), _clock.ToString());
        }
    }
}
