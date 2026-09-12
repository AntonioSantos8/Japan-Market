using UnityEngine;
using DG.Tweening;
public class MainMenuManager : MonoBehaviour
{
    
    [SerializeField] OptionsGroup[] allOptions;
    

    void Start()
    {
        SetOption(0);
    
    }

    public void SetOption(int option)
    {
        foreach(var options in allOptions)
        {
                options.SetTarget(0f);
               
        }
        allOptions[option].SetTarget(1f);
        


    }

}
