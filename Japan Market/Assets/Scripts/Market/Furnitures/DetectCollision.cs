using UnityEngine;

public class DetectCollision : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Furniture"))
        {
            ServiceLocator.Get<FurnituresManager>().furnitureInWay = true;
            print("Entrou");
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Furniture"))
        {
            ServiceLocator.Get<FurnituresManager>().furnitureInWay = false;
            print("Saiu");
        }
    }
}
