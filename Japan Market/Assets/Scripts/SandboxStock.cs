using JapanMarket.Data;
using JapanMarket.Gameplay;
using UnityEngine;

public sealed class SandboxStock : MonoBehaviour
{
    public ItemDefinition Product;
    private void Start()
    {
        var storage = GetComponent<ProductStorage>();
        for (int i=0;i<8;i++) if (!storage.TryPlace(Product,out _)) break;
    }
}
