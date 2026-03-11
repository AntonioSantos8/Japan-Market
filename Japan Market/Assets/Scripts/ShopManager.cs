using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEditor.Search;
public class ShopManager : MonoBehaviour
{

    static EventSystem eventSystem;

    [SerializeField] GameObject startButtonslecrr;

    [SerializeField] GameObject furniturePanel;
    [SerializeField] GameObject optionsPanel;
   

    private void Awake()
    {
        ServiceLocator.Register(this);
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

    public void ExitFurnitureSesion()
    {
        if (!furniturePanel.activeSelf)
            return;

        furniturePanel.SetActive(false);
        optionsPanel.SetActive(true);
        SelectButton(startButtonslecrr);
        ServiceLocator.Get<ButtonOrganizer>().SetLastSelectedButton();
    }

    public void SelectButton(GameObject btn) { eventSystem.SetSelectedGameObject(btn); }
    
}

