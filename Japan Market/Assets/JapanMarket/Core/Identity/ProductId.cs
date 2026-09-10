using System;
using UnityEngine;

namespace JapanMarket.Core
{
    /// <summary>
    /// Identidade estável de um produto.
    ///
    /// Existe por UM motivo: save, telemetria e qualquer lugar onde a referência
    /// direta ao asset não pode ser guardada. Em código de runtime, refira-se ao
    /// <c>ItemDefinition</c> diretamente — a referência de asset é imune a rename,
    /// reordenação e remoção, ao contrário do <c>enum Items</c> que ela substitui.
    ///
    /// O valor é gerado uma única vez, no momento em que o asset é criado, e nunca
    /// muda. É por isso que o campo é <see cref="ReadOnlyFieldAttribute"/>.
    /// </summary>
    [Serializable]
    public struct ProductId : IEquatable<ProductId>, IComparable<ProductId>
    {
        [SerializeField] private string _value;

        public static readonly ProductId None = default;

        private ProductId(string value) => _value = value;

        /// <summary>Gera um id novo. Use apenas no editor, ao criar a definição.</summary>
        public static ProductId Generate() => new(Guid.NewGuid().ToString("N"));

        /// <summary>Reconstrói um id a partir de um save. Não valida conteúdo.</summary>
        public static ProductId FromString(string value) =>
            string.IsNullOrWhiteSpace(value) ? None : new ProductId(value);

        public string Value => _value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_value);

        public bool Equals(ProductId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ProductId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(ProductId other) => string.CompareOrdinal(Value, other.Value);

        public static bool operator ==(ProductId a, ProductId b) => a.Equals(b);
        public static bool operator !=(ProductId a, ProductId b) => !a.Equals(b);

        public override string ToString() => IsValid ? _value : "<none>";
    }
}
