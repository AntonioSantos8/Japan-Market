using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class ComputerButtonsManager : MonoBehaviour
{
    [SerializeField] UIButtonAnimator[] allButtons;
    [SerializeField] RectTransform leftIndicator, rightIndicator;
    [SerializeField] float indicatorsSpeed;

    RectTransform targetButton;
    Vector3 rightTarget, leftTarget;

    void Start()
    {
        foreach (UIButtonAnimator b in allButtons)
        {
          
            UIButtonAnimator captured = b;

            captured.onSelection.AddListener(() =>
            {
                targetButton = captured.GetComponent<RectTransform>();
                CalculateTargetPos();
            });
        }
    }
[SerializeField] float indicatorsZ = 0f;
   public void CalculateTargetPos()
{
    Vector3[] corners = new Vector3[4];
    targetButton.GetWorldCorners(corners);

    float centerX   = (corners[0].x + corners[2].x) / 2f;
    float centerY   = (corners[0].y + corners[2].y) / 2f;
    float halfWidth = (corners[2].x - corners[0].x) / 2f;

   halfWidth *= targetButton.localScale.x;

    Transform rParent = rightIndicator.parent;
    Transform lParent = leftIndicator.parent;

    Vector3 rWorld = new Vector3(centerX + halfWidth, centerY, 0f);
    Vector3 lWorld = new Vector3(centerX - halfWidth, centerY, 0f);

    rightTarget = rParent != null ? rParent.InverseTransformPoint(rWorld) : rWorld;
    leftTarget  = lParent != null ? lParent.InverseTransformPoint(lWorld) : lWorld;

    rightTarget.z = 0f;
    leftTarget.z  = 0f;
}
   void Update()
{
    rightIndicator.position = Vector3.Lerp(
        rightIndicator.position, rightTarget, indicatorsSpeed * Time.deltaTime);
    rightIndicator.localPosition = new Vector3(
        rightIndicator.localPosition.x,
        rightIndicator.localPosition.y,
        0f);
    leftIndicator.position = Vector3.Lerp(
        leftIndicator.position, leftTarget, indicatorsSpeed * Time.deltaTime);
    leftIndicator.localPosition = new Vector3(
        leftIndicator.localPosition.x,
        leftIndicator.localPosition.y,
        0f);
}
}