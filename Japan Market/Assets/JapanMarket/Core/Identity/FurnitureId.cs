using System;
using UnityEngine;

namespace JapanMarket.Core
{

    [Serializable]
    public struct FurnitureId : IEquatable<FurnitureId>, IComparable<FurnitureId>
    {
        [SerializeField] private string _value;

        public static readonly FurnitureId None = default;

        private FurnitureId(string value) => _value = value;

        public static FurnitureId Generate() => new(Guid.NewGuid().ToString("N"));

        public static FurnitureId FromString(string value) =>
            string.IsNullOrWhiteSpace(value) ? None : new FurnitureId(value);

        public string Value => _value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_value);

        public bool Equals(FurnitureId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is FurnitureId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(FurnitureId other) => string.CompareOrdinal(Value, other.Value);

        public static bool operator ==(FurnitureId a, FurnitureId b) => a.Equals(b);
        public static bool operator !=(FurnitureId a, FurnitureId b) => !a.Equals(b);

        public override string ToString() => IsValid ? _value : "<none>";
    }
}
