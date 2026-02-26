using UnityEngine;
using System.Collections;
public class Clean : MonoBehaviour
{
   [SerializeField] float timerAppear = 3;
   [SerializeField] GameObject[] dust;
    Coroutine coroutine;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Vassoura"))
        {
         
        }
    }
    
    
 

   
   

}
