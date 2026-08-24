using UnityEngine;

[System.Serializable]
public class ItemGridConfig
{
    [Header("Offset e Spacing")]
    public Vector3 originOffset;
    public float spacingX = 0.1f;
    public float spacingY = 0.1f;
    public float spacingZ = 0.1f;

    [Header("Quantidade no grid (Largura x Altura x Profundidade)")]
    public int countWidth = 3;
    public int countHeight = 2;
    public int countDepth = 3;

    [Header("Transform do item dentro do slot")]
    public Vector3 itemScale = Vector3.one;
    public Vector3 itemRotationEuler;

    public int MaxCapacity =>
        Mathf.Max(0, countWidth) * Mathf.Max(0, countHeight) * Mathf.Max(0, countDepth);

    // Gera as posições locais dos slots, em ordem "camada por camada"
    // (enche a base largura x profundidade antes de subir de altura).
    public Vector3[] GenerateLocalPositions()
    {
        int capacity = MaxCapacity;
        Vector3[] positions = new Vector3[capacity];

        int index = 0;
        for (int h = 0; h < countHeight; h++)
        {
            for (int d = 0; d < countDepth; d++)
            {
                for (int w = 0; w < countWidth; w++)
                {
                    positions[index] = originOffset + new Vector3(w * spacingX, h * spacingY, d * spacingZ);
                    index++;
                }
            }
        }

        return positions;
    }
}

[CreateAssetMenu(fileName = "All Things", menuName = "Scriptable Objects/Create Thing")]
public class AllIThingsData : ScriptableObject
{
    public string itemName, description;
    public float singleItemPrice;
    public Items itemType;
    public GameObject itemPrefab;
    public GameObject itemBoxPrefab;
    public Sprite itemSprite;
    public float marketPrice;

    [Header("Grid procedural - Caixa")]
    public ItemGridConfig boxGrid;

    [Header("Grid procedural - Prateleira")]
    public ItemGridConfig shelfGrid;
}
