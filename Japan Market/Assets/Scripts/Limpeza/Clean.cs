using System.Collections;
using UnityEngine;

/// <summary>
/// Sujeira no chão da loja. Pode ser limpa com a vassoura.
/// Mantém um contador global (ActiveDustCount) usado pelos NPCs
/// para reagir a uma loja suja.
/// </summary>
public class Clean : MonoBehaviour
{
    /// <summary>Quantidade de sujeiras ativas na cena.</summary>
    public static int ActiveDustCount { get; private set; }

    [SerializeField] float mimScale = 0.2f;
    [SerializeField] float speed = 1.3f;

    bool isCleaning;

    private void OnEnable() => ActiveDustCount++;

    // OnDisable também é chamado quando o objeto é destruído,
    // então o contador se mantém correto ao limpar a sujeira.
    private void OnDisable() => ActiveDustCount--;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Vassoura")) return;
        if (isCleaning) return;

        isCleaning = true;
        StartCoroutine(ClearDust());
    }

    private IEnumerator ClearDust()
    {
        while (transform.localScale.x >= mimScale)
        {
            transform.localScale -= Vector3.one * speed * Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
