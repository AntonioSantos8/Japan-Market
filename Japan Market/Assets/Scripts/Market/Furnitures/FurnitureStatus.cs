using TMPro;
using UnityEngine;

public class FurnitureStatus : MonoBehaviour
{
    private FurnitureManager _manager;
    [SerializeField] private FurnitureType _type;
    //teste
    [SerializeField] private TMP_Text textQnt;
    [SerializeField] private SegmentTypeGroup segment;
    private void Start()
    {
        if (_manager == null) return;
        _manager = ServiceLocator.Get<FurnitureManager>();
        _type = _manager.GetCurrentSelected().type;
    }
    private void Update()
    {
        //teste
        //print(segment.GetNullSpace());
    }
}
