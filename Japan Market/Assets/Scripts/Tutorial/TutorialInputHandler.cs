using UnityEngine;
using UnityEngine.EventSystems;

public class TutorialInputHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TutorialManager tutorialManager;

    public void OnPointerClick(PointerEventData eventData)
    {
        tutorialManager.HandleClick();
    }
}
