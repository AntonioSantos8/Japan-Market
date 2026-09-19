using System;
using UnityEngine;

namespace JapanMarket.Core
{

    [Serializable]
    public struct ProductId : IEquatable<ProductId>, IComparable<ProductId>
    {
        [SerializeField] private string _value;

        public static readonly ProductId None = default;

        private ProductId(string value) => _value = value;

        public static ProductId Generate() => new(Guid.NewGuid().ToString("N"));

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
