using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

public class Clean : MonoBehaviour
{
    [SerializeField] float mimScale = 0.2f;
    [SerializeField] float speed = 1.3f;
    bool isCleaning;
    bool rubbing;
    [SerializeField] float respawnDust = 5f;


    private void Update()
    {
       
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Vassoura"))
        {
            if (!isCleaning)
            {
                StartCoroutine(ClearDust());
            }
            
        }
    }
  private IEnumerator ClearDust()
    {
      if (transform.localScale.x < mimScale)
      { 
            transform.localScale -= Vector3.one * speed * Time.deltaTime;
      }

        yield return null;
    }
    IEnumerator RespawnDust()
    {

        yield return null;
    }

}

   
    
    
 

   
   


