using DG.Tweening;
using UnityEngine;

public class FurniturePlacementValidator : MonoBehaviour
{
    [SerializeField] private MeshRenderer[] renderers;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    private int _overlapCount;
    private bool _wasValid;
    private bool _suspended;

    public bool IsValid => _overlapCount == 0;

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<MeshRenderer>();
    }

    private void Start()
    {
        _wasValid = IsValid;
        ApplyMaterials(_wasValid);
    }

    public void Suspend()
    {
        if (_suspended) return;
        _suspended = true;
        transform.DOKill(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_suspended || other.isTrigger) return;
        _overlapCount++;
        UpdateVisuals();
    }

    private void OnTriggerExit(Collider other)
    {
        if (_suspended || other.isTrigger) return;
        _overlapCount = Mathf.Max(0, _overlapCount - 1);
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        bool isValid = IsValid;
        ApplyMaterials(isValid);

        if (isValid == _wasValid) return;
        _wasValid = isValid;

        transform.DOKill(true);
        if (!isValid)
            transform.DOShakeRotation(0.35f, new Vector3(0f, 0f, 14f), 22, 45f, true);
    }

    private void ApplyMaterials(bool isValid)
    {
        Material targetMaterial = isValid ? validMaterial : invalidMaterial;
        if (targetMaterial == null || renderers == null) return;

        foreach (MeshRenderer meshRenderer in renderers)
            if (meshRenderer != null) meshRenderer.material = targetMaterial;
    }
}
