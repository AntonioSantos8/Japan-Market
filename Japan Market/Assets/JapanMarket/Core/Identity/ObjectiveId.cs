using System;
using UnityEngine;

namespace JapanMarket.Core
{
    /// <summary>
    /// Identidade estável de um objetivo. Mesmo contrato do
    /// <see cref="ProductId"/>, e existe pelo mesmo motivo: o save guarda "o
    /// objetivo X está em 3 de 10", e guardar isso pelo NOME do asset faz um
    /// rename apagar o progresso do jogador em silêncio.
    ///
    /// Gerado uma vez, na criação do asset, e nunca mais alterado.
    /// </summary>
    [Serializable]
    public struct ObjectiveId : IEquatable<ObjectiveId>, IComparable<ObjectiveId>
    {
        [SerializeField] private string _value;

        public static readonly ObjectiveId None = default;

        private ObjectiveId(string value) => _value = value;

        /// <summary>Gera um id novo. Use apenas no editor, ao criar a definição.</summary>
        public static ObjectiveId Generate() => new(Guid.NewGuid().ToString("N"));

        /// <summary>Reconstrói um id a partir de um save. Não valida conteúdo.</summary>
        public static ObjectiveId FromString(string value) =>
            string.IsNullOrWhiteSpace(value) ? None : new ObjectiveId(value);

        public string Value => _value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_value);

        public bool Equals(ObjectiveId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ObjectiveId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(ObjectiveId other) => string.CompareOrdinal(Value, other.Value);

        public static bool operator ==(ObjectiveId a, ObjectiveId b) => a.Equals(b);
        public static bool operator !=(ObjectiveId a, ObjectiveId b) => !a.Equals(b);

        public override string ToString() => IsValid ? _value : "<none>";
    }
}
