using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonOrganizer : MonoBehaviour
{
    GameObject lastSelectedButton;
    EventSystem eventSystem;
    private void Awake()
    {
        // Register em Awake: todos os Awake correm antes de qualquer Start.
        ServiceLocator.Register(this);
    }
    private void Start()
    {
        eventSystem = EventSystem.current;
    }
    public void ChangeLastSelected(GameObject button)
    {

        lastSelectedButton = button;
    }
    public void SetLastSelectedButton() 
    {
        eventSystem.SetSelectedGameObject(null   );
        lastSelectedButton.GetComponent<UIButtonAnimator>().OnSelect( null);
        eventSystem.SetSelectedGameObject(lastSelectedButton);

    }
}
