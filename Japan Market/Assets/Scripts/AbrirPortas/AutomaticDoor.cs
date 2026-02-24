using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class AutomaticDoor : MonoBehaviour
{
    [SerializeField] Transform doorTransform;
    [SerializeField] Vector3 closePos;
    [SerializeField] Vector4 openPos;
    bool isOpen;
    [SerializeField] float duration;
    [SerializeField] float distance;
    [SerializeField] LayerMask layerMask;
    Coroutine coroutine;

    private void FixedUpdate()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, distance, layerMask);

        bool prevBool = isOpen; 
         isOpen = colliders.Length > 0;

        if (isOpen != prevBool)
        {

            ToggleDoor();
         }

    }
    public void ToggleDoor()
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }

        coroutine = StartCoroutine(TweenCoroutine(isOpen));
    }
    private IEnumerator TweenCoroutine (bool open)
    {
       isOpen = open;

        Vector3 starPos = doorTransform.position;   
        Vector3 targetPos = open ? openPos : closePos;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            doorTransform.position = Vector3.Lerp(starPos, targetPos,t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        doorTransform.position = targetPos;

    }
   

}
