using UnityEngine;

/// <summary>Legacy bin marker. Recycling now uses the equipped bag.</summary>
public class TrashBin : MonoBehaviour
{
    private void Awake()
    {
        if (GetComponent<TrashBinInteraction>() == null)
            gameObject.AddComponent<TrashBinInteraction>();
    }
}
