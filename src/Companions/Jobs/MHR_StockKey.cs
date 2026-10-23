using System;

namespace Companions
{
    // Fase 3.19 (auditoria): the key of the "how much of this is around" caches (MHR_GatherRules.Stock and
    // MHR_SeedRules.CountNear). The total they compute includes the companion's OWN pack, so a cache keyed by the
    // item alone answered with one companion's total for another one: companion A saw 6 wood (5 in its own pack) and
    // companion B, who had none, thought it could stop. Keyed by (companion, item), each one gets its own number.
    internal struct MHR_StockKey : IEquatable<MHR_StockKey>
    {
        internal int Companion;

        internal string Item;

        internal MHR_StockKey(int companion, string item)
        {
            Companion = companion;
            Item = item;
        }

        public bool Equals(MHR_StockKey other)
        {
            return Companion == other.Companion && string.Equals(Item, other.Item, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MHR_StockKey && Equals((MHR_StockKey)obj);
        }

        public override int GetHashCode()
        {
            int hash = Companion * 397;
            return Item != null ? hash ^ Item.GetHashCode() : hash;
        }
    }
}
