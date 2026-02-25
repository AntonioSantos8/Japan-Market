using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

    public class ShopManager : MonoBehaviour
{
 
    static EventSystem eventSystem;
    [SerializeField] string sceneToLoad;

      [SerializeField] GameObject startButtonslecrr;






 
    [SerializeField] GameObject mainMenuContent;
    [SerializeField] GameObject optionsMenuContent;


    private void Awake()
    {
        eventSystem = EventSystem.current;
    }
    void Start()
    {
       
        if (eventSystem.currentSelectedGameObject == null)
        {
            eventSystem.SetSelectedGameObject(eventSystem.firstSelectedGameObject);
        }
        if (startButtonslecrr != null)
        {
            print("start selected");
            eventSystem.SetSelectedGameObject(startButtonslecrr);
        }


    }
 
    public void SelectButton(GameObject btn) { eventSystem.SetSelectedGameObject(btn); }
    public void OnStartButton()
    {
        mainMenuContent.SetActive(true);
        optionsMenuContent.SetActive(false);
        if (startButtonslecrr != null)
            eventSystem.SetSelectedGameObject(startButtonslecrr);
    }

    
}

