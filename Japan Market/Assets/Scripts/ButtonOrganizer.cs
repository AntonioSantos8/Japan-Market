using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonOrganizer : MonoBehaviour
{
    GameObject lastSelectedButton;
    EventSystem eventSystem;
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

        eventSystem.SetSelectedGameObject(lastSelectedButton);

    }
}
