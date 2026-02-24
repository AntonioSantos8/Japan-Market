using UnityEngine;

public class PlacementFollower : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private float viewDistance = 4f;
    [SerializeField] private float gridSize = 0.5f;
    [SerializeField] private Transform followPointY;
    private FurnitureManager _manager;
    private void Start()
    {
        _manager = ServiceLocator.Get<FurnitureManager>();
    }

    private void Update()
    {
        GameObject ghost = _manager.GetActiveGhost();
        if (ghost == null) return;

        UpdateGhostPosition(ghost.transform);
    }
    
    private void UpdateGhostPosition(Transform ghostTransform)
    {
        FurnitureData currentSelected = _manager.GetCurrentSelected();
        Vector3 targetPoint = anchor.position + anchor.forward * viewDistance;

        float snappedX = Mathf.Round(targetPoint.x / gridSize) * gridSize;
        float snappedZ = Mathf.Round(targetPoint.z / gridSize) * gridSize;

        ghostTransform.position = new Vector3(snappedX, currentSelected.floorDistance, snappedZ);
    }
}