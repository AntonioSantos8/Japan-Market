using UnityEngine;
public enum SelectedMode
{
    Ghost,
    GameFurniture
}
public class PlacementFollower : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private float viewDistance = 4f;
    [SerializeField] private float gridSize = 0.5f;
    private FurnitureManager _manager;

    [SerializeField] private GameObject _follow;
    private void Start()
    {
        _manager = ServiceLocator.Get<FurnitureManager>();
    }

    private void Update()
    {
        GameObject ghost = _manager.GetActiveGhost();
        GameObject gameFurniture = _manager.GetActiveGameFurniture();
        if (ghost == null) return;

        UpdateGhostPosition(ghost.transform, gameFurniture.transform);
    }
    
    private void UpdateGhostPosition(Transform ghostTransform, Transform gameFurnitureTransform)
    {
        FurnitureData currentSelected = _manager.GetCurrentSelected();
        Vector3 targetPoint = anchor.position + anchor.forward * viewDistance;

        float snappedX = Mathf.Round(targetPoint.x / gridSize) * gridSize;
        float snappedZ = Mathf.Round(targetPoint.z / gridSize) * gridSize;

        ghostTransform.position = new Vector3(snappedX, currentSelected.floorDistance, snappedZ);
    }
}