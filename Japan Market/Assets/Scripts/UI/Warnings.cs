using System.Collections;
using TMPro;
using UnityEngine;
using DG.Tweening;
using Unity.VisualScripting;
public class Warnings : MonoBehaviour
{
    [SerializeField] private TMP_Text warningTmp;
    [SerializeField] private float warningDuration = 2f;
    [SerializeField] private bool isWarningActive = false;

    public bool IsWarningActive { get => isWarningActive; set => isWarningActive = value; }

    private void Start()
    {
        ServiceLocator.Register(this);
    }
    public void ShowWarning(string message, bool goodWarning)
    {
        StartCoroutine(WarningCoroutine(message, goodWarning));
    }
    public void ShowGoodWarning(string message)
    {
        StartCoroutine(WarningCoroutine(message, true));
    }
    public void ShowBadWarning(string message)
    {
        StartCoroutine(WarningCoroutine(message, false));
    }
    private void HideWarning()
    {
        warningTmp.DOFade(0f, 0.4f)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                warningTmp.text = "";
                IsWarningActive = false;
            });
    }

    private IEnumerator WarningCoroutine(string message, bool goodWarning)
    {
        if (IsWarningActive)
            yield break;
        IsWarningActive = true;

        if (goodWarning)
            warningTmp.color = Color.green;
        else
            warningTmp.color = Color.red;

        warningTmp.alpha = 0f;
        warningTmp.text = message;

        warningTmp.DOFade(1f, 0.35f).SetEase(Ease.OutCubic);
        warningTmp.transform.DOPunchScale(Vector3.one * 0.11f, 0.35f, 5, 0.5f);

        ConstructionUI ui = ServiceLocator.Get<ConstructionUI>();
        if (ui != null)
        {
            if (goodWarning) ui.FlashGood();
            else             ui.FlashBad();
        }

        yield return new WaitForSeconds(warningDuration);
        HideWarning();
    }
}
