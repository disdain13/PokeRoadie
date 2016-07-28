#region

using PokemonGo.RocketAPI.Enums;
using System.Collections.Generic;
using PokemonGo.RocketAPI.GeneratedCode;

#endregion


namespace PokemonGo.RocketAPI
{
    public interface ISettings
    {

        #region " Properties "

        AuthType AuthType { get; set; }
        string PtcPassword { get; set; }
        string PtcUsername { get; set; }
        double CurrentLatitude { get; set; }
        double CurrentLongitude { get; set; }
        double CurrentAltitude { get; set; }
        bool UseGPXPathing { get; set; }
        string GPXFile { get; set; }
        double MinSpeed { get; set; }
        int MaxDistance { get; set; }
        bool UsePokemonToNotCatchList { get; set; }
        bool EvolvePokemon { get; set; }
        bool EvolveOnlyPokemonAboveIV { get; set; }
        double EvolveOnlyPokemonAboveIVValue { get; set; }
        bool TransferPokemon { get; set; }
        int KeepDuplicateAmount { get; set; }
        bool NotTransferPokemonsThatCanEvolve { get; set; }
        double KeepAboveIV { get; set; }
        double KeepAboveV { get; set; }
        int KeepAboveCP { get; set; }
        double TransferBelowIV { get; set; }
        double TransferBelowV { get; set; }
        int TransferBelowCP { get; set; }
        bool UseLuckyEggs { get; set; }
        bool LoiteringActive { get; set; }
        int MinutesPerDestination { get; set; }
        int FlyingSpeed { get; set; }
        bool CatchWhileFlying { get; set; }
        bool FlyingEnabled { get; set; }
        bool MoveWhenNoStops { get; set; }
        bool PrioritizeStopsWithLures { get; set; }
        bool DestinationsEnabled { get; set; }
        int DestinationIndex { get; set; }
        int DisplayRefreshMinutes { get; set; }
        bool DisplayAllPokemonInLog { get; set; }
        bool DisplayAggregateLog { get; set; }
        bool EnableSpeedAdjustment { get; set; }
        bool EnableSpeedRandomizer { get; set; }
        bool CatchPokemon { get; set; }
        int MaxSpeed { get; set; }
        int MaxSecondsBetweenStops { get; set; }
        PriorityType PriorityType { get; set; }

        ICollection<KeyValuePair<ItemId, int>> ItemRecycleFilter { get; }
        ICollection<PokemonId> PokemonsToEvolve { get; }
        ICollection<PokemonId> PokemonsNotToTransfer { get; }
        ICollection<PokemonId> PokemonsNotToCatch { get; }
        ICollection<PokemonMoveDetail> PokemonMoveDetails { get; }

        #endregion
        #region " Methods "

        void SetDefaultLocation(double lat, double lng, double z);
        void Save();
        void Load();

        #endregion

    }
}