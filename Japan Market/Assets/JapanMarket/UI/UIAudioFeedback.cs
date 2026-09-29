using JapanMarket.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JapanMarket.UI
{
    [RequireComponent(typeof(Selectable))]
    [DisallowMultipleComponent]
    public sealed class UIAudioFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        private Selectable _control;
        private bool _pointerInside;

        public static void Ensure(Selectable control)
        {
            if (control != null && control.GetComponent<UIAudioFeedback>() == null)
                control.gameObject.AddComponent<UIAudioFeedback>();
        }

        private void Awake() => _control = GetComponent<Selectable>();

        private bool CanPlay => _control != null && _control.interactable
            && GetComponent("MenuButtonFX") == null
            && GetComponent("UIButtonAnimator") == null;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            if (CanPlay) GameAudio.Play(GameAudioCue.ButtonHover);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            if (CanPlay) GameAudio.Play(GameAudioCue.ButtonUnhover);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (CanPlay) GameAudio.Play(GameAudioCue.ButtonClick);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!_pointerInside && CanPlay) GameAudio.Play(GameAudioCue.ButtonHover);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (!_pointerInside && CanPlay) GameAudio.Play(GameAudioCue.ButtonUnhover);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (CanPlay) GameAudio.Play(GameAudioCue.ButtonClick);
        }
    }
}
