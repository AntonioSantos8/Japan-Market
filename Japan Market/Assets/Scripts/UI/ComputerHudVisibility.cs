using DG.Tweening;
using UnityEngine;

/// <summary>Fades a gameplay HUD without stopping its updates or tutorial logic.</summary>
[DisallowMultipleComponent]
public sealed class ComputerHudVisibility : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

    private CanvasGroup _group;
    private Tween _fade;
    private bool _captured;
    private float _visibleAlpha;
    private bool _visibleInteractable;
    private bool _visibleBlocksRaycasts;

    public void Hide()
    {
        if (_group == null)
            _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        // Keep the original state when re-entering during the fade back in.
        if (!_captured)
        {
            _visibleAlpha = _group.alpha;
            _visibleInteractable = _group.interactable;
            _visibleBlocksRaycasts = _group.blocksRaycasts;
            _captured = true;
        }

        _fade?.Kill();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _fade = _group.DOFade(0f, fadeDuration).SetUpdate(true);
    }

    public void Show(bool immediate = false)
    {
        if (!_captured || _group == null) return;

        _fade?.Kill();
        if (immediate)
        {
            _group.alpha = _visibleAlpha;
            RestoreInteraction();
            return;
        }

        _fade = _group.DOFade(_visibleAlpha, fadeDuration)
            .SetUpdate(true)
            .OnComplete(RestoreInteraction);
    }

    private void RestoreInteraction()
    {
        _group.interactable = _visibleInteractable;
        _group.blocksRaycasts = _visibleBlocksRaycasts;
        _captured = false;
    }

    private void OnDestroy()
    {
        _fade?.Kill();
    }
}
