#region

using PokemonGo.RocketAPI.Enums;
using System.Collections.Generic;
using PokemonGo.RocketAPI.GeneratedCode;

#endregion


namespace PokemonGo.RocketAPI
{
    public interface ISettings
    {
        AuthType AuthType { get; set; }
        string PtcPassword { get; set; }
        string PtcUsername { get; set; }
        double DefaultLatitude { get; set; }
        double DefaultLongitude { get; set; }
        double DefaultAltitude { get; set; }
        bool UseGPXPathing { get; set; }
        string GPXFile { get; set; }
        double WalkingSpeedInKilometerPerHour { get; set; }
        int MaxTravelDistanceInMeters { get; set; }
        bool UsePokemonToNotCatchList { get; set; }
        bool EvolvePokemon { get; set; }
        bool EvolveOnlyPokemonAboveIV { get; set; }
        float EvolveOnlyPokemonAboveIVValue { get; set; }
        bool TransferPokemon { get; set; }
        int TransferPokemonKeepDuplicateAmount { get; set; }
        bool NotTransferPokemonsThatCanEvolve { get; set; }
        float KeepMinIVPercentage { get; set; }
        int KeepMinCP { get; set; }
        bool PrioritizeIVOverCP { get; set; }
        bool useLuckyEggsWhileEvolving { get; set; }
        bool LoiteringActive { get; set; }
        int MinutesPerDestination { get; set; }
        int FlyingSpeedInKilometerPerHour { get; set; }
        bool CatchWhileFlying { get; set; }
        bool FlyingEnabled { get; set; }
        bool MoveWhenNoStops { get; set; }
        bool PrioritizeStopsWithLures { get; set; }
        bool DestinationsEnabled { get; set; }
        int DestinationIndex { get; set; }
        int DisplayRefreshMinutes { get; set; }
        bool DisplayAllPokemonInLog { get; set; }
        bool EnableSpeedAdjustment { get; set; }
        bool EnableSpeedRandomizer { get; set; }
        bool CatchPokemon { get; set; }
        int WalkingSpeedInKilometerPerHourMax { get; set; }
        int MaxSecondsBetweenStops { get; set; }

        ICollection<KeyValuePair<ItemId, int>> ItemRecycleFilter { get; }
        ICollection<PokemonId> PokemonsToEvolve { get; }
        ICollection<PokemonId> PokemonsNotToTransfer { get; }
        ICollection<PokemonId> PokemonsNotToCatch { get; }

        void SetDefaultLocation(double lat, double lng, double z);
        void Save();
        void Load();
    }
}