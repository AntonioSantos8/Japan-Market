using UnityEngine;

public class Interruptor : InteractableBase
{
    private bool isOn;
    [SerializeField] private GameObject modelOn;
    [SerializeField] private GameObject modelOff;
    public override void Interact()
    {
        isOn = !isOn;
        if (isOn)
        {
            print("Interruptor is now ON");
            modelOff.SetActive(false);
            modelOn.SetActive(true);
        }
        else
        {
            print("Interruptor is now OFF");
            modelOff.SetActive(true);
            modelOn.SetActive(false);
        }
    }

}
