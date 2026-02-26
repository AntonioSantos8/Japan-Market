using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

    public class ShopManager : MonoBehaviour
{

    static EventSystem eventSystem;

      [SerializeField] GameObject startButtonslecrr;


   

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
        
            eventSystem.SetSelectedGameObject(startButtonslecrr);
        }


    }
 
    public void SelectButton(GameObject btn) { eventSystem.SetSelectedGameObject(btn); }
    
}

