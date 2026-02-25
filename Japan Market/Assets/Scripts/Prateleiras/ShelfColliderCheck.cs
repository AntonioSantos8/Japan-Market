using UnityEngine;

public class ShelfColliderCheck : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
{

	if(other.gameObject.layer == LayerMask.NameToLayer("InShelf"))
{other.gameObject.layer =LayerMask.NameToLayer("Interactive"); }

}
}
