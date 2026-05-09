using DG.Tweening;
using UnityEngine;

public class ComputerButtonsManager : MonoBehaviour
{
    [SerializeField] UIButtonAnimator[] allButtons;
    [SerializeField] RectTransform leftIndicator, rightIndicator;
    [SerializeField] float indicatorsSpeed = 8f;
    [SerializeField] float indicatorsSpacing = 1f;

    [SerializeField] float switchDuration = 0.4f;
    [SerializeField] Vector3 switchScalePunch = new Vector3(0.4f, -0.2f, 0f); 
    [SerializeField] Vector3 switchRotPunch   = new Vector3(0f, 0f, 12f);    
    [SerializeField] int   switchVibrato      = 2;
    [SerializeField] float switchElasticity   = 0.4f;


    [SerializeField] float clickDuration = 0.3f;
    [SerializeField] Vector3 clickScalePunch = new Vector3(-0.15f, 0.35f, 0f); 
    [SerializeField] Vector3 clickRotPunch   = new Vector3(0f, 0f, 6f);
    [SerializeField] int   clickVibrato      = 1;
    [SerializeField] float clickElasticity   = 0.3f;

    RectTransform targetButton;
    Vector3 rightTarget, leftTarget;


    Vector3 rightBaseScale;
    Vector3 leftBaseScale;
    Quaternion rightBaseRot;
    Quaternion leftBaseRot;

    [SerializeField] RectTransform firstButton;

    void Start()
    {
        rightBaseScale = rightIndicator.localScale;
        leftBaseScale  = leftIndicator.localScale;
        rightBaseRot   = rightIndicator.localRotation;
        leftBaseRot    = leftIndicator.localRotation;

        foreach (UIButtonAnimator b in allButtons)
        {
            UIButtonAnimator captured = b;

            captured.onSelection.AddListener(() =>
            {
                targetButton = captured.GetComponent<RectTransform>();
                PunchSwitch();
            });

            captured.onClicked.AddListener(PunchClick);
        }
        targetButton = firstButton;
    }

    void ResetIndicators()
    {
       
        leftIndicator.DOKill();
        rightIndicator.DOKill();



        leftIndicator.localScale    = leftBaseScale;
        rightIndicator.localScale   = rightBaseScale;
        leftIndicator.localRotation = leftBaseRot;
        rightIndicator.localRotation = rightBaseRot;
    }

    void PunchSwitch()
    {
        ResetIndicators();

     
        rightIndicator.DOPunchScale(switchScalePunch, switchDuration, switchVibrato, switchElasticity).SetUpdate(true);
        rightIndicator.DOPunchRotation(switchRotPunch, switchDuration, switchVibrato, switchElasticity).SetUpdate(true);

    
        leftIndicator.DOPunchScale(new Vector3(-switchScalePunch.x, switchScalePunch.y, 0f), switchDuration, switchVibrato, switchElasticity)  .SetUpdate(true);
        leftIndicator.DOPunchRotation(new Vector3(0f, 0f, -switchRotPunch.z), switchDuration, switchVibrato, switchElasticity).SetUpdate(true);
    }

    void PunchClick()
    {
        ResetIndicators();

         rightIndicator.DOPunchScale(clickScalePunch, clickDuration, clickVibrato, clickElasticity) .SetUpdate(true);
        rightIndicator.DOPunchRotation(clickRotPunch, clickDuration, clickVibrato, clickElasticity).SetUpdate(true);

        leftIndicator.DOPunchScale(new Vector3(-clickScalePunch.x, clickScalePunch.y, 0f), clickDuration, clickVibrato, clickElasticity).SetUpdate(true);
        leftIndicator.DOPunchRotation(new Vector3(0f, 0f, -clickRotPunch.z), clickDuration, clickVibrato, clickElasticity).SetUpdate(true);
    }

    public void CalculateTargetPos()
    {
        Vector3[] corners = new Vector3[4];
        targetButton.GetWorldCorners(corners);
        float centerX   = (corners[0].x + corners[2].x) / 2f;
        float centerY   = (corners[0].y + corners[2].y) / 2f;
        float halfWidth = (corners[2].x - corners[0].x) / 2f * indicatorsSpacing;
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
        if (targetButton == null) return;

        CalculateTargetPos();

        rightIndicator.localPosition = Vector3.Lerp(rightIndicator.localPosition, rightTarget, indicatorsSpeed * Time.deltaTime);

        leftIndicator.localPosition = Vector3.Lerp(leftIndicator.localPosition, leftTarget, indicatorsSpeed * Time.deltaTime);
    }
}