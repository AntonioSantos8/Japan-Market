using System;
using System.Globalization;
using UnityEngine;

namespace JapanMarket.Core
{

    [Serializable]
    public struct Money : IEquatable<Money>, IComparable<Money>, IFormattable
    {
        public const string Symbol = "¥";

        [SerializeField] private long _yen;

        private Money(long yen) => _yen = yen;

        public static readonly Money Zero = default;

        public static Money FromYen(long yen) => new(yen);

        public static Money FromYen(double yen) =>
            new((long)Math.Round(yen, MidpointRounding.AwayFromZero));

        public long Yen => _yen;
        public bool IsZero => _yen == 0;
        public bool IsPositive => _yen > 0;
        public bool IsNegative => _yen < 0;
        public Money Abs => new(Math.Abs(_yen));

        public static Money operator +(Money a, Money b) => new(a._yen + b._yen);
        public static Money operator -(Money a, Money b) => new(a._yen - b._yen);
        public static Money operator -(Money a) => new(-a._yen);
        public static Money operator *(Money a, int factor) => new(a._yen * factor);
        public static Money operator *(int factor, Money a) => new(a._yen * factor);
        public static Money operator *(Money a, float factor) => FromYen(a._yen * (double)factor);
        public static Money operator *(float factor, Money a) => FromYen(a._yen * (double)factor);

        public static float Ratio(Money numerator, Money denominator) =>
            denominator._yen == 0 ? 0f : (float)((double)numerator._yen / denominator._yen);

        public static Money Max(Money a, Money b) => a._yen >= b._yen ? a : b;
        public static Money Min(Money a, Money b) => a._yen <= b._yen ? a : b;
        public static Money Clamp(Money v, Money min, Money max) => Max(min, Min(max, v));

        public bool Equals(Money other) => _yen == other._yen;
        public override bool Equals(object obj) => obj is Money other && Equals(other);
        public override int GetHashCode() => _yen.GetHashCode();
        public int CompareTo(Money other) => _yen.CompareTo(other._yen);

        public static bool operator ==(Money a, Money b) => a._yen == b._yen;
        public static bool operator !=(Money a, Money b) => a._yen != b._yen;
        public static bool operator <(Money a, Money b) => a._yen < b._yen;
        public static bool operator >(Money a, Money b) => a._yen > b._yen;
        public static bool operator <=(Money a, Money b) => a._yen <= b._yen;
        public static bool operator >=(Money a, Money b) => a._yen >= b._yen;

        public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

        public string ToString(string format, IFormatProvider provider)
        {
            provider ??= CultureInfo.InvariantCulture;
            switch (string.IsNullOrEmpty(format) ? "G" : format.ToUpperInvariant())
            {
                case "N": return _yen.ToString("N0", provider);
                case "K": return ToCompactString(provider);
                default:  return Symbol + _yen.ToString("N0", provider);
            }
        }

        public string ToCompactString(IFormatProvider provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;

            long abs = Math.Abs(_yen);
            string sign = _yen < 0 ? "-" : string.Empty;

            if (abs >= 1_000_000_000L)
                return sign + Symbol + (abs / 1_000_000_000d).ToString("0.##", provider) + "b";
            if (abs >= 1_000_000L)
                return sign + Symbol + (abs / 1_000_000d).ToString("0.##", provider) + "m";
            if (abs >= 1_000L)
                return sign + Symbol + (abs / 1_000d).ToString("0.##", provider) + "k";

            return sign + Symbol + abs.ToString(provider);
        }

        public static bool TryParse(string text, out Money money)
        {
            money = Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var digits = new System.Text.StringBuilder(text.Length);
            bool negative = false;

            foreach (char c in text)
            {
                if (c == '-' && digits.Length == 0) { negative = true; continue; }
                if (char.IsDigit(c)) { digits.Append(c); continue; }
                if (c == ',' || c == '.' || c == ' ' || c == '\u00A5') continue;
                return false;
            }

            if (digits.Length == 0) return false;
            if (!long.TryParse(digits.ToString(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out long parsed)) return false;

            money = new Money(negative ? -parsed : parsed);
            return true;
        }
    }
}
