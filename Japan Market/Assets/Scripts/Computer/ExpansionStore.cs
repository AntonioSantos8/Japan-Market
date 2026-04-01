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
    GameObject currentStore;
    int currentExpansion = 0;
   
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

        Vector3 targetScale = currentStore.transform.localScale;
        currentStore.transform.localScale = Vector3.zero;

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

        yield return buildSeq.WaitForCompletion();

        upgradeCamera.transform.DOShakePosition(0.1f, 0.5f);

        yield return new WaitForSeconds(0.3f);

        upgradeCamera.Priority = 0;
        mainCamera.Priority = 10;
    }
 public int GetCurrentExpansion() => currentExpansion;
 public bool HasMoreExpansions() => currentExpansion + 1 < storesExpansion.Length;
}