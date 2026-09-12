using UnityEngine;
using UnityEngine.UI;
public class OptionsGroup : MonoBehaviour
{
    CanvasGroup canvasGroup;
    public CanvasGroup Group => canvasGroup;
    [SerializeField] float alphaSpeed;
    float target = 0f;
    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

    }
    public void SetTarget(float newTarget)
    {
        target = newTarget;
        if(newTarget == 1f)
        {
            Group.blocksRaycasts = true;
            Group.interactable = true;

        }else
        {
             Group.blocksRaycasts = false;
            Group.interactable = false;
            
        }

    }
    public void Update()
    {
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, target, alphaSpeed * Time.deltaTime);
        
    }
  

}