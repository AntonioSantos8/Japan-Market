using UnityEngine;

public class TrashBin : MonoBehaviour
{
    [SerializeField] private TrashType acceptedType;

    [Header("Rejeição")]
    [Tooltip("Força do impulso quando o lixo é colocado no lugar errado.")]
    [SerializeField] private float _rejectForce = 5f;

    [Header("Recompensa")]
    [SerializeField] private float _coinReward = 50f;
    [Tooltip("Prefab do pop '+50 ¥' que aparece acima da lixeira ao reciclar corretamente.")]
    [SerializeField] private GameObject _coinPopPrefab;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out TrashInstance trash)) return;
        if (trash.TrashData == null) return;

        var controller = ServiceLocator.Get<ItemRaycastController>();

        if (trash.TrashData.type == acceptedType)
        {
            // Solta da mão do player antes de destruir
            if (controller.HeldItem != null)
                controller.DropItem();

            Recycle(other.gameObject);
        }
        else
        {
            RejectTrash(other.gameObject, controller);
        }
    }

    private void Recycle(GameObject trashObj)
    {
        ServiceLocator.Get<TrashSystem>().UnregisterTrash(trashObj);
        Destroy(trashObj);

        ServiceLocator.Get<MarketManager>().Earn_Money(_coinReward);

        if (_coinPopPrefab != null)
        {
            GameObject pop = Instantiate(_coinPopPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);
            Destroy(pop, 3f);
        }
    }

    private void RejectTrash(GameObject trashObj, ItemRaycastController controller)
    {
        // Solta da mão — DropItem já zera a velocidade do Rigidbody
        if (controller.HeldItem != null)
            controller.DropItem();

        // Impulso: sai voando para longe da lixeira com arco pra cima
        if (trashObj.TryGetComponent(out Rigidbody rb))
        {
            Vector3 dir = (trashObj.transform.position - transform.position).normalized;
            dir.y = 0.5f;
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(dir.normalized * _rejectForce, ForceMode.Impulse);
        }

        ServiceLocator.Get<Warnings>().ShowWarning("Lixo errado!", false);
    }
}
