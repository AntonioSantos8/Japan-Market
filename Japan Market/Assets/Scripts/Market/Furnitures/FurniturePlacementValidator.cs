using UnityEngine;

public class FurniturePlacementValidator : MonoBehaviour
{
    [SerializeField] private MeshRenderer[] renderers;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    private int _overlapCount;
    public bool IsValid => _overlapCount == 0;

    private void Start()
    {
        if (renderers.Length == 0) renderers = GetComponentsInChildren<MeshRenderer>();
        UpdateVisuals();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;
        _overlapCount++;
        UpdateVisuals();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.isTrigger) return;
        _overlapCount--;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        Material targetMat = IsValid ? validMaterial : invalidMaterial;
        foreach (var r in renderers)
        {
            r.material = targetMat;
        }
    }
}