using System;
using UnityEngine;

namespace JapanMarket.Core
{
    /// <summary>
    /// Marca um campo serializado como somente-leitura no Inspector.
    /// Usado em identidades geradas (ProductId, FurnitureId) que nunca devem
    /// ser editadas à mão: mudar um id em produção invalida saves e telemetria.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class ReadOnlyFieldAttribute : PropertyAttribute
    {
    }
}
