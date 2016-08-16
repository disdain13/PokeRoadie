#region " Imports "


using POGOProtos.Data;


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
