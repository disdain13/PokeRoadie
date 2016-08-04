using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeRoadie
{
    [Serializable]
    public class IncubatorData : IEquatable<IncubatorData>
    {
        public string IncubatorId { get; set; }
        public ulong PokemonId { get; set; }

        public bool Equals(IncubatorData other)
        {
            return other != null && other.IncubatorId == IncubatorId && other.PokemonId == PokemonId;
        }
    }
}
