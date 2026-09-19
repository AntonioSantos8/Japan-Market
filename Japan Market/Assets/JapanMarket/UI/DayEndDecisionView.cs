using JapanMarket.Core;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.UI
{
    /// <summary>
    /// Oferece uma única decisão às 21h: encerrar agora ou continuar aberto.
    /// Recusar não fecha a porta e não repete o aviso; o relógio encerra o dia
    /// automaticamente à meia-noite. Durante o tutorial obrigatório a janela
    /// nunca aparece, porque o próprio tutorial decide quando o primeiro dia
    /// pode terminar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayEndDecisionView : MonoBehaviour
    {
        [Header("Janela")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private UnityEngine.UI.Button _finishDayButton;
        [SerializeField] private UnityEngine.UI.Button _continueOpenButton;

        [Header("Conteúdo")]
        [SerializeField] private TMPro.TextMeshProUGUI _timeText;
        [SerializeField] private TMPro.TextMeshProUGUI _titleText;
        [SerializeField] private TMPro.TextMeshProUGUI _messageText;

        private IGameClock _clock;
        private int _handledDay;
        private bool _visible;
        private bool _decisionInProgress;
        private float _previousTimeScale = 1f;
        private bool _previousClockRunning = true;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);

            _finishDayButton?.onClick.AddListener(FinishDay);
            _continueOpenButton?.onClick.AddListener(ContinueOpen);

            Set(_timeText, "21:00");
            Set(_titleText, "ENCERRAR O DIA?");
            Set(_messageText,
                "Você já pode encerrar o expediente e ver o resumo do dia. " +
                "Se continuar aberto, a loja funcionará normalmente até 00:00.");
        }

        private void Update()
        {
            if (_clock == null && !ServiceContainer.Current.TryResolve(out _clock))
                return;

            if (_handledDay != 0 && _handledDay != _clock.Day)
                _handledDay = 0;

            if (_visible || _decisionInProgress || _handledDay == _clock.Day)
                return;
            if (!_clock.IsRunning || _clock.IsEndOfDayLocked)
                return;
            if (_clock.TimeOfDay < _clock.EndDayPromptHour
                || _clock.TimeOfDay >= _clock.EndOfDayHour)
                return;

            Show();
        }

        private void Show()
        {
            _visible = true;
            _handledDay = _clock.Day;

            _previousTimeScale = Time.timeScale;
            _previousClockRunning = _clock.IsRunning;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            _clock.SetRunning(false);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_panel != null)
            {
                _panel.transform.SetAsLastSibling();
                _panel.SetActive(true);
            }

            _finishDayButton?.Select();
        }

        /// <summary>Botão principal: fecha porta, relatório e dia agora.</summary>
        public void FinishDay()
        {
            if (!_visible || _decisionInProgress || _clock == null) return;

            _decisionInProgress = true;
            HideAndRestore();

            // O relatório é emitido de forma síncrona. Restauramos primeiro
            // para a tela de resumo poder capturar o estado correto e pausar de
            // novo, sem duas janelas brigando por Time.timeScale e cursor.
            if (!_clock.RequestEndOfDay())
            {
                _handledDay = 0;
                _decisionInProgress = false;
                return;
            }

            _decisionInProgress = false;
        }

        /// <summary>Botão secundário: mantém loja, clientes e relógio até 00h.</summary>
        public void ContinueOpen()
        {
            if (!_visible || _decisionInProgress) return;
            HideAndRestore();
        }

        private void HideAndRestore()
        {
            _visible = false;
            if (_panel != null) _panel.SetActive(false);

            Time.timeScale = _previousTimeScale;
            _clock?.SetRunning(_previousClockRunning);
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }

        private static void Set(TMPro.TextMeshProUGUI target, string value)
        {
            if (target != null) target.text = value;
        }

        private void OnDestroy()
        {
            _finishDayButton?.onClick.RemoveListener(FinishDay);
            _continueOpenButton?.onClick.RemoveListener(ContinueOpen);

            if (_visible) HideAndRestore();
        }
    }
}
