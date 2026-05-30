using System;

namespace LegendsOfWarAndMagic.Game.World.Domain
{
    public readonly struct FactionId : IEquatable<FactionId>
    {
        public FactionId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public bool Equals(FactionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is FactionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }
    }
}
