using UnityEngine;

[System.Serializable]
public class ItemGridSettings
{
    [Tooltip("Quantidade de itens por eixo (X = largura, Y = altura, Z = profundidade)")]
    public Vector3Int count = Vector3Int.one;

    [Tooltip("Espaçamento independente por eixo entre os centros dos itens")]
    public Vector3 spacing = Vector3.zero;

    [Tooltip("Deslocamento inicial a partir da origem")]
    public Vector3 originOffset = Vector3.zero;

    [Tooltip("Rotação do item no slot (em ângulos de Euler)")]
    public Vector3 itemRotation = Vector3.zero;

    [Tooltip("Escala local do item quando posicionado")]
    public Vector3 itemScale = Vector3.one;

    /// <summary>
    /// Capacidade total de itens calculada com segurança (retorna 0 se qualquer eixo for menor ou igual a zero).
    /// </summary>
    public int TotalCapacity => (count.x > 0 && count.y > 0 && count.z > 0) ? count.x * count.y * count.z : 0;

    /// <summary>
    /// Retorna a posição local para um determinado índice no grid, preenchendo camada por camada (base X x Z antes de subir em Y).
    /// </summary>
    public Vector3 GetLocalPosition(int index)
    {
        int baseCapacity = count.x * count.z;
        if (baseCapacity <= 0 || count.y <= 0 || index < 0)
            return originOffset;

        int y = index / baseCapacity;
        int remainder = index % baseCapacity;
        int z = remainder / count.x;
        int x = remainder % count.x;

        return originOffset + new Vector3(x * spacing.x, y * spacing.y, z * spacing.z);
    }

    /// <summary>
    /// Retorna a rotação local configurada para os itens.
    /// </summary>
    public Quaternion GetLocalRotation()
    {
        return Quaternion.Euler(itemRotation);
    }
}
