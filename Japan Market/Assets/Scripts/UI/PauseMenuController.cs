// PauseMenuController.cs
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla o menu de pause: abre/fecha com Esc, pausa o jogo (Time.timeScale)
/// e anima o painel entrando suavemente pela esquerda, no mesmo padrão de
/// DOTween + SetUpdate(true) já usado em UIButtonAnimator/ComputerButtonsManager.
///
/// Setup no Inspector:
///  - panelRoot: o RectTransform do painel esquerdo (o retângulo com as flores,
///    tiles etc). O menu assume que a âncora dele já está no canto esquerdo
///    (pivot/anchor em x=0) e calcula sozinho o ponto "fora da tela" a partir
///    da largura do painel, então não precisa configurar posição manualmente.
///  - dimOverlay: um Image full-screen atrás do painel (cor escura, alpha baixo)
///    usado para escurecer o jogo por trás. Opcional — pode deixar vazio.
///  - firstSelected: o botão que deve ficar selecionado ao abrir (ex.: Resume),
///    útil se o jogo também for jogado com teclado/gamepad. Opcional.
/// </summary>
[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    [Header("Referências")]
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

    private Vector2 _onscreenPos;
    private Vector2 _offscreenPos;
    private Sequence _sequence;
    private bool _resumedMotor;

    private void Awake()
    {
        if (panelRoot == null)
        {
            Debug.LogError($"{nameof(PauseMenuController)}: panelRoot não configurado.", this);
            enabled = false;
            return;
        }

        _onscreenPos = panelRoot.anchoredPosition;
        _offscreenPos = _onscreenPos + Vector2.left * (panelRoot.rect.width + extraOffscreenPadding);

        // Começa fechado e fora da tela, sem depender de estado deixado no editor.
        panelRoot.anchoredPosition = _offscreenPos;
        panelRoot.gameObject.SetActive(false);

        if (dimOverlay != null)
        {
            dimOverlay.alpha = 0f;
            dimOverlay.gameObject.SetActive(false);
            dimOverlay.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Toggle();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;

        // Não abre por cima de outra tela que já tirou o cursor do lock
        // (computador, roda de ferramentas etc.) — evita duas UIs simultâneas.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        IsOpen = true;
        _resumedMotor = false;

        panelRoot.gameObject.SetActive(true);
        panelRoot.anchoredPosition = _offscreenPos;

        if (dimOverlay != null)
        {
            dimOverlay.gameObject.SetActive(true);
            dimOverlay.blocksRaycasts = true;
        }

        Time.timeScale = 0f;

        var motor = ServiceLocator.Get<PlayerMotor>();
        if (motor != null) motor.SetCanMove(false);

        var look = ServiceLocator.Get<PlayerLook>();
        if (look != null) look.CanLook = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.Join(panelRoot.DOAnchorPos(_onscreenPos, slideDuration).SetEase(slideInEase));

        if (dimOverlay != null)
            _sequence.Join(dimOverlay.DOFade(overlayMaxAlpha, overlayFadeDuration).SetEase(Ease.OutSine));

        _sequence.OnComplete(() =>
        {
            if (firstSelected != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstSelected);
        });

        onOpened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

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

        Time.timeScale = 1f;

        var motor = ServiceLocator.Get<PlayerMotor>();
        if (motor != null) motor.SetCanMove(true);

        var look = ServiceLocator.Get<PlayerLook>();
        if (look != null) look.CanLook = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>Atalho para ligar direto no OnClick do botão "Resume" no Inspector.</summary>
    public void OnResumeButtonClicked() => Close();

    /// <summary>Atalho para o botão "Quit" (sai do jogo; no editor só loga).</summary>
    public void OnQuitButtonClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
        // Evita travar o jogo em timeScale 0 se o objeto for destruído com o menu aberto.
        if (IsOpen) ResumeGame();
    }

    private void OnDisable()
    {
        if (IsOpen && Time.timeScale == 0f) ResumeGame();
    }
}
