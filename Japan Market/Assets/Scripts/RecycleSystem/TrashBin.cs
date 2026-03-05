using UnityEngine;

public class TrashBin : MonoBehaviour
{
    [SerializeField] private TrashType acceptedType;
    [SerializeField] private TrashSystem trashSystem;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out TrashInstance trash))
        {
            if (trash.TrashData.type == acceptedType)
            {
                Debug.Log($"acertou {trash.TrashData.trashName} reciclado");
                Recycle(other.gameObject);
            }
            else
            {
                Debug.Log("errou");
            }
        }
    }

    private void Recycle(GameObject trashObj)
    {
        trashSystem.UnregisterTrash(trashObj);
        Destroy(trashObj);
    }
}