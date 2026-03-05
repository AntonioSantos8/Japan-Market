using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;
    public class ShopManager : MonoBehaviour
{

    static EventSystem eventSystem;

    [SerializeField] GameObject startButtonslecrr;

   

    private void Awake()
    {
        eventSystem = EventSystem.current;
    }
  public void SelectButton()
    {
        if (startButtonslecrr != null)
        {

            eventSystem.SetSelectedGameObject(startButtonslecrr);
        }
    }
    public void DeselectButton()
    {


        EventSystem.current.SetSelectedGameObject(null);
        
    }

   

    public void SelectButton(GameObject btn) { eventSystem.SetSelectedGameObject(btn); }
    
}

