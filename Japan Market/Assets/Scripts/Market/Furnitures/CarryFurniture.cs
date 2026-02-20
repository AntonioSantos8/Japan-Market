using UnityEngine;

public class CarryFurniture : MonoBehaviour
{
    [SerializeField]
    private Transform player;
    [SerializeField]
    private float distance = 2f;
    [SerializeField]
    private float gridSize = 1f;
    [SerializeField]
    private float height = 1f;
    void Update()
    {
        FollowPlayer();

        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.Rotate(0, 90, 0);
        }
    }

    void FollowPlayer()
    {
        Vector3 targetPos = player.position + player.forward * distance;

        Vector3 snappedPos = new Vector3(
            Mathf.Round(targetPos.x / gridSize) * gridSize,
            targetPos.y - height,
            Mathf.Round(targetPos.z / gridSize) * gridSize
        );

        transform.position = snappedPos;
    }
}