using System;
using System.Text;
using DG.Tweening;
using JapanMarket.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Controla o prefab do menu de pause e liga automaticamente os botões pelos
/// ícones usados no layout. Assim o prefab continua funcionando mesmo quando
/// uma instância é recriada ou movida para outra cena.
/// </summary>
[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    [Header("Referências opcionais")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup dimOverlay;
    [SerializeField] private GameObject firstSelected;

    [Header("Animação")]
    [SerializeField] private float slideDuration = 0.45f;
    [SerializeField] private Ease slideInEase = Ease.OutExpo;
    [SerializeField] private Ease slideOutEase = Ease.InExpo;
    [SerializeField] private float extraOffscreenPadding = 40f;
    [SerializeField] private float overlayFadeDuration = 0.3f;
    [SerializeField, Range(0f, 1f)] private float overlayMaxAlpha = 0.55f;

    [Header("Eventos")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    public bool IsOpen { get; private set; }

    private RectTransform _contentPanel;
    private TextMeshProUGUI _contentTitle;
    private TextMeshProUGUI _contentBody;
    private readonly Button[] _actionButtons = new Button[3];
    private readonly TextMeshProUGUI[] _actionLabels = new TextMeshProUGUI[3];
    private readonly Action[] _actionCallbacks = new Action[3];

    private Vector2 _onscreenPos;
    private Vector2 _offscreenPos;
    private Sequence _sequence;
    private bool _resumedMotor;
    private float _timeScaleBeforePause = 1f;
    private bool _lookWasEnabled = true;
    private CursorLockMode _cursorLockBeforePause = CursorLockMode.Locked;
    private bool _cursorVisibleBeforePause;

    private static readonly Color PanelColor = new(0.10f, 0.18f, 0.24f, 0.98f);
    private static readonly Color AccentColor = new(0.92f, 0.43f, 0.52f, 1f);
    private static readonly Color ButtonColor = new(0.20f, 0.34f, 0.40f, 1f);

    private void Awake()
    {
        ResolveReferences();
        if (panelRoot == null)
        {
            Debug.LogError($"{nameof(PauseMenuController)}: não encontrei o painel 'PanelLeft'.", this);
            enabled = false;
            return;
        }

        EnsureEventSystem();
        EnsureContentPanel();
        WireMenuButtons();

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
        _onscreenPos = panelRoot.anchoredPosition;
        float width = Mathf.Max(panelRoot.rect.width, 360f);
        _offscreenPos = _onscreenPos + Vector2.left * (width + extraOffscreenPadding);

        panelRoot.anchoredPosition = _offscreenPos;
        panelRoot.gameObject.SetActive(false);
        HideTab();

        if (dimOverlay != null)
        {
            dimOverlay.alpha = 0f;
            dimOverlay.gameObject.SetActive(false);
            dimOverlay.blocksRaycasts = false;

            Image overlayImage = dimOverlay.GetComponent<Image>();
            if (overlayImage != null) overlayImage.raycastTarget = true;
        }
    }

    private void ResolveReferences()
    {
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            RectTransform canvasRect = canvas.transform as RectTransform;
            canvasRect.localScale = Vector3.one;
            canvasRect.localPosition = Vector3.zero;
        }

        if (panelRoot == null)
            panelRoot = FindRectTransform("PanelLeft");

        if (dimOverlay == null)
        {
            RectTransform overlay = FindRectTransform("Panel");
            if (overlay != null)
                dimOverlay = overlay.GetComponent<CanvasGroup>() ?? overlay.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private RectTransform FindRectTransform(string objectName)
    {
        RectTransform[] all = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == objectName) return all[i];
        return null;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystem);
    }

    private void WireMenuButtons()
    {
        Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button.GetComponent<MenuButtonFX>() == null)
                button.gameObject.AddComponent<MenuButtonFX>();

            string icon = FindIconName(button.transform);
            button.onClick.RemoveAllListeners();

            if (icon.Contains("objectives")) button.onClick.AddListener(ShowObjectives);
            else if (icon.Contains("stats")) button.onClick.AddListener(ShowStatistics);
            else if (icon.Contains("trophy")) button.onClick.AddListener(ShowAchievements);
            else if (icon.Contains("settings")) button.onClick.AddListener(ShowSettings);
            else if (icon.Contains("save")) button.onClick.AddListener(SaveGame);
            else if (icon.Contains("quit")) button.onClick.AddListener(ShowQuitConfirmation);

            if (firstSelected == null && !icon.Contains("quit"))
                firstSelected = button.gameObject;
        }
    }

    private static string FindIconName(Transform button)
    {
        Image[] images = button.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Sprite sprite = images[i].sprite;
            if (sprite != null && sprite.name.StartsWith("icon_", StringComparison.OrdinalIgnoreCase))
                return sprite.name.ToLowerInvariant();
        }
        return button.name.ToLowerInvariant();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;

        // Outra interface (computador, construção etc.) já está usando o cursor.
        if (Cursor.lockState != CursorLockMode.Locked && Time.timeScale > 0f) return;

        IsOpen = true;
        _resumedMotor = false;
        _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        _cursorLockBeforePause = Cursor.lockState;
        _cursorVisibleBeforePause = Cursor.visible;

        panelRoot.gameObject.SetActive(true);
        panelRoot.anchoredPosition = _offscreenPos;

        if (dimOverlay != null)
        {
            dimOverlay.gameObject.SetActive(true);
            dimOverlay.blocksRaycasts = true;
        }

        Time.timeScale = 0f;

        PlayerMotor motor = ServiceLocator.Get<PlayerMotor>();
        if (motor != null) motor.SetCanMove(false);

        PlayerLook look = ServiceLocator.Get<PlayerLook>();
        if (look != null)
        {
            _lookWasEnabled = look.CanLook;
            look.CanLook = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.Join(panelRoot.DOAnchorPos(_onscreenPos, slideDuration).SetEase(slideInEase));
        if (dimOverlay != null)
            _sequence.Join(dimOverlay.DOFade(overlayMaxAlpha, overlayFadeDuration).SetEase(Ease.OutSine));

        _sequence.OnComplete(() =>
        {
            if (firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected);
        });

        onOpened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        HideTab();

        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.Join(panelRoot.DOAnchorPos(_offscreenPos, slideDuration).SetEase(slideOutEase));
        if (dimOverlay != null)
            _sequence.Join(dimOverlay.DOFade(0f, overlayFadeDuration).SetEase(Ease.InSine));

        _sequence.OnComplete(() =>
        {
            panelRoot.gameObject.SetActive(false);
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(false);
                dimOverlay.blocksRaycasts = false;
            }
            ResumeGame();
        });

        onClosed?.Invoke();
    }

    private void ResumeGame()
    {
        if (_resumedMotor) return;
        _resumedMotor = true;

        Time.timeScale = _timeScaleBeforePause;

        PlayerMotor motor = ServiceLocator.Get<PlayerMotor>();
        if (motor != null) motor.SetCanMove(true);

        PlayerLook look = ServiceLocator.Get<PlayerLook>();
        if (look != null) look.CanLook = _lookWasEnabled;

        Cursor.lockState = _cursorLockBeforePause;
        Cursor.visible = _cursorVisibleBeforePause;
    }

    public void OnResumeButtonClicked() => Close();

    public void OnQuitButtonClicked()
    {
        ResumeGame();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowObjectives()
    {
        StringBuilder body = new();
        GameContext context = GameContext.Current;
        if (context?.Objectives == null || context.Objectives.Active.Count == 0)
        {
            body.Append("Nenhum objetivo ativo no momento.");
        }
        else
        {
            for (int i = 0; i < context.Objectives.Active.Count; i++)
            {
                var objective = context.Objectives.Active[i];
                string title = objective.Definition.Title.IsEmpty
                    ? objective.Definition.name
                    : objective.Definition.Title.Value;
                body.Append('•').Append(' ').Append(title).Append("  ")
                    .Append(Mathf.RoundToInt(objective.NormalizedProgress * 100f)).Append('%');
                if (i < context.Objectives.Active.Count - 1) body.AppendLine().AppendLine();
            }
        }

        ShowTab("OBJETIVOS", body.ToString());
    }

    private void ShowStatistics()
    {
        GameContext context = GameContext.Current;
        if (context == null)
        {
            ShowTab("ESTATÍSTICAS", "As estatísticas estarão disponíveis quando a partida iniciar.");
            return;
        }

        var report = context.Reports?.Current;
        string body = report == null
            ? $"Dia {context.Progress.CurrentDay}\nNível da loja: {context.StoreLevel.CurrentLevel}\nSaldo: {context.Ledger.Balance}"
            : $"Dia {report.Day}\n\nSaldo: {context.Ledger.Balance}\nNível da loja: {context.StoreLevel.CurrentLevel}\n" +
              $"XP: {context.StoreLevel.CurrentXP}/{context.StoreLevel.GetXPForNextLevel()}\n\n" +
              $"Clientes atendidos hoje: {report.CustomersServed}\nItens vendidos hoje: {report.ItemsSold}\n" +
              $"Receita de hoje: {report.Revenue}";

        ShowTab("ESTATÍSTICAS", body);
    }

    private void ShowAchievements()
    {
        GameContext context = GameContext.Current;
        if (context?.Objectives == null)
        {
            ShowTab("CONQUISTAS", "As conquistas estarão disponíveis quando a partida iniciar.");
            return;
        }

        var snapshots = context.Objectives.Snapshot();
        int completed = 0;
        for (int i = 0; i < snapshots.Count; i++)
            if (snapshots[i].Completed) completed++;

        ShowTab("CONQUISTAS", $"Objetivos concluídos: {completed}\nProgresso registrado: {snapshots.Count}\n\nContinue expandindo o mercado para liberar novas conquistas.");
    }

    private void ShowSettings()
    {
        bool vSync = PlayerPrefs.GetInt("vSync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        bool showFps = PlayerPrefs.GetInt("showFps", 0) == 1;
        int screenMode = Mathf.Clamp(PlayerPrefs.GetInt("screenMode", 0), 0, 3);
        string[] modes = { "Janela sem borda", "Tela cheia", "Maximizada", "Janela" };

        ShowTab(
            "CONFIGURAÇÕES",
            $"V-Sync: {(vSync ? "Ligado" : "Desligado")}\n" +
            $"Modo de tela: {modes[screenMode]}\n" +
            $"Mostrar FPS: {(showFps ? "Sim" : "Não")}",
            (vSync ? "DESLIGAR V-SYNC" : "LIGAR V-SYNC", ToggleVSync),
            ("MODO DE TELA", CycleScreenMode),
            (showFps ? "OCULTAR FPS" : "MOSTRAR FPS", ToggleFps));
    }

    private void ToggleVSync()
    {
        bool active = PlayerPrefs.GetInt("vSync", QualitySettings.vSyncCount > 0 ? 1 : 0) != 1;
        if (SettingsManager.Instance != null) SettingsManager.Instance.VSync(active);
        else QualitySettings.vSyncCount = active ? 1 : 0;
        PlayerPrefs.SetInt("vSync", active ? 1 : 0);
        PlayerPrefs.Save();
        ShowSettings();
    }

    private void CycleScreenMode()
    {
        int mode = (PlayerPrefs.GetInt("screenMode", 0) + 1) % 4;
        if (SettingsManager.Instance != null) SettingsManager.Instance.SetScreeMode(mode);
        else
        {
            Screen.fullScreenMode = mode switch
            {
                1 => FullScreenMode.ExclusiveFullScreen,
                2 => FullScreenMode.MaximizedWindow,
                3 => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };
        }
        PlayerPrefs.SetInt("screenMode", mode);
        PlayerPrefs.Save();
        ShowSettings();
    }

    private void ToggleFps()
    {
        bool active = PlayerPrefs.GetInt("showFps", 0) != 1;
        if (SettingsManager.Instance != null) SettingsManager.Instance.ShowFPS(active);
        PlayerPrefs.SetInt("showFps", active ? 1 : 0);
        PlayerPrefs.Save();
        ShowSettings();
    }

    private void SaveGame()
    {
        bool saved = GameContext.Current != null && GameContext.Current.SaveNow();
        ShowTab("SALVAR JOGO", saved
            ? "Partida salva com sucesso."
            : "Não foi possível salvar agora. Tente novamente durante a partida.");
    }

    private void ShowQuitConfirmation()
    {
        ShowTab("SAIR DO JOGO", "Deseja realmente encerrar o jogo? O progresso não salvo será perdido.",
            ("CANCELAR", HideTab), ("CONFIRMAR SAÍDA", OnQuitButtonClicked));
    }

    private void ShowTab(string title, string body, params (string label, Action callback)[] actions)
    {
        if (_contentPanel == null) return;

        _contentTitle.text = title;
        _contentBody.text = body;
        _contentPanel.gameObject.SetActive(true);

        for (int i = 0; i < _actionButtons.Length; i++)
        {
            bool visible = actions != null && i < actions.Length && actions[i].callback != null;
            _actionButtons[i].gameObject.SetActive(visible);
            _actionCallbacks[i] = visible ? actions[i].callback : null;
            if (visible) _actionLabels[i].text = actions[i].label;
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(actions != null && actions.Length > 0
                ? _actionButtons[0].gameObject
                : _contentPanel.gameObject);
    }

    private void HideTab()
    {
        if (_contentPanel != null) _contentPanel.gameObject.SetActive(false);
    }

    private void EnsureContentPanel()
    {
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        GameObject panel = new("Pause Tab Content", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        _contentPanel = panel.GetComponent<RectTransform>();
        _contentPanel.anchorMin = new Vector2(0.34f, 0.09f);
        _contentPanel.anchorMax = new Vector2(0.95f, 0.91f);
        _contentPanel.offsetMin = Vector2.zero;
        _contentPanel.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = PanelColor;

        _contentTitle = CreateText("Title", _contentPanel, 36f, FontStyles.Bold, TextAlignmentOptions.Left);
        SetRect(_contentTitle.rectTransform, new Vector2(0.06f, 0.83f), new Vector2(0.82f, 0.96f));
        _contentTitle.color = Color.white;

        _contentBody = CreateText("Body", _contentPanel, 24f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        SetRect(_contentBody.rectTransform, new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.80f));
        _contentBody.color = new Color(0.93f, 0.95f, 0.91f, 1f);

        Button close = CreateButton("Close Tab", _contentPanel, "×", AccentColor);
        RectTransform closeRect = close.transform as RectTransform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.93f, 0.91f);
        closeRect.sizeDelta = new Vector2(64f, 64f);
        closeRect.anchoredPosition = Vector2.zero;
        close.onClick.AddListener(HideTab);

        for (int i = 0; i < _actionButtons.Length; i++)
        {
            int index = i;
            Button action = CreateButton($"Action {i + 1}", _contentPanel, string.Empty, ButtonColor);
            RectTransform rect = action.transform as RectTransform;
            float start = 0.06f + i * 0.30f;
            rect.anchorMin = new Vector2(start, 0.06f);
            rect.anchorMax = new Vector2(start + 0.27f, 0.17f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            action.onClick.AddListener(() => _actionCallbacks[index]?.Invoke());
            _actionButtons[i] = action;
            _actionLabels[i] = action.GetComponentInChildren<TextMeshProUGUI>();
        }

        panel.SetActive(false);
    }

    private static Button CreateButton(string objectName, Transform parent, string label, Color color)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;

        TextMeshProUGUI text = CreateText("Label", go.transform, 20f, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one);
        text.text = label;
        text.color = Color.white;
        text.raycastTarget = false;

        go.AddComponent<MenuButtonFX>();
        return go.GetComponent<Button>();
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, float size,
        FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
        if (IsOpen) ResumeGame();
    }

    private void OnDisable()
    {
        if (IsOpen && Time.timeScale == 0f) ResumeGame();
    }
}
