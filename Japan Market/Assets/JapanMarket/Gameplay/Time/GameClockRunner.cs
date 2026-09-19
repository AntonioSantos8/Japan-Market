using System;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class GameClockRunner : MonoBehaviour
    {
        [Tooltip("Pauses the clock without pausing the game. Useful for debugging.")]
        [SerializeField] private bool _running = true;

        [Header("Debug")]
        [Tooltip("Logs each day change and the closing summary to the console.")]
        [SerializeField] private bool _logDayCycle = true;

        private IGameClock _clock;
        private DailyReportService _reports;
        private Action<DailyReport> _logHandler;
        private bool _lastRunningApplied = true;

        public IGameClock Clock => _clock;

        private void Update()
        {
            if (_clock == null) { TryResolve(); return; }

            if (_running != _lastRunningApplied)
            {
                _lastRunningApplied = _running;
                _clock.SetRunning(_running);
            }

            _clock.Tick(Time.deltaTime);
        }

        private void TryResolve()
        {
            GameContext game = GameContext.Current;
            if (game == null) return;

            game.Services.TryResolve(out _clock);

            game.Services.TryResolve(out IDailyReportService reports);
            _reports = reports as DailyReportService;

            if (_clock == null || _reports == null || _logHandler != null || !_logDayCycle) return;

            _logHandler = report => Debug.Log($"[Day] {report}", this);
            _reports.ReportClosed += _logHandler;
        }

        private void OnDisable()
        {

            if (_reports != null && _logHandler != null) _reports.ReportClosed -= _logHandler;

            _logHandler = null;
            _reports = null;
            _clock = null;
        }

        [ContextMenu("Debug/Open the store")]
        public void OpenStore()
        {
            TryResolve();
            if (_clock != null && !_clock.TryOpenStore())
                Debug.LogWarning("[Clock] Too late to open today.", this);
        }

        [ContextMenu("Debug/Close the store")]
        public void CloseStore()
        {
            TryResolve();
            _clock?.CloseStore();
        }

        [ContextMenu("Debug/End the day now")]
        public void EndDay()
        {
            TryResolve();
            _clock?.RequestEndOfDay();
        }

        private void OnGUI()
        {
            if (!_logDayCycle || _clock == null) return;

            GUI.Label(new Rect(10f, 10f, 320f, 22f), _clock.ToString());
        }
    }
}
