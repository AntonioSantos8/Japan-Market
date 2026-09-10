using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.UI;
public enum SliderType { Master, Sfx, Music}
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;
    [SerializeField]TMP_Text fpsText;
    [SerializeField] AudioMixer audioMixer;
    [SerializeField] Material colorBlindMaterial;

    private LocalKeyword cbNone;
    private LocalKeyword cbTritanopia;
    private LocalKeyword cbProtonopia;
    private LocalKeyword cbDeuteranopia;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeColorBlindKeywords();
            ApplySavedSettings();
            return;
        }
        Destroy(gameObject);
    }

    private void InitializeColorBlindKeywords()
    {
        if (colorBlindMaterial != null)
        {
            cbNone = new LocalKeyword(colorBlindMaterial.shader, "_COLORBLINDMODE_NONE");
            cbTritanopia = new LocalKeyword(colorBlindMaterial.shader, "_COLORBLINDMODE_TRITANOPIA");
            cbProtonopia = new LocalKeyword(colorBlindMaterial.shader, "_COLORBLINDMODE_PROTONOPIA");
            cbDeuteranopia = new LocalKeyword(colorBlindMaterial.shader, "_COLORBLINDMODE_DEUTERANOPIA");
        }
    }
    public void ShowFPS(bool showFps) 
    {
        PreviewShowFPS(showFps);
        PlayerPrefs.SetInt("showFps", showFps ? 1 : 0);
    }

    public void PreviewShowFPS(bool showFps)
    {
        if (fpsText != null)
            fpsText.gameObject.SetActive(showFps);
    }

    public void VSync(bool active)
    {
        PreviewVSync(active);
        PlayerPrefs.SetInt("vSync", active ? 1 : 0);
    }

    public void PreviewVSync(bool active) => QualitySettings.vSyncCount = active ? 1 : 0;

    public void SetScreeMode(int index)
    {
        PreviewScreenMode(index);
        PlayerPrefs.SetInt("screenMode", index);
    }

    public void PreviewScreenMode(int index)
    {
        switch (index)
        {
            case 0:
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
            case 1:
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                break;
            case 2:
                Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
                break;
            case 3:
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
        }
    }

    public void SetVolume(float to, SliderType sliderType) 
    {
        PreviewVolume(to, sliderType);
        switch (sliderType)
        {
            case SliderType.Master:
                PlayerPrefs.SetFloat("masterVolume", to);
                break;
            case SliderType.Sfx:
                PlayerPrefs.SetFloat("sfxVolume", to);
                break;
            case SliderType.Music:
                PlayerPrefs.SetFloat("musicVolume", to);
                break;
        }
    }

    public void PreviewVolume(float to, SliderType sliderType)
    {
        if (audioMixer == null) return;

        float realVolume = Mathf.Log10(Mathf.Max(to, 0.0001f)) * 20f;
        string parameter = sliderType switch
        {
            SliderType.Master => "MasterVolume",
            SliderType.Sfx => "SfxVolume",
            _ => "MusicVolume"
        };
        audioMixer.SetFloat(parameter, realVolume);
    }

    public void SetColorBlindMode(int index)
    {
        PreviewColorBlindMode(index);
        PlayerPrefs.SetInt("colorBlindMode", index);
    }

    public void PreviewColorBlindMode(int index)
    {
        if (colorBlindMaterial == null) return;

       
        colorBlindMaterial.SetKeyword(cbNone, false);
        colorBlindMaterial.SetKeyword(cbTritanopia, false);
        colorBlindMaterial.SetKeyword(cbProtonopia, false);
        colorBlindMaterial.SetKeyword(cbDeuteranopia, false);

        
        switch (index)
        {
            case 0: 
                colorBlindMaterial.SetKeyword(cbNone, true);
                break;
            case 1: 
                colorBlindMaterial.SetKeyword(cbTritanopia, true);
                break;
            case 2: 
                colorBlindMaterial.SetKeyword(cbProtonopia, true);
                break;
            case 3: 
                colorBlindMaterial.SetKeyword(cbDeuteranopia, true);
                break;
        }
    }

    private void ApplySavedSettings()
    {
        PreviewShowFPS(PlayerPrefs.GetInt("showFps", 0) == 1);
        PreviewVSync(PlayerPrefs.GetInt("vSync", 1) == 1);
        PreviewScreenMode(PlayerPrefs.GetInt("screenMode", 0));
        PreviewColorBlindMode(PlayerPrefs.GetInt("colorBlindMode", 0));
        PreviewVolume(PlayerPrefs.GetFloat("masterVolume", 0.8f), SliderType.Master);
        PreviewVolume(PlayerPrefs.GetFloat("musicVolume", 0.8f), SliderType.Music);
        PreviewVolume(PlayerPrefs.GetFloat("sfxVolume", 0.8f), SliderType.Sfx);
    }
}
