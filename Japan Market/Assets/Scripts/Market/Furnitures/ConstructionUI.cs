using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using UnityEngine.UI;

public class ConstructionUI : MonoBehaviour
{
    [SerializeField] private GameObject panelMode;
    [SerializeField] private TextMeshProUGUI textCurrentItem;
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color inactiveColor = Color.red;
    [SerializeField] private Image statusIndicator;

    private FurnitureManager _manager;
    private bool _wasBuilding;

    private Volume _buildModeVolume;
    private Tween _volumeTween;
    private Sequence _flashSequence;

    private Vignette _vignette;
    private ColorAdjustments _colorAdj;

    private static readonly Color BuildFilterColor   = new(0.88f, 0.95f, 1.00f);
    private static readonly Color BuildVignetteColor = new(0.10f, 0.12f, 0.30f);
    private static readonly Color GoodFlashFilter    = new(0.55f, 1.00f, 0.60f);
    private static readonly Color GoodFlashVignette  = new(0.05f, 0.45f, 0.10f);
    private static readonly Color BadFlashFilter     = new(1.00f, 0.40f, 0.35f);
    private static readonly Color BadFlashVignette   = new(0.50f, 0.05f, 0.05f);

    private void Awake()
    {
        // Register em Awake: todos os Awake correm antes de qualquer Start.
        ServiceLocator.Register(this);
    }

    private void Start()
    {
        _manager = ServiceLocator.Get<FurnitureManager>();
        panelMode.SetActive(false);
        BuildVolume();
    }

    private void OnDestroy()
    {
        _volumeTween?.Kill();
        _flashSequence?.Kill();
        if (_buildModeVolume != null)
            Destroy(_buildModeVolume.gameObject);
    }

    private void BuildVolume()
    {
        var go = new GameObject("BuildModePostProcess");
        _buildModeVolume = go.AddComponent<Volume>();
        _buildModeVolume.isGlobal = true;
        _buildModeVolume.priority = 10f;
        _buildModeVolume.weight = 0f;
        _buildModeVolume.sharedProfile = CreateProfile();
    }

    private VolumeProfile CreateProfile()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        _vignette = profile.Add<Vignette>(true);
        _vignette.color.Override(BuildVignetteColor);
        _vignette.intensity.Override(0.3f);
        _vignette.smoothness.Override(0.45f);

        var ca = profile.Add<ChromaticAberration>(true);
        ca.intensity.Override(0.42f);

        var wb = profile.Add<WhiteBalance>(true);
        wb.temperature.Override(-32f);

        _colorAdj = profile.Add<ColorAdjustments>(true);
        _colorAdj.saturation.Override(-28f);
        _colorAdj.contrast.Override(18f);
        _colorAdj.colorFilter.Override(BuildFilterColor);

        return profile;
    }

    // ── Public Flash API ─────────────────────────────────────────────────────

    public void FlashGood() => DoFlash(GoodFlashFilter, GoodFlashVignette);
    public void FlashBad()  => DoFlash(BadFlashFilter,  BadFlashVignette);

    private void DoFlash(Color filterColor, Color vignetteColor)
    {
        if (_buildModeVolume == null) return;

        _flashSequence?.Kill(true);
        _volumeTween?.Kill(false);

        float restWeight = _manager.IsBuildingMode ? 1f : 0f;
        float peakWeight = Mathf.Max(restWeight, 0.85f);

        _flashSequence = DOTween.Sequence()
            // spike in
            .Append(DOTween.To(
                () => _buildModeVolume.weight,
                x  => _buildModeVolume.weight = x,
                peakWeight, 0.06f).SetEase(Ease.OutQuad))
            .Join(DOTween.To(
                () => _colorAdj.colorFilter.value,
                x  => _colorAdj.colorFilter.Override(x),
                filterColor, 0.06f))
            .Join(DOTween.To(
                () => _vignette.color.value,
                x  => _vignette.color.Override(x),
                vignetteColor, 0.06f))
            // hold
            .AppendInterval(0.10f)
            // fade back
            .Append(DOTween.To(
                () => _buildModeVolume.weight,
                x  => _buildModeVolume.weight = x,
                restWeight, 0.50f).SetEase(Ease.OutCubic))
            .Join(DOTween.To(
                () => _colorAdj.colorFilter.value,
                x  => _colorAdj.colorFilter.Override(x),
                BuildFilterColor, 0.50f))
            .Join(DOTween.To(
                () => _vignette.color.value,
                x  => _vignette.color.Override(x),
                BuildVignetteColor, 0.50f));
    }

    // ── Build-mode volume ────────────────────────────────────────────────────

    private void Update()
    {
        bool isBuilding = _manager.IsBuildingMode;

        if (isBuilding != _wasBuilding)
        {
            _wasBuilding = isBuilding;

            if (isBuilding)
                OnEnterBuildMode();
            else
                OnExitBuildMode();
        }

        if (isBuilding && _manager.HasFurnitureInInventory)
            SetText();
        else
            textCurrentItem.text = "Nenhum selecionado";
    }

    private void OnEnterBuildMode()
    {
        panelMode.SetActive(true);
        panelMode.transform.localScale = Vector3.zero;
        panelMode.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);

        AnimateVolume(1f, 0.5f, Ease.OutCubic);
    }

    private void OnExitBuildMode()
    {
        panelMode.transform.DOKill();
        panelMode.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() => panelMode.SetActive(false));

        AnimateVolume(0f, 0.35f, Ease.InCubic);
    }

    private void AnimateVolume(float target, float duration, Ease ease)
    {
        if (_buildModeVolume == null) return;

        _flashSequence?.Kill(true);
        _volumeTween?.Kill();
        _volumeTween = DOTween
            .To(() => _buildModeVolume.weight, x => _buildModeVolume.weight = x, target, duration)
            .SetEase(ease);
    }

    // ── Text ─────────────────────────────────────────────────────────────────

    public void SetText()
    {
        FurnitureData current = _manager.GetCurrentSelected();
        if (current == null)
        {
            textCurrentItem.text = "Nenhum selecionado";
            if (statusIndicator != null) statusIndicator.color = inactiveColor;
            return;
        }

        int count = _manager.InventoryCount;
        textCurrentItem.text = count > 1
            ? $"Item: {current.furnitureName} ({count} restantes)"
            : $"Item: {current.furnitureName}";

        if (statusIndicator != null) statusIndicator.color = activeColor;
    }
}
