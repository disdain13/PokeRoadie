#region " Imports "

using System;

using PokemonGo.RocketAPI;

#endregion

namespace PokeRoadie
{
    public class PokeRoadieClient : Client
    {

        public DateTime? RefreshEndDate { get; set; }
        public PokeRoadieClient() : base(PokeRoadieSettings.Current)
        {
        }

    }
}
