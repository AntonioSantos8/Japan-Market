using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
public class ExpansionStore : MonoBehaviour
{
    [SerializeField] GameObject[] storesExpansion;
    [SerializeField] List<AllIThingsData> allExpansions;
    [SerializeField] ItemsExample itemsExample;
    [SerializeField] CinemachineCamera mainCamera;
    [SerializeField] CinemachineCamera upgradeCamera;
    [SerializeField] ShopBuyItems shopBuyItems;
    [SerializeField] float animationTime = 1.5f;
    [SerializeField] Color emissionColor = Color.white;
    [SerializeField] float emissionIntensity = 5f;
    [SerializeField] GameObject trianglePrefab;
    GameObject currentStore;
    int currentExpansion = 0;
    Material currentMaterial;

    void Start()
    {
        currentStore = storesExpansion[0];
    }
    public void BuyExpansion()
    {
        if (currentExpansion + 1 >= storesExpansion.Length)
            return;

        currentExpansion++;

        UpdateItem();
        shopBuyItems.RefreshCurrentItem();

        StartCoroutine(UpgradeSequence());
    }

    void UpdateItem()
    {
        if (currentExpansion < allExpansions.Count)
            itemsExample.SetAllThingsData(allExpansions[currentExpansion]);
    }

    IEnumerator UpgradeSequence()
    {
        upgradeCamera.Priority = 10;
        mainCamera.Priority = 0;

        yield return new WaitForSeconds(0.5f);

        Vector3 originalPos = currentStore.transform.position;

       
        Sequence destroySeq = DOTween.Sequence();

        destroySeq.Append(
            currentStore.transform
                .DOScale(Vector3.zero, animationTime)
                .SetEase(Ease.InBack)
        );

        destroySeq.Join(
            currentStore.transform
                .DOMoveY(originalPos.y - 3f, animationTime)
                .SetEase(Ease.InQuad)
        );

        yield return destroySeq.WaitForCompletion();

        Destroy(currentStore);

        
        Vector3 spawnPos = originalPos + Vector3.down * 2f;

        currentStore = Instantiate(
            storesExpansion[currentExpansion],
            spawnPos,
            Quaternion.identity
        );

       SpawnTriangle(currentStore.transform);

      ActiveEmission(currentStore);

        Vector3 targetScale = currentStore.transform.localScale;
        currentStore.transform.localScale = Vector3.zero;

        float emissionValue = 0f;
        EmissionColor(0f);

        Sequence buildSeq = DOTween.Sequence();

        buildSeq.Append(
            currentStore.transform
                .DOMoveY(originalPos.y, animationTime)
                .SetEase(Ease.OutQuad)
        );

        buildSeq.Join(
            currentStore.transform
                .DOScale(targetScale, animationTime)
                .SetEase(Ease.OutBack)
        );

        buildSeq.Join(
            DOTween.To(
                () => emissionValue,
                x =>
                {
                    emissionValue = x;
                    EmissionColor(emissionValue);
                },
                emissionIntensity,
                animationTime
            )
        );

        yield return buildSeq.WaitForCompletion();

       
        EmissionColor(0f);
        if (currentMaterial != null)
            currentMaterial.DisableKeyword("_EMISSION");

       
        upgradeCamera.transform.DOShakePosition(0.1f, 0.4f);

        yield return new WaitForSeconds(0.3f);

        upgradeCamera.Priority = 0;
        mainCamera.Priority = 10;
    }
    void SpawnTriangle(Transform target)
    {
        StartCoroutine(TriangleEffect(target));
    }
    IEnumerator TriangleEffect(Transform target)
    {
        int count = 23;

        for (int i = 0; i < count; i++)
        {
            GameObject tri = Instantiate(trianglePrefab);

            Vector3 randomOffset = new Vector3(
                Random.Range(-1.5f, 1.5f),
                0,
                Random.Range(-1.5f, 1.5f)
            );

            Vector3 startPos = target.position + randomOffset;
            Vector3 endPos = startPos + Vector3.up * Random.Range(2f, 4f);

            tri.transform.position = startPos;
            tri.transform.localScale = Vector3.zero;

            float duration = Random.Range(0.8f, 1.2f);

            Sequence seq = DOTween.Sequence();

            seq.Append(
                tri.transform.DOScale(0.2f, 0.2f).SetEase(Ease.OutBack)
            );

           seq.Join(
                tri.transform.DOMove(endPos, duration).SetEase(Ease.OutQuad)
            );

            seq.Join(
                tri.transform.DORotate(new Vector3(0, 0, 180f), duration)
            );

            
            seq.Append(
                tri.transform.DOScale(0f, 0.3f)
            );

            Destroy(tri, duration + 0.5f);

            yield return new WaitForSeconds(0.05f);
        }
    }
    void ActiveEmission(GameObject obj)
    {
        Renderer rend = obj.GetComponentInChildren<Renderer>();

        if (rend != null)
        {
            currentMaterial = rend.material;
            currentMaterial.EnableKeyword("_EMISSION");
            currentMaterial.SetColor("_EmissionColor", Color.black);
        }
    }
    void EmissionColor(float intensity)
    {
        if (currentMaterial != null)
        {
            Color finalColor = emissionColor * intensity;
            currentMaterial.SetColor("_EmissionColor", finalColor);
        }
    }
   public int GetCurrentExpansion() => currentExpansion;
   public bool HasMoreExpansions() => currentExpansion + 1 < storesExpansion.Length;
}