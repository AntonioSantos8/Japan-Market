using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsManager : MonoBehaviour
{
    [Header("Abas")]
    [SerializeField] private GameObject[] gameObj;
    [SerializeField] private int currentObj;

    [Header("Geral")]
    [SerializeField] private Toggle showFps;
    [SerializeField] private Toggle vSync;
    [SerializeField] private TMP_Dropdown screenMode;

    [Header("Acessibilidade")]
    [SerializeField] private TMP_Dropdown colorBlind;

    [Header("Audio")]
    [SerializeField] private Slider master;
    [SerializeField] private Slider music;
    [SerializeField] private Slider sfx;
    [SerializeField] private TMP_Text masterValue;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private TMP_Text sfxValue;

    [Header("Feedback")]
    [SerializeField] private TMP_Text statusText;

    private SettingsSnapshot savedSnapshot;
    private bool initialized;
    private const float DefaultVolume = 0.8f;

    private struct SettingsSnapshot
    {
        public bool ShowFps;
        public bool VSync;
        public int ScreenMode;
        public int ColorBlind;
        public float Master;
        public float Music;
        public float Sfx;
    }

    private void Start()
    {
        LoadSavedSettings();
        RegisterListeners();
        SetCurrentTab(currentObj);
        ApplyPreview();
        savedSnapshot = ReadControls();
        initialized = true;
        SetStatus(string.Empty);
    }

    private void OnEnable()
    {
        if (!initialized) return;
        LoadSavedSettings();
        savedSnapshot = ReadControls();
        ApplyPreview();
        SetCurrentTab(0);
        SetStatus(string.Empty);
    }

    private void RegisterListeners()
    {
        if (showFps != null) showFps.onValueChanged.AddListener(_ => SettingsManager.Instance?.PreviewShowFPS(showFps.isOn));
        if (vSync != null) vSync.onValueChanged.AddListener(_ => SettingsManager.Instance?.PreviewVSync(vSync.isOn));
        if (screenMode != null) screenMode.onValueChanged.AddListener(_ => SettingsManager.Instance?.PreviewScreenMode(screenMode.value));
        if (colorBlind != null) colorBlind.onValueChanged.AddListener(_ => SettingsManager.Instance?.PreviewColorBlindMode(colorBlind.value));
        if (master != null) master.onValueChanged.AddListener(value => PreviewVolume(value, SliderType.Master));
        if (music != null) music.onValueChanged.AddListener(value => PreviewVolume(value, SliderType.Music));
        if (sfx != null) sfx.onValueChanged.AddListener(value => PreviewVolume(value, SliderType.Sfx));
    }

    private void PreviewVolume(float value, SliderType type)
    {
        SettingsManager.Instance?.PreviewVolume(value, type);
        UpdateVolumeLabels();
    }

    public void Save()
    {
        SettingsSnapshot values = ReadControls();
        PlayerPrefs.SetInt("showFps", values.ShowFps ? 1 : 0);
        PlayerPrefs.SetInt("vSync", values.VSync ? 1 : 0);
        PlayerPrefs.SetInt("screenMode", values.ScreenMode);
        PlayerPrefs.SetInt("colorBlindMode", values.ColorBlind);
        PlayerPrefs.SetFloat("masterVolume", values.Master);
        PlayerPrefs.SetFloat("musicVolume", values.Music);
        PlayerPrefs.SetFloat("sfxVolume", values.Sfx);
        PlayerPrefs.Save();
        savedSnapshot = values;
        SetStatus("Configuracoes salvas");
    }

    public void SaveAndClose()
    {
        Save();
        gameObject.SetActive(false);
    }

    public void Cancel()
    {
        WriteControls(savedSnapshot);
        ApplyPreview();
        SetStatus("Alteracoes descartadas");
    }

    public void CancelAndClose()
    {
        Cancel();
        gameObject.SetActive(false);
    }

    public void RestoreDefaults()
    {
        WriteControls(new SettingsSnapshot
        {
            ShowFps = false,
            VSync = true,
            ScreenMode = 0,
            ColorBlind = 0,
            Master = DefaultVolume,
            Music = DefaultVolume,
            Sfx = DefaultVolume
        });
        ApplyPreview();
        SetStatus("Padroes restaurados - clique em Salvar");
    }

    public void Open() => gameObject.SetActive(true);

    public void Next()
    {
        if (gameObj == null || gameObj.Length == 0) return;
        SetCurrentTab((currentObj + 1) % gameObj.Length);
    }

    public void Previous()
    {
        if (gameObj == null || gameObj.Length == 0) return;
        SetCurrentTab((currentObj - 1 + gameObj.Length) % gameObj.Length);
    }

    public void SetTab(int tabIndex) => SetCurrentTab(tabIndex);

    private void SetCurrentTab(int tabIndex)
    {
        if (gameObj == null || gameObj.Length == 0) return;
        currentObj = (tabIndex % gameObj.Length + gameObj.Length) % gameObj.Length;
        for (int i = 0; i < gameObj.Length; i++)
        {
            if (gameObj[i] != null) gameObj[i].SetActive(i == currentObj);
        }
    }

    private void LoadSavedSettings()
    {
        WriteControls(new SettingsSnapshot
        {
            ShowFps = PlayerPrefs.GetInt("showFps", 0) == 1,
            VSync = PlayerPrefs.GetInt("vSync", 1) == 1,
            ScreenMode = PlayerPrefs.GetInt("screenMode", 0),
            ColorBlind = PlayerPrefs.GetInt("colorBlindMode", 0),
            Master = PlayerPrefs.GetFloat("masterVolume", DefaultVolume),
            Music = PlayerPrefs.GetFloat("musicVolume", DefaultVolume),
            Sfx = PlayerPrefs.GetFloat("sfxVolume", DefaultVolume)
        });
    }

    private SettingsSnapshot ReadControls()
    {
        return new SettingsSnapshot
        {
            ShowFps = showFps != null && showFps.isOn,
            VSync = vSync != null && vSync.isOn,
            ScreenMode = screenMode != null ? screenMode.value : 0,
            ColorBlind = colorBlind != null ? colorBlind.value : 0,
            Master = master != null ? master.value : DefaultVolume,
            Music = music != null ? music.value : DefaultVolume,
            Sfx = sfx != null ? sfx.value : DefaultVolume
        };
    }

    private void WriteControls(SettingsSnapshot values)
    {
        if (showFps != null) showFps.SetIsOnWithoutNotify(values.ShowFps);
        if (vSync != null) vSync.SetIsOnWithoutNotify(values.VSync);
        if (screenMode != null) screenMode.SetValueWithoutNotify(Mathf.Clamp(values.ScreenMode, 0, Mathf.Max(0, screenMode.options.Count - 1)));
        if (colorBlind != null) colorBlind.SetValueWithoutNotify(Mathf.Clamp(values.ColorBlind, 0, Mathf.Max(0, colorBlind.options.Count - 1)));
        if (master != null) master.SetValueWithoutNotify(values.Master);
        if (music != null) music.SetValueWithoutNotify(values.Music);
        if (sfx != null) sfx.SetValueWithoutNotify(values.Sfx);
        UpdateVolumeLabels();
    }

    private void ApplyPreview()
    {
        SettingsSnapshot values = ReadControls();
        SettingsManager.Instance?.PreviewShowFPS(values.ShowFps);
        SettingsManager.Instance?.PreviewVSync(values.VSync);
        SettingsManager.Instance?.PreviewScreenMode(values.ScreenMode);
        SettingsManager.Instance?.PreviewColorBlindMode(values.ColorBlind);
        SettingsManager.Instance?.PreviewVolume(values.Master, SliderType.Master);
        SettingsManager.Instance?.PreviewVolume(values.Music, SliderType.Music);
        SettingsManager.Instance?.PreviewVolume(values.Sfx, SliderType.Sfx);
        UpdateVolumeLabels();
    }

    private void UpdateVolumeLabels()
    {
        if (masterValue != null && master != null) masterValue.text = Mathf.RoundToInt(master.value * 100f) + "%";
        if (musicValue != null && music != null) musicValue.text = Mathf.RoundToInt(music.value * 100f) + "%";
        if (sfxValue != null && sfx != null) sfxValue.text = Mathf.RoundToInt(sfx.value * 100f) + "%";
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
