using Unity.Android.Gradle.Manifest;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonOrganizer : MonoBehaviour
{
    GameObject lastSelectedButton;
    EventSystem eventSystem;
    private void Start()
    {
        eventSystem = EventSystem.current;
        ServiceLocator.Register(this);
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
