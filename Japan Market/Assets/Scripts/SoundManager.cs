using JapanMarket.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum SFX
{
    AbrirCaixa, // não usado mais em código — substituído por PegarItem. Mantido pra não deslocar os índices já configurados na cena.
    GanharDinheiro,
    PopItemPrateleira,
    PortaAutomaticaAbrir,
    PararDeVerSegmento,
    VerSegmentoInteragivel,
    FurnitureColocada,
    WooshTransicaoItem,
    PegarMoeda,
    WooshPegarDinheiro,
    Passo,
    ClicarBotaoMaquinaCartao,
    ComprarItemOuFurnitureComputador,
    LojaAbertaFechada,
    GastarDinheiro,
    NaoPodePagarSemDinheiro,
    NavegacaoBotoesComputador,
    PCLigarDesligar,
    PegarItem,
    Warning, ButtonHover, ButtonClick, ButtonUnhover,
    WheelOpen, WheelHover, WheelSelect, WheelClose, ScanItem,
    PanelOpen, PanelClose
}

[System.Serializable]
public class SoundConfig
{
    public SFX sound;
    public List<AudioClip> clips;

   

    [Range(0f, 1f)]
    public float volume = 1f;
    public int poolSize = 3;
    public AudioMixerGroup mixerGroup;
    public bool randomPitch = false;

    [Range(0f, 0.5f)]
    public float pitchVariation = 0.1f;

    [Header("Audio Espacial 3D")]
    public bool spatialAudio3D = false;
    public float dopplerLevel = 1f;
    [Range(0f, 360f)]
    public float spread = 0f;
    public float minDistance = 1f;
    public float maxDistance = 500f;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    public AudioClip GetRandomClip()
    {
        if (clips == null || clips.Count == 0) return null;
        return clips[Random.Range(0, clips.Count)];
    }
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private List<SoundConfig> soundConfigs;

    private Dictionary<SFX, SoundConfig> _configs = new();
    private Dictionary<SFX, Queue<AudioSource>> _pool = new();
    private readonly Dictionary<SFX, HashSet<AudioSource>> _active = new();
    private readonly Dictionary<AudioSource, Coroutine> _returns = new();



    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        ServiceLocator.Register(this);
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameAudio.Requested += OnGameAudioRequested;
        InitPool();

    
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A cena Main cria um novo container no Awake. Como este objeto persiste
        // desde o menu, seu Awake não roda outra vez para registrá-lo ali.
        ServiceLocator.Register(this);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameAudio.Requested -= OnGameAudioRequested;
        Instance = null;
    }


    private void OnGameAudioRequested(GameAudioCue cue)
    {
        switch (cue)
        {
            case GameAudioCue.ButtonHover: Play(SFX.ButtonHover); break;
            case GameAudioCue.ButtonClick: Play(SFX.ButtonClick); break;
            case GameAudioCue.ButtonUnhover: Play(SFX.ButtonUnhover); break;
        }
    }

    private void InitPool()
    {
        foreach (var config in soundConfigs)
        {
            _configs[config.sound] = config;
            _pool[config.sound] = new Queue<AudioSource>();
            _active[config.sound] = new HashSet<AudioSource>();

            for (int i = 0; i < config.poolSize; i++)
                _pool[config.sound].Enqueue(CreateSource(config));
        }
    }

    private AudioSource CreateSource(SoundConfig config)
    {
        var go = new GameObject($"SFX_{config.sound}");
        go.transform.SetParent(transform);

        var src = go.AddComponent<AudioSource>();
        src.volume = config.volume;
        src.outputAudioMixerGroup = config.mixerGroup;
        src.playOnAwake = false;

        ApplySpatialSettings(src, config);

        return src;
    }

    private void ApplySpatialSettings(AudioSource src, SoundConfig config)
    {
        src.spatialBlend = config.spatialAudio3D ? 1f : 0f;
        src.dopplerLevel = config.dopplerLevel;
        src.spread = config.spread;
        src.minDistance = config.minDistance;
        src.maxDistance = config.maxDistance;
        src.rolloffMode = config.rolloffMode;
    }

    public void Play(SFX sound)
    {
        if (!_pool.TryGetValue(sound, out var queue) || queue.Count == 0)
        {
            return;
        }

        var config = _configs[sound];
        var source = queue.Dequeue();

        source.clip = config.GetRandomClip();
        if (source.clip == null) { queue.Enqueue(source); return; }

        source.pitch = config.randomPitch
            ? 1f + Random.Range(-config.pitchVariation, config.pitchVariation)
            : 1f;

        source.Play();
        _active[sound].Add(source);
        _returns[source] = StartCoroutine(ReturnToPool(sound, source));
    }


    public void PlayAt(SFX sound, Vector3 position)
    {
        if (!_pool.TryGetValue(sound, out var queue) || queue.Count == 0)
        { 
           
            return;
        }

        var config = _configs[sound];
        var source = queue.Dequeue();

        source.clip = config.GetRandomClip();
        if (source.clip == null) { queue.Enqueue(source); return; }

        source.transform.position = position;
        source.spatialBlend = 1f;

        source.pitch = config.randomPitch
            ? 1f + Random.Range(-config.pitchVariation, config.pitchVariation)
            : 1f;

        source.Play();
        _active[sound].Add(source);
        _returns[source] = StartCoroutine(ReturnToPool(sound, source));
    }


    public void Stop(SFX sound)
    {
        if (!_pool.TryGetValue(sound, out var queue)) return;

        foreach (var source in _active[sound])
        {
            if (_returns.TryGetValue(source, out var routine)) StopCoroutine(routine);
            _returns.Remove(source);
            source.Stop();
            ApplySpatialSettings(source, _configs[sound]);
            source.transform.position = transform.position;
            queue.Enqueue(source);
        }
        _active[sound].Clear();
    }



    private IEnumerator ReturnToPool(SFX sound, AudioSource source)
    {

        yield return new WaitForSecondsRealtime(source.clip.length / source.pitch);

        source.Stop();
        ApplySpatialSettings(source, _configs[sound]);
        source.transform.position = transform.position;

        _returns.Remove(source);
        _active[sound].Remove(source);
        _pool[sound].Enqueue(source);
    }
}
