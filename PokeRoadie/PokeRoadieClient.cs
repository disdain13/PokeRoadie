#region " Imports "

using System;
using System.Linq;
using System.IO;
using System.Reflection;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI;
using POGOProtos.Networking.Envelopes;
using System.Threading.Tasks;
using PokeRoadie.Utils;

#endregion

namespace PokeRoadie
{
    public class PokeRoadieClient : Client
    {

        #region " Shadowed Properties "

        new public PokeRoadieSettings Settings { get { return (PokeRoadieSettings)base.Settings; } }

        #endregion
        #region " Constructors "

        public PokeRoadieClient(PokeRoadieSettings settings, ApiFailureStrategy apiFailureStrategy) 
            : base(settings, apiFailureStrategy)
        {
        }

        #endregion

    }
}
