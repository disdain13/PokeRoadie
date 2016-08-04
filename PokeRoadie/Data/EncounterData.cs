#region " Imports "

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using PokemonGo.RocketAPI;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.Helpers;
using PokemonGo.RocketAPI.Logging;
using PokemonGo.RocketAPI.Exceptions;

using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Responses;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Map.Fort;
using POGOProtos.Map.Pokemon;

using PokeRoadie.Extensions;
using PokeRoadie.Utils;

#endregion

namespace PokeRoadie
{
    public class EncounterData
    {
        public LocationData Location { get; set; }
        public ulong EncounterId { get; set; }
        public PokemonData PokemonData { get; set; }
        public float? Probability { get; set; }
        public string SpawnPointId { get; set; }
        public EncounterSourceTypes Source { get; set; }

        public EncounterData(LocationData location, ulong encounterId, PokemonData pokemonData, float? probability, string spawnPointId, EncounterSourceTypes source)
        {
            Location = location;
            EncounterId = encounterId;
            PokemonData = pokemonData;
            Probability = probability;
            SpawnPointId = spawnPointId;
            Source = source;


        }
    }
}
