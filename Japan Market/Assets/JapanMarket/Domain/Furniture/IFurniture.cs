using JapanMarket.Core;
using JapanMarket.Data;
using UnityEngine;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Um móvel colocado na loja, visto de fora.
    ///
    /// Domain fala com móveis através desta interface, nunca através do
    /// MonoBehaviour que a implementa — é o que mantém a camada testável e o que
    /// permite substituir um móvel por um fake num teste de fila.
    /// </summary>
    public interface IFurniture
    {
        FurnitureId Id { get; }
        FurnitureDefinition Definition { get; }

        Vector3 Position { get; }
        Quaternion Rotation { get; }

        /// <summary>
        /// False depois que o móvel foi destruído ou desativado.
        ///
        /// Existe porque um objeto Unity destruído continua sendo uma referência
        /// C# não-nula: quem guardou o móvel precisa de uma forma barata de
        /// perguntar "isso ainda existe?" sem depender do operador == sobrecarregado
        /// (que some assim que a referência é tipada como interface).
        /// </summary>
        bool IsAlive { get; }

        bool TryGetCapability<T>(out T capability) where T : class, IFurnitureCapability;
        bool HasCapability<T>() where T : class, IFurnitureCapability;
    }
}
