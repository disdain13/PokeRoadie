#region " Imports "

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;

using PokeRoadie.Api;
using PokeRoadie.Api.Enums;
using PokeRoadie.Api.Extensions;
using PokeRoadie.Api.Helpers;
using PokeRoadie.Api.Logging;
using PokeRoadie.Api.Exceptions;
using PokeRoadie.Api.Rpc;

using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Responses;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Map.Fort;
using POGOProtos.Data.Player;
using POGOProtos.Map.Pokemon;
using POGOProtos.Data.Capture;

using PokeRoadie.Extensions;
using PokeRoadie.Utils;
//using PokeRoadie.Logging;
using System.ComponentModel;

#endregion

namespace PokeRoadie
{
    public class PokeRoadieLogic
    {
        #region " Events "

        //system events
        public event Func<bool> OnPromptForCredentials;
        //public event Func<bool> OnPromptForCoords;

        //encounter events
        public event Action<EncounterData> OnEncounter;
        public event Action<EncounterData, CatchPokemonResponse> OnCatchAttempt;
        public event Action<EncounterData, CatchPokemonResponse> OnCatch;

        //geo events
        public event Action<LocationData, int> OnChangeDestination;
        public event Action<LocationData> OnChangeWaypoint;
        public event Action<LocationData> OnChangeLocation;

        //fort events
        public event Action<LocationData, List<FortData>> OnVisitForts;
        public event Action<LocationData, CollectDailyDefenderBonusResponse> OnPickupDailyDefenderBonus;
        //pokestop events
        public event Action<LocationData, List<FortData>> OnGetAllNearbyPokestops;
        public event Action<LocationData, FortDetailsResponse> OnTravelingToPokestop;
        public event Action<LocationData, FortDetailsResponse, FortSearchResponse> OnVisitPokestop;

        //gym events
        public event Action<LocationData, List<FortData>> OnGetAllNearbyGyms;
        public event Action<LocationData, FortDetailsResponse> OnTravelingToGym;
        public event Action<LocationData, FortDetailsResponse, GetGymDetailsResponse> OnVisitGym;
        public event Action<LocationData, GetGymDetailsResponse, PokemonData> OnDeployToGym;

        //pokemon events
        public event Action<PokemonData> OnEvolve;
        public event Action<PokemonData> OnPowerUp;
        public event Action<PokemonData> OnTransfer;
        public event Action<PokemonData> OnFavorite;

        //inventory events
        public event Action<ItemId, int> OnRecycleItems;
        public event Action OnLuckyEggActive;
        public event Action OnIncenseActive;
        public event Action<ItemId, PokemonData> OnUsePotion;
        public event Action<ItemId, PokemonData> OnUseRevive;
        public event Action<IncubatorData, PokemonData> OnEggHatched;
        public event Action<EggIncubator> OnUseIncubator;
        public event Action<EggIncubator> OnIncubatorStatus;

        //used to raise syncronized events
        private bool RaiseSyncEvent(Delegate method, params object[] args)
        {
            if (method == null || Context.Invoker == null || !Context.Invoker.InvokeRequired) return false;
            Context.Invoker.Invoke(method, args);
            return true;
        }

        #endregion
        #region " Static Members "

        private object xloLock = new object();
        private int xloCount = 0;
        private volatile bool isRunning;
        //private volatile bool inFlight = false;


        #endregion
        #region " Timers "

        private DateTime? _nextLuckyEggTime;
        private DateTime? _nextIncenseTime;
        private DateTime? _nextExportTime;
        public DateTime? _nextWriteStatsTime;
        private DateTime? fleeEndTime;
        private DateTime? fleeStartTime;
        private DateTime noWorkTimer = DateTime.Now;
        private DateTime mapsTimer = DateTime.Now;
        private DateTime? nextTransEvoPowTime;

        #endregion
        #region " Counter "

        private int recycleCounter = 0;
        private int fleeCounter = 0;

        #endregion
        #region " Members "

        private GetPlayerResponse _playerProfile;
        private GetMapObjectsResponse _map = null;

        private bool IsInitialized = false;
        private bool softBan = false;
        private bool hasDisplayedConfigSettings;
        private List<string> gymTries = new List<string>();
        private Random Random = new Random(DateTime.Now.Millisecond);
        private ulong lastMissedEncounterId = 0;
        private int locationAttemptCount = 0;
        private List<TutorialState> tutorialAttempts = new List<TutorialState>();
        private List<ulong> _recentEncounters = new List<ulong>();

        public static volatile bool NeedsNewLogin = false;
        public static volatile bool IsTravelingLongDistance;

        #endregion
        #region " Properties "
        public Context Context { get; set; }
        public bool CanCatch { get { return Context.Settings.CatchPokemon && Context.Session.Current.CatchEnabled && !softBan && Context.Navigation.LastKnownSpeed <= Context.Settings.MaxCatchSpeed && noWorkTimer <= DateTime.Now; } }
        public bool CanVisit { get { return Context.Settings.VisitPokestops && Context.Session.Current.VisitEnabled && !softBan; } }
        public bool CanVisitGyms { get { return Context.Settings.VisitGyms && Context.Statistics.Currentlevel > 4 && !softBan; } }

        public async Task<GetMapObjectsResponse> GetMapObjects(bool force = false)
        {
            if (force || _map == null || mapsTimer <= DateTime.Now)
            {
                var objects = await Context.Client.Map.GetMapObjects();
                //if (Context.Settings.ShowDebugMessages) Logger.Write("Map objects pull made from server", LogLevel.Debug);
                if (objects != null && objects.Item1 != null)
                {
                    mapsTimer = DateTime.Now.AddMilliseconds(Random.Next(1600, 2300));
                    _map = objects.Item1;
                }
                else
                    mapsTimer = DateTime.Now.AddMilliseconds(5000);
            }
            return _map;
        }

        #endregion
        #region " Constructors "

        public PokeRoadieLogic(Context context) : base()
        {
            Context = context;
            Context.Navigation.OnChangeLocation += RelayLocation;
        }
        public PokeRoadieLogic(Context context, ISynchronizeInvoke form) : this(context)
        {
            Context.Invoker = form;
        }

        #endregion
        #region " Application Methods "

        public void Stop()
        {
            isRunning = false;
        }

        public async Task CloseApplication(int exitCode)
        {
            Logger.Write($"PokeRoadie will be closed in 15 seconds...", LogLevel.Warning);
            for (int i = 0; i < 15; i++)
            {
                await Task.Delay(1000);
                Logger.Append(".");
            }
            Program.ExitApplication(exitCode);
        }

        #endregion
        #region " Maintenance/Utility Methods "
        private async Task Export()
        {
            if (!_nextExportTime.HasValue || _nextExportTime.Value < DateTime.Now)
            {
                _nextExportTime = DateTime.Now.AddMinutes(5);
                await Context.Inventory.ExportPokemonToCSV(_playerProfile.PlayerData);
            }
        }
        private async Task WriteStats()
        {
            if (!_nextWriteStatsTime.HasValue || _nextWriteStatsTime.Value <= DateTime.Now)
            {
                await PokeRoadieInventory.GetCachedInventory(Context.Client);
                _playerProfile = await Context.Client.Player.GetPlayer();
                var playerName = Context.Statistics.GetUsername(Context.Client, _playerProfile);
                Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
                Context.Statistics.SetStardust(_playerProfile.PlayerData.Currencies.ToArray()[1].Amount);
                var currentLevelInfos = await Context.Statistics._getcurrentLevelInfos();
                //get all ordered by id, then cp
                var allPokemon = (await Context.Inventory.GetPokemons()).OrderBy(x => x.PokemonId).ThenByDescending(x => x.Cp).ToList();
                var deployedPokemon = allPokemon.Where(x => !string.IsNullOrEmpty(x.DeployedFortId)).ToList();
                Logger.Write("====== User Info ======", LogLevel.None, ConsoleColor.Yellow);
                Logger.Write($"Name: {playerName}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Team: {_playerProfile.PlayerData.Team}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Level: {currentLevelInfos}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Pokemon: {allPokemon.Count}", LogLevel.None, ConsoleColor.White);
                Logger.Write("====== Deployment Summary ======", LogLevel.None, ConsoleColor.Yellow);
                Logger.Write($"Deployed: {deployedPokemon.Count}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Min Needed: {Context.Settings.MinGymsBeforeBonusPickup}", LogLevel.None, ConsoleColor.White);
                if (_playerProfile.PlayerData.DailyBonus.NextDefenderBonusCollectTimestampMs < DateTime.UtcNow.ToUnixTime())
                {
                    Logger.Write($"Time to Bonus: Available Now!", LogLevel.None, ConsoleColor.White);
                }
                else
                {
                    TimeSpan timeToBonus = TimeSpan.FromMilliseconds(_playerProfile.PlayerData.DailyBonus.NextDefenderBonusCollectTimestampMs - DateTime.UtcNow.ToUnixTime());
                    Logger.Write($"Time to Bonus: {timeToBonus.ToString(@"hh\:mm\:ss")}", LogLevel.None, ConsoleColor.White);
                }
                if (Client.Proxy != null)
                {
                    var host = Client.Proxy.Address.ToString();
                    Logger.Write("====== Proxy Info ======", LogLevel.None, ConsoleColor.Yellow);
                    Logger.Write($"Address: {Client.Proxy.Address}", LogLevel.None, ConsoleColor.White);
                }
                Logger.Write("====== Currencies ======", LogLevel.None, ConsoleColor.Yellow);
                if (_playerProfile.PlayerData.Currencies.Any())
                {
                    foreach (var entry in _playerProfile.PlayerData.Currencies)
                    {
                        Logger.Write($"{entry.Name}: {entry.Amount}", LogLevel.None, ConsoleColor.White);
                    }
                }
                if (Context.Settings.ShowDebugMessages)
                {
                    Logger.Write("(DEBUG) ====== Tutorial States ======", LogLevel.None, ConsoleColor.Yellow);
                    if (_playerProfile.PlayerData.TutorialState.Any())
                    {
                        foreach (var entry in _playerProfile.PlayerData.TutorialState)
                        {
                            Logger.Write($"{entry}", LogLevel.Debug);
                        }
                    }
                }
                var items = await Context.Inventory.GetItems();
                Logger.Write($"====== Items ({items.Select(x => x.Count).Sum()}) ======", LogLevel.None, ConsoleColor.Yellow);
                var pokeBalls = items.Where(x => x.ItemId == ItemId.ItemPokeBall).FirstOrDefault();
                var pokeBallsCount = pokeBalls == null ? 0 : pokeBalls.Count;
                var greatBalls = items.Where(x => x.ItemId == ItemId.ItemGreatBall).FirstOrDefault();
                var greatBallsCount = greatBalls == null ? 0 : pokeBalls.Count;
                var ultraBalls = items.Where(x => x.ItemId == ItemId.ItemUltraBall).FirstOrDefault();
                var ultraBallsCount = ultraBalls == null ? 0 : pokeBalls.Count;
                var masterBalls = items.Where(x => x.ItemId == ItemId.ItemMasterBall).FirstOrDefault();
                var masterBallsCount = masterBalls == null ? 0 : pokeBalls.Count;
                var potions = items.Where(x => x.ItemId == ItemId.ItemPotion).FirstOrDefault();
                var potionsCount = potions == null ? 0 : potions.Count;
                var superPotions = items.Where(x => x.ItemId == ItemId.ItemSuperPotion).FirstOrDefault();
                var superPotionsCount = superPotions == null ? 0 : superPotions.Count;
                var hyperPotions = items.Where(x => x.ItemId == ItemId.ItemHyperPotion).FirstOrDefault();
                var hyperPotionsCount = hyperPotions == null ? 0 : hyperPotions.Count;
                var maxPotions = items.Where(x => x.ItemId == ItemId.ItemMaxPotion).FirstOrDefault();
                var maxPotionsCount = maxPotions == null ? 0 : maxPotions.Count;
                var revives = items.Where(x => x.ItemId == ItemId.ItemRevive).FirstOrDefault();
                var revivesCount = revives == null ? 0 : revives.Count;
                var maxRevives = items.Where(x => x.ItemId == ItemId.ItemMaxRevive).FirstOrDefault();
                var maxRevivesCount = maxRevives == null ? 0 : maxRevives.Count;
                var luckyEggs = items.Where(x => x.ItemId == ItemId.ItemLuckyEgg).FirstOrDefault();
                var luckyEggsCount = luckyEggs == null ? 0 : luckyEggs.Count;
                var incenseOrdinarys = items.Where(x => x.ItemId == ItemId.ItemIncenseOrdinary).FirstOrDefault();
                var incenseOrdinarysCount = incenseOrdinarys == null ? 0 : incenseOrdinarys.Count;
                //var incenseSpicys = items.Where(x => x.ItemId == ItemId.ItemIncenseSpicy).FirstOrDefault();
                //var incenseSpicysCount = incenseSpicys == null ? 0 : incenseSpicys.Count;
                //var incenseCools = items.Where(x => x.ItemId == ItemId.ItemIncenseCool).FirstOrDefault();
                //var incenseCoolsCount = incenseCools == null ? 0 : incenseCools.Count;
                //var incenseFlorals = items.Where(x => x.ItemId == ItemId.ItemIncenseFloral).FirstOrDefault();
                //var incenseFloralsCount = incenseFlorals == null ? 0 : incenseFlorals.Count;
                var troyDisks = items.Where(x => x.ItemId == ItemId.ItemTroyDisk).FirstOrDefault();
                var troyDisksCount = troyDisks == null ? 0 : troyDisks.Count;
                var razzBerries = items.Where(x => x.ItemId == ItemId.ItemRazzBerry).FirstOrDefault();
                var razzBerriesCount = razzBerries == null ? 0 : razzBerries.Count;
                //var blukBerries = items.Where(x => x.ItemId == ItemId.ItemBlukBerry).FirstOrDefault();
                //var blukBerriesCount = blukBerries == null ? 0 : blukBerries.Count;
                //var nanabBerries = items.Where(x => x.ItemId == ItemId.ItemNanabBerry).FirstOrDefault();
                //var nanabBerriesCount = nanabBerries == null ? 0 : nanabBerries.Count;
                //var weparBerries = items.Where(x => x.ItemId == ItemId.ItemWeparBerry).FirstOrDefault();
                //var weparBerriesCount = weparBerries == null ? 0 : weparBerries.Count;
                //var pinapBerries = items.Where(x => x.ItemId == ItemId.ItemPinapBerry).FirstOrDefault();
                //var pinapBerriesCount = pinapBerries == null ? 0 : pinapBerries.Count;
                var incubatorBasicUnlimiteds = items.Where(x => x.ItemId == ItemId.ItemIncubatorBasicUnlimited).FirstOrDefault();
                var incubatorBasicUnlimitedsCount = incubatorBasicUnlimiteds == null ? 0 : incubatorBasicUnlimiteds.Count;
                var incubatorBasics = items.Where(x => x.ItemId == ItemId.ItemIncubatorBasic).FirstOrDefault();
                var incubatorBasicsCount = incubatorBasics == null ? 0 : incubatorBasics.Count;
                Logger.Write($"{"Balls:".PadRight(11)} Poke x {pokeBallsCount} | Great x {greatBallsCount} | Ultra x {ultraBallsCount} | Master x {masterBallsCount}".Replace("Item", ""), LogLevel.None, ConsoleColor.White);
                Logger.Write($"{"Potions:".PadRight(11)} Potion x {potionsCount} | Super x {superPotionsCount} | Hyper x {hyperPotionsCount} | Max x {maxPotionsCount}".Replace("Item", ""), LogLevel.None, ConsoleColor.White);
                Logger.Write($"{"Revives:".PadRight(11)} Revive x {revivesCount} | Max x {maxRevivesCount}".Replace("Item", ""), LogLevel.None, ConsoleColor.White);
                Logger.Write($"{"Power-ups:".PadRight(11)} Lucky Egg x {luckyEggsCount} | Incense x {incenseOrdinarysCount} | Lure x {troyDisksCount} | Berry x {razzBerriesCount}".Replace("Item", ""), LogLevel.None, ConsoleColor.White);
                Logger.Write($"Incubators: Unlimited x {incubatorBasicUnlimitedsCount} | Basic x {incubatorBasicsCount}", LogLevel.None, ConsoleColor.White);
                if (!hasDisplayedConfigSettings)
                {
                    hasDisplayedConfigSettings = true;
                    //write transfer settings
                    if (Context.Settings.TransferPokemon)
                    {
                        Logger.Write("====== Transfer Settings ======", LogLevel.None, ConsoleColor.Yellow);
                        Logger.Write($"{("Keep Above CP:").PadRight(25)}{Context.Settings.KeepAboveCP}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Keep Above IV:").PadRight(25)}{Context.Settings.KeepAboveIV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Keep Above LV:").PadRight(25)}{Context.Settings.KeepAboveLV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Keep Above V:").PadRight(25)}{Context.Settings.KeepAboveV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Transfer Below CP:").PadRight(25)}{Context.Settings.AlwaysTransferBelowCp}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Transfer Below IV:").PadRight(25)}{Context.Settings.AlwaysTransferBelowIV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Transfer Below LV:").PadRight(25)}{Context.Settings.AlwaysTransferBelowLV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Transfer Below V:").PadRight(25)}{Context.Settings.AlwaysTransferBelowV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Transfer Evolvable:").PadRight(25)}{!Context.Settings.NotTransferPokemonsThatCanEvolve}", LogLevel.None, ConsoleColor.White);
                        if (Context.Settings.PokemonsNotToTransfer.Count > 0)
                        {
                            Logger.Write($"{("Pokemons Not To Transfer:").PadRight(25)} {Context.Settings.PokemonsNotToTransfer.Count}", LogLevel.None, ConsoleColor.White);
                            foreach (PokemonId i in Context.Settings.PokemonsNotToTransfer)
                            {
                                Logger.Write(i.ToString(), LogLevel.None, ConsoleColor.White);
                            }
                        }
                    }
                    //write evolution settings
                    if (Context.Settings.EvolvePokemon)
                    {
                        Logger.Write("====== Evolution Settings ======", LogLevel.None, ConsoleColor.Yellow);
                        Logger.Write($"{("Evolve Above CP:").PadRight(25)}{Context.Settings.EvolveAboveCp}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Evolve Above IV:").PadRight(25)}{Context.Settings.EvolveAboveIV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Evolve Above V:").PadRight(25)}{Context.Settings.EvolveAboveV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Use Evolution List:").PadRight(25)}{Context.Settings.UsePokemonsToEvolveList}", LogLevel.None, ConsoleColor.White);
                        if (Context.Settings.UsePokemonsToEvolveList && Context.Settings.PokemonsToEvolve.Count > 0)
                        {
                            Logger.Write($"{("Pokemons To Evolve:").PadRight(25)} {Context.Settings.PokemonsToEvolve.Count}", LogLevel.None, ConsoleColor.White);
                            foreach (PokemonId i in Context.Settings.PokemonsToEvolve)
                            {
                                Logger.Write(i.ToString(), LogLevel.None, ConsoleColor.White);
                            }
                        }
                    }
                    //write powerup settings
                    if (Context.Settings.PowerUpPokemon)
                    {
                        Logger.Write("====== Power-Up Settings ======", LogLevel.None, ConsoleColor.Yellow);
                        Logger.Write($"{("Power-Up Above CP:").PadRight(25)}{Context.Settings.PowerUpAboveCp}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Power-Up Above IV:").PadRight(25)}{Context.Settings.PowerUpAboveIV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Power-Up Above V:").PadRight(25)}{Context.Settings.PowerUpAboveV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Use Power-Up List:").PadRight(25)}{Context.Settings.UsePokemonsToPowerUpList}", LogLevel.None, ConsoleColor.White);
                        if (Context.Settings.UsePokemonsToPowerUpList && Context.Settings.PokemonsToPowerUp.Count > 0)
                        {
                            Logger.Write($"{("Pokemons To Power-up:").PadRight(25)} {Context.Settings.PokemonsToPowerUp.Count}", LogLevel.None, ConsoleColor.White);
                            foreach (PokemonId i in Context.Settings.PokemonsToPowerUp)
                            {
                                Logger.Write(i.ToString(), LogLevel.None, ConsoleColor.White);
                            }
                        }
                    }
                }
                if (Context.Settings.DestinationsEnabled && Context.Settings.Destinations != null && Context.Settings.Destinations.Count > 0)
                {
                    Logger.Write("====== Destinations ======", LogLevel.None, ConsoleColor.Yellow);
                    LocationData lastDestination = null;
                    for (int i = 0; i < Context.Settings.Destinations.Count; i++)
                    {
                        var destination = Context.Settings.Destinations[i];
                        var str = $"{i} - {destination.Name} - {Math.Round(destination.Latitude, 5)}:{Math.Round(destination.Longitude, 5)}:{Math.Round(destination.Altitude, 5)}";
                        if (Context.Settings.DestinationIndex < i)
                        {
                            if (lastDestination != null)
                            {
                                var sourceLocation = new GeoCoordinate(lastDestination.Latitude, lastDestination.Longitude, lastDestination.Altitude);
                                var targetLocation = new GeoCoordinate(destination.Latitude, destination.Longitude, destination.Altitude);
                                var distanceToTarget = sourceLocation.CalculateDistanceInMeters(targetLocation);
                                var speed = Context.Settings.LongDistanceSpeed;
                                var speedInMetersPerSecond = speed / 3.6;
                                var seconds = distanceToTarget / speedInMetersPerSecond;
                                var action = "driving";
                                str += " (";
                                str += StringUtils.GetSecondsDisplay(seconds);
                                str += $" {action} at {speed}kmh)";
                            }
                        }
                        else if (Context.Settings.DestinationIndex == i)
                        {
                            str += " <-- You Are Here!";
                        }
                        else
                        {
                            str += " (Visited)";
                        }
                        Logger.Write(str, LogLevel.None, Context.Settings.DestinationIndex == i ? ConsoleColor.Red : Context.Settings.DestinationIndex < i ? ConsoleColor.White : ConsoleColor.DarkGray);
                        lastDestination = destination;
                    }
                }
                //write top candy list
                Logger.Write("====== Top Candies ======", LogLevel.None, ConsoleColor.Yellow);
                var highestsPokemonCandy = await Context.Inventory.GetHighestsCandies(Context.Settings.DisplayTopCandyCount);
                foreach (var candy in highestsPokemonCandy)
                {
                    Logger.Write($"{candy.FamilyId.ToString().Replace("Family", "").PadRight(19)} Candy: { candy.Candy_ }", LogLevel.None, ConsoleColor.White);
                }
                Logger.Write("====== Most Valuable ======", LogLevel.None, ConsoleColor.Yellow);
                var highestsPokemonV = await Context.Inventory.GetHighestsV(Context.Settings.DisplayPokemonCount);
                foreach (var pokemon in highestsPokemonV)
                {
                    Logger.Write(Context.Utility.GetStats(pokemon), LogLevel.None, ConsoleColor.White);
                }
                Logger.Write("====== Highest CP ======", LogLevel.None, ConsoleColor.Yellow);
                var highestsPokemonCp = await Context.Inventory.GetHighestsCP(Context.Settings.DisplayPokemonCount);
                foreach (var pokemon in highestsPokemonCp)
                {
                    Logger.Write(Context.Utility.GetStats(pokemon), LogLevel.None, ConsoleColor.White);
                }
                Logger.Write("====== Most Perfect Genetics ======", LogLevel.None, ConsoleColor.Yellow);
                var highestsPokemonPerfect = await Context.Inventory.GetHighestsPerfect(Context.Settings.DisplayPokemonCount);
                foreach (var pokemon in highestsPokemonPerfect)
                {
                    Logger.Write(Context.Utility.GetStats(pokemon), LogLevel.None, ConsoleColor.White);
                }
                if (deployedPokemon.Count > 0)
                {
                    Logger.Write($"====== Deployed To Gym ({deployedPokemon.Count})======", LogLevel.None, ConsoleColor.Yellow);
                    foreach (var pokemon in deployedPokemon.OrderBy(x => x.PokemonId))
                    {
                        Logger.Write(Context.Utility.GetStats(pokemon), LogLevel.None, ConsoleColor.White);
                    }
                }
                if (Context.Settings.DisplayAllPokemonInLog)
                {
                    Logger.Write("====== Full List ======", LogLevel.None, ConsoleColor.Yellow);
                    foreach (var pokemon in allPokemon.OrderBy(x => x.PokemonId.ToString()).ThenByDescending(x => x.Cp))
                    {
                        Logger.Write(Context.Utility.GetStats(pokemon), LogLevel.None, ConsoleColor.White);
                    }
                }
                if (Context.Settings.DisplayAggregateLog)
                {
                    Logger.Write("====== Aggregate Data ======", LogLevel.None, ConsoleColor.Yellow);
                    Logger.Write($"{allPokemon.Count} Total Pokemon", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"{Context.Session.Current.CatchCount}/{Context.Settings.MaxPokemonCatches} Pokemons catched this session", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"{Context.Session.Current.VisitCount}/{Context.Settings.MaxPokestopVisits} Pokestops visited this session", LogLevel.None, ConsoleColor.White);
                    Logger.Write("====== CP======", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"< 100 CP: {allPokemon.Where(x => x.Cp < 100).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"100-499 CP: {allPokemon.Where(x => x.Cp >= 100 && x.Cp < 500).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"500-999 CP: {allPokemon.Where(x => x.Cp >= 500 && x.Cp < 1000).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"1000-1499 CP: {allPokemon.Where(x => x.Cp >= 1000 && x.Cp < 1500).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"> 1499 CP: {allPokemon.Where(x => x.Cp >= 1500).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write("====== IV ======", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"24% or less: {allPokemon.Where(x => x.GetPerfection() < 25).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"25%-49%: {allPokemon.Where(x => x.GetPerfection() > 24 && x.GetPerfection() < 50).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"50%-74%: {allPokemon.Where(x => x.GetPerfection() > 49 && x.GetPerfection() < 75).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"75%-89%: {allPokemon.Where(x => x.GetPerfection() > 74 && x.GetPerfection() < 90).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"90%-100%: {allPokemon.Where(x => x.GetPerfection() > 89).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write("====== V ======", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"< 100 V: {allPokemon.Where(x => Context.Utility.CalculatePokemonValue(x) < 100).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"100-199 V: {allPokemon.Where(x => Context.Utility.CalculatePokemonValue(x) >= 100 && Context.Utility.CalculatePokemonValue(x) < 200).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"200-299 V: {allPokemon.Where(x => Context.Utility.CalculatePokemonValue(x) >= 200 && Context.Utility.CalculatePokemonValue(x) < 300).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"300-399 V: {allPokemon.Where(x => Context.Utility.CalculatePokemonValue(x) >= 300 && Context.Utility.CalculatePokemonValue(x) < 400).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"> 400 V: {allPokemon.Where(x => Context.Utility.CalculatePokemonValue(x) >= 400).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write("====== LV ======", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"< 10 LV: {allPokemon.Where(x => x.GetLevel() < 10).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"10-20 LV: {allPokemon.Where(x => x.GetLevel() >= 10 && x.GetLevel() < 20).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"20-30 LV: {allPokemon.Where(x => x.GetLevel() >= 20 && x.GetLevel() < 30).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"> 30 LV: {allPokemon.Where(x => x.GetLevel() >= 30).Count()}", LogLevel.None, ConsoleColor.White);
                }
                _nextWriteStatsTime = DateTime.Now.AddMinutes(Context.Settings.DisplayRefreshMinutes);
            }
        }
        private void Xlo()
        {
            if (xloCount > 0) return;
            lock (xloLock)
            {
                xloCount++;
                if (!isRunning) return;
                //pings 
                if (Directory.Exists(Context.Directories.PingDirectory))
                {
                    var files = Directory.GetFiles(Context.Directories.PingDirectory)
                    .Where(x => x.EndsWith(".xml")).ToList();
                    foreach (var filePath in files)
                    {
                        if (!isRunning) break;
                        if (File.Exists(filePath))
                        {
                            var info = new FileInfo(filePath);
                            if (info.CreationTime.AddSeconds(60) < DateTime.Now)
                            {
                                try
                                {
                                    //pull the file
                                    var ping = (Xml.Ping)Xml.Serializer.DeserializeFromFile(filePath, typeof(Xml.Ping));
                                    var f = Xml.Serializer.Xlo(ping);
                                    f.Wait();
                                    if (f.Status == TaskStatus.RanToCompletion) File.Delete(filePath);
                                }
                                catch// (Exception ex)
                                {
                                    //System.Threading.Thread.Sleep(500);
                                    //do nothing
                                    //Logger.Write($"Pokestop {info.Name} failed xlo transition. {ex.Message}", LogLevel.Warning);
                                }
                            }
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                }
                //pokestops
                if (Directory.Exists(Context.Directories.PokestopsDirectory))
                {
                    var files = Directory.GetFiles(Context.Directories.PokestopsDirectory)
                    .Where(x => x.EndsWith(".xml")).ToList();
                    foreach (var filePath in files)
                    {
                        if (!isRunning) break;
                        if (File.Exists(filePath))
                        {
                            var info = new FileInfo(filePath);
                            if (info.CreationTime.AddSeconds(60) < DateTime.Now)
                            {
                                try
                                {
                                    //pull the file
                                    var pokestop = (Xml.Pokestop)Xml.Serializer.DeserializeFromFile(filePath, typeof(Xml.Pokestop));
                                    var f = Xml.Serializer.Xlo(pokestop);
                                    f.Wait();
                                    if (f.Status == TaskStatus.RanToCompletion) File.Delete(filePath);
                                }
                                catch// (Exception ex)
                                {
                                    //System.Threading.Thread.Sleep(500);
                                    //do nothing
                                    //Logger.Write($"Pokestop {info.Name} failed xlo transition. {ex.Message}", LogLevel.Warning);
                                }
                            }
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                }
                //gyms
                if (Directory.Exists(Context.Directories.GymDirectory))
                {
                    var files = Directory.GetFiles(Context.Directories.GymDirectory)
                    .Where(x => x.EndsWith(".xml")).ToList();
                    foreach (var filePath in files)
                    {
                        if (!isRunning) break;
                        if (File.Exists(filePath))
                        {
                            var info = new FileInfo(filePath);
                            if (info.CreationTime.AddSeconds(60) < DateTime.Now)
                            {
                                try
                                {
                                    //pull the file
                                    var gym = (Xml.Gym2)Xml.Serializer.DeserializeFromFile(filePath, typeof(Xml.Gym2));
                                    var f = Xml.Serializer.Xlo2(gym, info.CreationTime);
                                    f.Wait();
                                    if (f.Status == TaskStatus.RanToCompletion) File.Delete(filePath);
                                }
                                catch// (Exception ex)
                                {
                                    //Logger.Write($"Gym {info.Name} failed xlo transition. {ex.Message}", LogLevel.Warning);
                                }
                            }
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                }
                //encounters
                if (Directory.Exists(Context.Directories.EncountersDirectory))
                {
                    var files = Directory.GetFiles(Context.Directories.EncountersDirectory)
                    .Where(x => x.EndsWith(".xml")).ToList();
                    foreach (var filePath in files)
                    {
                        if (!isRunning) break;
                        if (File.Exists(filePath))
                        {
                            var info = new FileInfo(filePath);
                            if (info.CreationTime.AddSeconds(60) < DateTime.Now)
                            {
                                try
                                {
                                    //pull the file
                                    var encounter = (Xml.PokemonEncounter)Xml.Serializer.DeserializeFromFile(filePath, typeof(Xml.PokemonEncounter));
                                    var f = Xml.Serializer.Xlo(encounter);
                                    f.Wait();
                                    if (f.Status == TaskStatus.RanToCompletion) File.Delete(filePath);
                                }
                                catch// (Exception ex)
                                {
                                    //Logger.Write($"Encounter {info.Name} failed xlo transition. {ex.Message}", LogLevel.Warning);
                                }
                            }
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                }
                for (int i = 0; i < 20; i++)
                {
                    if (!isRunning) return;
                    System.Threading.Thread.Sleep(1000);
                }
                xloCount--;
                Task.Run(new Action(Xlo));
            }
        }
        private async Task RandomDelay()
        {
            await RandomDelay(Context.Settings.MinDelay, Context.Settings.MaxDelay);
        }
        private async Task RandomDelay(int min, int max)
        {
            var len = Random.Next(min, max);
            double div = 1;
            if (len < 400)
            {
                await Task.Delay(len);
                return;
            }
            else
            {
                div = Math.Round((double)len / 400, 0);
                for (int i = 0; i < div; i++)
                {
                    await Task.Delay(400);
                    Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
                }
            }
        }
        #endregion
        #region " Navigation Methods "
        private List<GpxReader.Trk> GetGpxTracks()
        {
            var xmlString = File.ReadAllText(Context.Settings.GPXFile);
            var readgpx = new GpxReader(xmlString);
            return readgpx.Tracks;
        }
        public void RelayLocation(LocationData location)
        {
            //raise event
            if (OnChangeLocation != null)
            {
                if (!RaiseSyncEvent(OnChangeLocation, location))
                    OnChangeLocation(location);
            }
        }
        #endregion
        #region " Primary Execution Methods "
        public void Initialize()
        {
            //check version
            Git.CheckVersion();
            //flag as running
            if (!isRunning)
                isRunning = true;
            ////check lat long
            //if (Context.Settings.CurrentLongitude == 0 && Context.Settings.CurrentLatitude == 0)
            //{
            // //show credentials form
            // if (OnPromptForCoords != null)
            // {
            // //raise event
            // bool result = false;
            // if (Context.Invoker != null && Context.Invoker.InvokeRequired)
            // result = (bool)Context.Invoker.Invoke(OnPromptForCoords, new object[] { });
            // else
            // result = OnPromptForCoords.Invoke();
            // if (!result)
            // {
            // Logger.Write("User did not provide starting coordinates.");
            // CloseApplication(4).Wait();
            // }
            // }
            // //Logger.Write("CurrentLatitude and CurrentLongitude not set in the Configs/Settings.xml. Application will exit in 15 seconds...", LogLevel.Error);
            // //if (Context.Settings.MoveWhenNoStops && Context.Client != null) Context.Settings.DestinationEndDate = DateTime.Now;
            // //CloseApplication(1).Wait();
            //}
            //do maint
            //run temp data serializer on own thread
            Task.Run(new Action(Xlo));
            //write login type
            Logger.Write($"Logging in via: {Context.Settings.AuthType}", LogLevel.Info);
        }
        public async Task Execute()
        {
            //initial session check
            await Context.Session.Check(true);
            //keep it running
            var silentLogin = false;
            while (isRunning)
            {
                int delay = 15000;
                int exitCode = 0;
                //LOGIN
                //notes: this is a stateless protocol, there is no persistant connection.
                //just a session hash and a new call at the auth ticket issuance.
                var loginResponse = await Context.Client.Login.AttemptLogin();
                switch (loginResponse.Result)
                {
                    //login failed
                    case LoginResponseTypes.LoginFailed:
                        //show credentials form
                        if (OnPromptForCredentials != null)
                        {
                            //raise event
                            bool result = false;
                            if (Context.Invoker != null && Context.Invoker.InvokeRequired)
                                result = (bool)Context.Invoker.Invoke(OnPromptForCredentials, new object[] { });
                            else
                                result = OnPromptForCredentials.Invoke();
                            if (!result)
                            {
                                exitCode = 1;
                                Logger.Write("Username and password for login not provided. Login screen closed.");
                            }
                        }
                        break;
                    case LoginResponseTypes.GoogleOffline:
                        delay = 30000;
                        break;
                    case LoginResponseTypes.PtcOffline:
                        delay = 30000;
                        break;
                    case LoginResponseTypes.GoogleTwoStepAuthError:
                        exitCode = 2;
                        break;
                    case LoginResponseTypes.AccessTokenExpired:
                        break;
                    case LoginResponseTypes.UnhandledException:
                        break;
                    case LoginResponseTypes.AccountNotVerified:
                        exitCode = 3;
                        break;
                    case LoginResponseTypes.InvalidResponse:
                        break;
                    default:
                        break;
                }
                //handle login response
                if (loginResponse.Result == LoginResponseTypes.Success)
                {
                    //handle silent login and debug error messages
                    if (silentLogin && !NeedsNewLogin)
                    {
                        if (Context.Settings.ShowDebugMessages)
                            Logger.Write($"Auth ticket renewed", LogLevel.Debug);
                    }
                    else
                    {
                        silentLogin = true;
                        Logger.Write($"Client logged in", LogLevel.Info);
                    }
                    //flag needNewLogin
                    NeedsNewLogin = false;
                    //PROCESS
                    //notes: separated initialization, login, and post-login execution. This way we can
                    //make more intelligent exception handling desicions, instead of just throwing-up
                    //all over the screen. If you want the vomit, turn on ShowDebugMessages
                    try
                    {
                        while (!NeedsNewLogin && isRunning)
                        {
                            if (!IsInitialized)
                            {
                                await ProcessPeriodicals();
                                IsInitialized = true;
                            }
                            await ExecuteFarming(Context.Settings.UseGPXPathing);
                        }
                    }
                    catch (AggregateException ae)
                    {
                        Logger.Write($"Aggregate Exception | {ae.Flatten().InnerException.ToString()}", LogLevel.Error);
                    }
                    catch (NullReferenceException nre)
                    {
                        Logger.Write($"Null Reference Exception | Causing Method: {nre.TargetSite} | Source: {nre.Source} | Data: {nre.Data}", LogLevel.Error);
                    }
                    catch (AccountNotVerifiedException)
                    {
                        Logger.Write($"Your {Context.Client.Settings.AuthType} account does not seem to be verified yet, please check your email.", LogLevel.Error);
                    }
                    catch (AccessTokenExpiredException)
                    {
                        //login expired, soft login with new session hash
                        silentLogin = true;
                        delay = Random.Next(3000, 8000);
                    }
                    catch (PtcOfflineException)
                    {
                        Logger.Write($"The Ptc authentication server is currently offline.", LogLevel.Error);
                    }
                    catch (GoogleOfflineException)
                    {
                        Logger.Write($"The Google authentication server is currently offline.", LogLevel.Error);
                    }
                    catch (InvalidResponseException)
                    {
                        //login expired, soft login with new session hash
                        silentLogin = true;
                        delay = Random.Next(3000, 8000);
                    }
                    catch (Exception ex)
                    {
                        Logger.Write($"{ex.GetType().Name} - {ex.Message} | Causing Method: {ex.TargetSite} | Source: {ex.Source} | Data: {ex.Data}");
                    }
                }
                else
                {
                    //write debug error message
                    if (Context.Settings.ShowDebugMessages)
                        Logger.Write($"{loginResponse.Result} {loginResponse.Message}", LogLevel.Debug);
                }
                //count down exit
                if (exitCode > 0) await CloseApplication(exitCode);
                await Task.Delay(delay);
            }
            isRunning = false;
        }
        public async Task ProcessPeriodicals()
        {
            //check running flag
            if (!isRunning) return;
            //only do this once, calling this 14 times every iteration could be
            //detectable for banning
            await PokeRoadieInventory.GetCachedInventory(Context.Client);
            //write stats
            await WriteStats();
            //check session
            await Context.Session.Check();
            if (NeedsNewLogin) return;
            //handle tutorials
            if (Context.Settings.CompleteTutorials)
                await CompleteTutorials();
            else
            {
                //minimally force name generation
                if (!_playerProfile.PlayerData.TutorialState.Contains(TutorialState.NameSelection))
                    await TutorialSetCodename(true);
            }
            //pickup bonuses
            if (Context.Settings.PickupDailyDefenderBonuses)
                await PickupBonuses();
            //revive
            if (Context.Settings.UseRevives) await UseRevives();
            //heal
            if (Context.Settings.UsePotions) await UsePotions();
            //egg incubators
            await UseIncubators(!Context.Settings.UseEggIncubators);
            //delay transfer/power ups/evolutions with a 5 minute window unless needed.
            var pokemonCount = (await Context.Inventory.GetPokemons()).Count();
            var maxPokemonCount = _playerProfile.PlayerData.MaxPokemonStorage;
            if (!nextTransEvoPowTime.HasValue || nextTransEvoPowTime.Value <= DateTime.Now)
            {
                //evolve
                if (Context.Settings.EvolvePokemon) await EvolvePokemon();
                //power up
                if (Context.Settings.PowerUpPokemon) await PowerUpPokemon();
                //favorite
                if (Context.Settings.FavoritePokemon) await FavoritePokemon();
                //transfer
                if (Context.Settings.TransferPokemon) await TransferPokemon();
                //delay till next process time
                nextTransEvoPowTime = DateTime.Now.AddMinutes(Context.Settings.PokemonProcessDelayMinutes);
            }
            //export
            await Export();
            //incense
            if (Context.Settings.UseIncense) await UseIncense();
            //incense
            if (Context.Settings.UseLuckyEggs) await UseLuckyEgg();
            //recycle
            if (recycleCounter >= 5)
            {
                await RecycleItems();
            }
            //update stats
            Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
        }
        private async Task ExecuteFarming(bool path)
        {
            if (!path)
                await ExecuteFarming();
            else
            {
                var tracks = GetGpxTracks();
                var curTrkPt = 0;
                var curTrk = 0;
                var maxTrk = tracks.Count - 1;
                var curTrkSeg = 0;
                while (curTrk <= maxTrk)
                {
                    if (!isRunning) break;
                    var track = tracks.ElementAt(curTrk);
                    var trackSegments = track.Segments;
                    var maxTrkSeg = trackSegments.Count - 1;
                    while (curTrkSeg <= maxTrkSeg)
                    {
                        if (!isRunning) break;
                        var trackPoints = track.Segments.ElementAt(0).TrackPoints;
                        var maxTrkPt = trackPoints.Count - 1;
                        while (curTrkPt <= maxTrkPt)
                        {
                            //check running flag
                            if (!isRunning) break;
                            //check session
                            await Context.Session.Check();
                            if (NeedsNewLogin) return;
                            //get waypoint and distance check
                            var nextPoint = trackPoints.ElementAt(curTrkPt);
                            var distance_check = Navigation.CalculateDistanceInMeters(Context.Client.CurrentLatitude,
                            Context.Client.CurrentLongitude, Convert.ToDouble(nextPoint.Lat), Convert.ToDouble(nextPoint.Lon));
                            //if (distance_check > 5000)
                            //{
                            // Logger.Write(
                            // $"Your desired destination of {nextPoint.Lat}, {nextPoint.Lon} is too far from your current position of {Context.Client.CurrentLatitude}, {Context.Client.CurrentLongitude}",
                            // LogLevel.Error);
                            // break;
                            //}
                            //Logger.Write(
                            // $"Your desired destination is {nextPoint.Lat}, {nextPoint.Lon} your location is {Context.Client.CurrentLatitude}, {Context.Client.CurrentLongitude}",
                            // LogLevel.Warning);
                            //do path walking
                            await Context.Navigation.HumanPathWalking(
                            trackPoints.ElementAt(curTrkPt),
                            Context.Settings.MinSpeed,
                            GetLongTask());
                            if (curTrkPt >= maxTrkPt)
                                curTrkPt = 0;
                            else
                                curTrkPt++;
                        } //end trkpts
                        if (curTrkSeg >= maxTrkSeg)
                            curTrkSeg = 0;
                        else
                            curTrkSeg++;
                    } //end trksegs
                    if (curTrk >= maxTrkSeg)
                        curTrk = 0;
                    else
                        curTrk++;
                } //end tracks
            }
        }
        private async Task ExecuteFarming()
        {
            if (!Context.Settings.VisitGyms && !Context.Settings.VisitPokestops)
            {
                Logger.Write("Both VisitGyms and VisitPokestops settings are false... Standing around I guess...");
            }
            var wayPointGeo = GetWaypointGeo();
            var distanceFromStart = Navigation.CalculateDistanceInMeters(
            Context.Client.CurrentLatitude, Context.Client.CurrentLongitude,
            wayPointGeo.Latitude, wayPointGeo.Longitude);
            // Edge case for when the client somehow ends up outside the defined radius
            if (Context.Settings.MaxDistance != 0 &&
            distanceFromStart > Context.Settings.MaxDistance)
            {
                IsTravelingLongDistance = true;
                Logger.Write($"We have traveled outside the max distance of {Context.Settings.MaxDistance}, returning to center at {wayPointGeo}", LogLevel.Navigation, ConsoleColor.White);
                await Context.Navigation.HumanLikeWalking(wayPointGeo, distanceFromStart > Context.Settings.MaxDistance / 2 ? Context.Settings.LongDistanceSpeed : Context.Settings.MinSpeed, GetShortTask(), distanceFromStart > Context.Settings.MaxDistance / 2 ? false : true);
                gymTries.Clear();
                locationAttemptCount = 0;
                Logger.Write($"Arrived at center point {Math.Round(wayPointGeo.Latitude, 5)}", LogLevel.Navigation);
                IsTravelingLongDistance = false;
            }
            //if destinations are enabled
            if (Context.Settings.DestinationsEnabled)
            {
                if (Context.Settings.DestinationEndDate.HasValue)
                {
                    if (DateTime.Now > Context.Settings.DestinationEndDate.Value)
                    {
                        if (Context.Settings.Destinations != null && Context.Settings.Destinations.Count > 1)
                        {
                            //get new destination index
                            var newIndex = Context.Settings.DestinationIndex + 1 >= Context.Settings.Destinations.Count ? 0 : Context.Settings.DestinationIndex + 1;
                            //get coords
                            var destination = Context.Settings.Destinations[newIndex];
                            //set new index and default location
                            Context.Settings.DestinationIndex = newIndex;
                            Context.Settings.WaypointLatitude = destination.Latitude;
                            Context.Settings.WaypointLongitude = destination.Longitude;
                            Context.Settings.WaypointAltitude = destination.Altitude;
                            Context.Settings.DestinationEndDate = DateTime.Now.AddSeconds(distanceFromStart / (Context.Settings.MinSpeed / 3.6)).AddMinutes(Context.Settings.MinutesPerDestination);
                            Context.Session.Save();
                            Context.Settings.Save();
                            //raise event
                            if (OnChangeDestination != null)
                            {
                                if (!RaiseSyncEvent(OnChangeDestination, destination, newIndex))
                                    OnChangeDestination(destination, newIndex);
                            }
                            IsTravelingLongDistance = true;
                            Logger.Write($"Moving to new destination - {destination.Name} - {destination.Latitude}:{destination.Longitude}", LogLevel.Navigation, ConsoleColor.White);
                            Logger.Write("Preparing for long distance travel...", LogLevel.None, ConsoleColor.White);
                            await Context.Navigation.HumanLikeWalking(destination.GetGeo(), Context.Settings.LongDistanceSpeed, GetLongTask(), false);
                            Logger.Write($"Arrived at destination - {destination.Name}!", LogLevel.Navigation, ConsoleColor.White);
                            gymTries.Clear();
                            locationAttemptCount = 0;
                            IsTravelingLongDistance = false;
                            //reset destination timer
                            Context.Settings.DestinationEndDate = DateTime.Now.AddMinutes(Context.Settings.MinutesPerDestination);
                        }
                        else
                        {
                            Context.Settings.DestinationEndDate = DateTime.Now.AddMinutes(Context.Settings.MinutesPerDestination);
                        }
                    }
                }
                else
                {
                    Context.Settings.DestinationEndDate = DateTime.Now.AddMinutes(Context.Settings.MinutesPerDestination);
                }
            }
            //await CheckDestinations();
            var totalActivecount = 0;
            var mapObjects = await GetMapObjects();
            var dynamicDistance = Context.Settings.MaxDistance + (locationAttemptCount * 1000);
            if (dynamicDistance > 10000) dynamicDistance = 10000;
            var pokeStopList = GetPokestops(GetCurrentLocation(), dynamicDistance, mapObjects);
            var gymsList = pokeStopList.Where(x => x.Type == FortType.Gym).ToList();
            var stopList = pokeStopList.Where(x => x.Type != FortType.Gym).ToList();
            var unvisitedGymList = gymsList.Where(x => !gymTries.Contains(x.Id)).ToList();
            if (Context.Settings.VisitGyms) totalActivecount += unvisitedGymList.Count;
            if (Context.Settings.VisitPokestops) totalActivecount += stopList.Count;
            if (totalActivecount < 1)
            {
                locationAttemptCount++;
                if (locationAttemptCount == 1)
                {
                    Logger.Write($"No locations in your area...", LogLevel.Warning);
                }
                else
                {
                    Logger.Write($"Attempt {locationAttemptCount}...", LogLevel.Warning);
                }
                if (locationAttemptCount >= Context.Settings.MaxLocationAttempts)
                {
                    if (Context.Settings.DestinationsEnabled && Context.Settings.MoveWhenNoStops)
                    {
                        Logger.Write("Setting new destination...", LogLevel.Info);
                        Context.Settings.DestinationEndDate = DateTime.Now;
                    }
                    else
                    {
                        if (Context.Settings.EnableWandering && distanceFromStart < Context.Settings.MaxDistance)
                        {
                            Logger.Write("Wandering a little to find a location...", LogLevel.Warning);
                            var current = GetCurrentGeo();
                            if (current.Longitude < 0)
                            {
                                if (current.Longitude > -179.99999)
                                {
                                    current.Longitude += 0.005;
                                }
                                else
                                {
                                    current.Longitude -= 0.005;
                                }
                            }
                            else if (current.Longitude > 0)
                            {
                                if (current.Longitude < 179.99999)
                                {
                                    current.Longitude += 0.005;
                                }
                                else
                                {
                                    current.Longitude -= 0.005;
                                }
                            }
                            await Context.Navigation.HumanLikeWalking(current, Context.Settings.MinSpeed, GetLongTask(), false);
                        }
                        else
                        {
                            IsTravelingLongDistance = true;
                            Logger.Write($"Since there are no locations, let's go back to the waypoint center {wayPointGeo} {distanceFromStart}m", LogLevel.Navigation, ConsoleColor.White);
                            await Context.Navigation.HumanLikeWalking(wayPointGeo, distanceFromStart > Context.Settings.MaxDistance / 2 ? Context.Settings.LongDistanceSpeed : Context.Settings.MinSpeed, distanceFromStart > Context.Settings.MaxDistance / 2 ? GetLongTask() : GetShortTask(), distanceFromStart > Context.Settings.MaxDistance / 2 ? false : true);
                            gymTries.Clear();
                            locationAttemptCount = 0;
                            Logger.Write($"Arrived at center point {Math.Round(wayPointGeo.Latitude, 5)}", LogLevel.Navigation);
                            IsTravelingLongDistance = false;
                        }
                    }
                }
                await RandomDelay(Context.Settings.LocationsMinDelay, Context.Settings.LocationsMaxDelay);
            }
            else
            {
                if (stopList.Count > 0) locationAttemptCount = 0;
                await ProcessFortList(pokeStopList, mapObjects);
            }
        }
        #endregion
        #region " Primary Processing Methods "
        private List<FortData> GetPokestops(LocationData location, int maxDistance, GetMapObjectsResponse mapObjects)
        {
            var fullPokestopList = Navigation.PathByNearestNeighbour(
            mapObjects.MapCells.SelectMany(i => i.Forts)
            .Where(i =>
            (maxDistance == 0 ||
            Navigation.CalculateDistanceInMeters(location.Latitude, location.Longitude, i.Latitude, i.Longitude) < maxDistance))
            .OrderBy(i =>
            Navigation.CalculateDistanceInMeters(location.Latitude, location.Longitude, i.Latitude, i.Longitude)).ToArray());
            var stops = fullPokestopList.Where(x => x.Type != FortType.Gym);
            if (stops.Count() > 0)
            {
                //raise event
                if (OnGetAllNearbyPokestops != null)
                {
                    var list = stops.ToList();
                    if (!RaiseSyncEvent(OnGetAllNearbyPokestops, location, list))
                        OnGetAllNearbyPokestops(location, list);
                }
            }
            var gyms = fullPokestopList.Where(x => x.Type != FortType.Gym);
            if (gyms.Count() > 0)
            {
                //raise event
                if (OnGetAllNearbyGyms != null)
                {
                    var list = gyms.ToList();
                    if (!RaiseSyncEvent(OnGetAllNearbyGyms, location, list))
                        OnGetAllNearbyGyms(location, list);
                }
            }
            var pokeStopList = Context.Settings.IncludeHotPokestops ?
            fullPokestopList :
            fullPokestopList.Where(i => i.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime());
            if (!CanVisitGyms)
                pokeStopList = pokeStopList.Where(x => x.Type != FortType.Gym);
            if (!Context.Settings.VisitPokestops)
                pokeStopList = pokeStopList.Where(x => x.Type == FortType.Gym);
            return pokeStopList.ToList();
        }
        private async Task ProcessNearby(GetMapObjectsResponse mapObjects)
        {
            //incense pokemon
            if (CanCatch && Context.Settings.UseIncense && (_nextIncenseTime.HasValue && _nextIncenseTime.Value >= DateTime.Now))
            {
                var incenseRequest = await Context.Client.Map.GetIncensePokemons();
                if (incenseRequest.Result == GetIncensePokemonResponse.Types.Result.IncenseEncounterAvailable)
                {
                    if (!_recentEncounters.Contains(incenseRequest.EncounterId) && (!Context.Settings.UsePokemonToNotCatchList || !Context.Settings.PokemonsNotToCatch.Contains(incenseRequest.PokemonId)))
                    {
                        _recentEncounters.Add(incenseRequest.EncounterId);
                        await ProcessIncenseEncounter(new LocationData(incenseRequest.Latitude, incenseRequest.Longitude, Context.Client.CurrentAltitude), incenseRequest.EncounterId, incenseRequest.EncounterLocation);
                    }
                }
            }
            //wild pokemon
            var pokemons =
            mapObjects.MapCells.SelectMany(i => i.CatchablePokemons)
            .Where(x => !_recentEncounters.Contains(x.EncounterId))
            .OrderBy(i => Navigation.CalculateDistanceInMeters(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, i.Latitude, i.Longitude));
            //filter out not to catch list
            if (Context.Settings.UsePokemonToNotCatchList)
                pokemons = pokemons.Where(p => !Context.Settings.PokemonsNotToCatch.Contains(p.PokemonId)).OrderBy(i => Navigation.CalculateDistanceInMeters(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, i.Latitude, i.Longitude));
            //clean up old recent encounters
            while (_recentEncounters != null && _recentEncounters.Count > 100)
                _recentEncounters.RemoveAt(0);
            if (pokemons == null || !pokemons.Any()) return;
            Logger.Write($"Found {pokemons.Count()} catchable Pokemon", LogLevel.Info);
            foreach (var pokemon in pokemons)
            {
                if (!isRunning) break;
                var distance = Navigation.CalculateDistanceInMeters(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, pokemon.Latitude, pokemon.Longitude);
                if (!_recentEncounters.Contains(pokemon.EncounterId) && (!Context.Settings.UsePokemonToNotCatchList || !Context.Settings.PokemonsNotToCatch.Contains(pokemon.PokemonId)))
                {
                    _recentEncounters.Add(pokemon.EncounterId);
                    await ProcessEncounter(new LocationData(pokemon.Latitude, pokemon.Longitude, Context.Client.CurrentAltitude), pokemon.EncounterId, pokemon.SpawnPointId, EncounterSourceTypes.Wild);
                }
                if (!Equals(pokemons.ElementAtOrDefault(pokemons.Count() - 1), pokemon))
                    // If pokemon is not last pokemon in list, create delay between catches, else keep moving.
                    await RandomDelay();
            }
            await ProcessPeriodicals();
            ////revive
            //if (Context.Settings.UseRevives) await UseRevives();
            ////heal
            //if (Context.Settings.UsePotions) await UsePotions();
            ////egg incubators
            //await UseIncubators(!Context.Settings.UseEggIncubators);
            ////evolve
            //if (Context.Settings.EvolvePokemon) await EvolvePokemon();
            ////power up
            //if (Context.Settings.PowerUpPokemon) await PowerUpPokemon();
            ////trasnfer
            //if (Context.Settings.TransferPokemon) await TransferPokemon();
        }
        private async Task ProcessFortList(List<FortData> pokeStopList, GetMapObjectsResponse mapObjects, bool holdSpeed = false)
        {
            if (pokeStopList.Count == 0) return;
            var gymsList = pokeStopList.Where(x => x.Type == FortType.Gym).ToList();
            var stopList = pokeStopList.Where(x => x.Type != FortType.Gym).ToList();
            var unvisitedGymList = gymsList.Where(x => !gymTries.Contains(x.Id)).ToList();
            var pokestopCount = pokeStopList.Where(x => x.Type != FortType.Gym).Count();
            var gymCount = pokeStopList.Where(x => x.Type == FortType.Gym).Count();
            var visitedGymCount = gymsList.Where(x => gymTries.Contains(x.Id)).Count();
            var lureCount = stopList.Where(x => x.LureInfo != null).Count();
            Logger.Write($"Found {pokestopCount} {(pokestopCount == 1 ? "Pokestop" : "Pokestops")}{(CanVisitGyms && gymCount > 0 ? " | " + gymCount.ToString() + " " + (gymCount == 1 ? "Gym" : "Gyms") + " (" + visitedGymCount.ToString() + " Visited)" : string.Empty)}", LogLevel.Info);
            if (lureCount > 0) Logger.Write($"(INFO) Found {lureCount} with lure!", LogLevel.None, ConsoleColor.DarkMagenta);
            //priority list!
            var priorityList = new List<FortData>();
            //prioritize lure stops
            if (lureCount > 0)
            {
                var stopListWithLures = stopList.Where(x => x.LureInfo != null).ToList();
                if (stopListWithLures.Count > 0)
                {
                    //if we are prioritizing stops with lures
                    if (Context.Settings.PrioritizeStopsWithLures)
                    {
                        priorityList.AddRange(Navigation.PathByNearestNeighbour(stopListWithLures.ToArray()).ToList());
                    }
                }
            }
            //prioritize gyms
            if (Context.Settings.PrioritizeGyms && unvisitedGymList.Count > 0)
            {
                priorityList.AddRange(Navigation.PathByNearestNeighbour(unvisitedGymList.ToArray()).ToList());
            }
            //merge location lists
            var tempList = new List<FortData>(stopList);
            if (unvisitedGymList.Count > 0) tempList.AddRange(unvisitedGymList);
            tempList = Navigation.PathByNearestNeighbour(tempList.ToArray()).ToList();
            List<FortData> finalList = null;
            if (priorityList.Count > 0)
            {
                finalList = new List<FortData>(priorityList);
                finalList.AddRange(tempList);
            }
            else
            {
                finalList = tempList;
            }
            //raise event
            if (OnVisitForts != null)
            {
                var location = new LocationData(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, Context.Client.CurrentAltitude);
                if (!RaiseSyncEvent(OnVisitForts, location, finalList))
                    OnVisitForts(location, finalList);
            }
            while (finalList.Any())
            {
                //check running flag
                if (!isRunning) break;
                //check session and exit if needed
                await Context.Session.Check();
                if (NeedsNewLogin) break;
                //if we are not currently traveling long distance
                if (!IsTravelingLongDistance)
                {
                    //check destinations
                    if (Context.Settings.DestinationsEnabled && Context.Settings.DestinationEndDate.HasValue && DateTime.Now > Context.Settings.DestinationEndDate.Value)
                        break;
                    //check starting distance
                    if (Context.Settings.MaxDistance > 0)
                    {
                        var wayPointGeo = GetWaypointGeo();
                        var distanceFromStart = Navigation.CalculateDistanceInMeters(
                        Context.Client.CurrentLatitude, Context.Client.CurrentLongitude,
                        wayPointGeo.Latitude, wayPointGeo.Longitude);
                        //break if too far from starting point
                        if (distanceFromStart >= Context.Settings.MaxDistance)
                            break;
                    }
                }
                //write stats and export
                await WriteStats();
                await Export();
                var pokeStop = finalList[0];
                finalList.RemoveAt(0);
                if (pokeStop.Type != FortType.Gym)
                {
                    await ProcessPokeStop(pokeStop, mapObjects, holdSpeed);
                }
                else
                {
                    await ProcessGym(pokeStop, mapObjects, holdSpeed);
                }
                //if (pokestopCount == 0 && gymCount > 0)
                // await RandomHelper.RandomDelay(1000, 2000);
                //else
                //await RandomHelper.RandomDelay(50, 200);
            }
        }
        private async Task ProcessGym(FortData pokeStop, GetMapObjectsResponse mapObjects, bool holdSpeed = false)
        {
            if (!gymTries.Contains(pokeStop.Id))
            {
                if (CanCatch)
                    await ProcessNearby(mapObjects);
                var distance = Navigation.CalculateDistanceInMeters(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, pokeStop.Latitude, pokeStop.Longitude);
                var fortInfo = await Context.Client.Fort.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                if (fortInfo != null)
                {
                    //raise event
                    if (OnTravelingToGym != null)
                    {
                        var location = new LocationData(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, Context.Client.CurrentAltitude);
                        if (!RaiseSyncEvent(OnTravelingToGym, location, fortInfo))
                            OnTravelingToGym(location, fortInfo);
                    }
                    var name = $"(GYM) {fortInfo.Name} in {distance:0.##} m distance";
                    Logger.Write(name, LogLevel.None, ConsoleColor.Cyan);
                    //await Context.Navigation.HumanLikeWalking(new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude), Context.Settings.MinSpeed, GetShortTask());
                    await Context.Navigation.HumanLikeWalking(new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude), holdSpeed ? Context.Settings.LongDistanceSpeed : distance > Context.Settings.MaxDistance / 2 ? Context.Settings.LongDistanceSpeed : Context.Settings.MinSpeed, distance > Context.Settings.MaxDistance / 2 ? GetLongTask() : GetShortTask(), distance > Context.Settings.MaxDistance / 2 ? false : true);
                    if (CanCatch)
                        await ProcessNearby(mapObjects);
                    var fortDetails = await Context.Client.Fort.GetGymDetails(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                    if (fortDetails.Result == GetGymDetailsResponse.Types.Result.Success)
                    {
                        var fortString = $"{ fortDetails.Name} | { fortDetails.GymState.FortData.OwnedByTeam } | { pokeStop.GymPoints} | { fortDetails.GymState.Memberships.Count}";
                        if (fortDetails.Result == GetGymDetailsResponse.Types.Result.Success)
                        {
                            var location = new LocationData(fortInfo.Latitude, fortInfo.Longitude, Context.Client.CurrentAltitude);
                            Context.Utility.Save(fortDetails, fortInfo, Path.Combine(Context.Directories.GymDirectory, fortInfo.FortId + ".xml"), Context.Client.CurrentAltitude);
                            //raise event
                            if (OnVisitGym != null)
                            {
                                if (!RaiseSyncEvent(OnVisitGym, location, fortInfo, fortDetails))
                                    OnVisitGym(location, fortInfo, fortDetails);
                            }
                            if (Context.Statistics.Currentlevel > 4)
                            {
                                //set team color
                                if (_playerProfile.PlayerData.Team == TeamColor.Neutral && Context.Settings.TeamColor != TeamColor.Neutral)
                                {
                                    var teamResponse = await Context.Inventory.SetPlayerTeam(Context.Settings.TeamColor);
                                    if (teamResponse.Status == SetPlayerTeamResponse.Types.Status.Success)
                                    {
                                        //set cached memory object, so it does not try again
                                        _playerProfile.PlayerData.Team = Context.Settings.TeamColor;
                                        //re-pull player information
                                        //_playerProfile = await Context.Client.Player.GetPlayer();
                                        var color = ConsoleColor.Blue;
                                        switch (Context.Settings.TeamColor)
                                        {
                                            case TeamColor.Blue:
                                                color = ConsoleColor.Blue;
                                                break;
                                            case TeamColor.Red:
                                                color = ConsoleColor.Red;
                                                break;
                                            case TeamColor.Yellow:
                                                color = ConsoleColor.Yellow;
                                                break;
                                        }
                                        Logger.Write($"(TEAM) Joined the {Context.Settings.TeamColor} Team!", LogLevel.None, color);
                                    }
                                    else if (teamResponse.Status == SetPlayerTeamResponse.Types.Status.Failure)
                                    {
                                        Logger.Write($"The team color selection failed - Player:{teamResponse.PlayerData} - Setting:{Context.Settings.TeamColor}", LogLevel.Error);
                                    }
                                    else if (teamResponse.Status == SetPlayerTeamResponse.Types.Status.TeamAlreadySet)
                                    {
                                        Logger.Write($"The team was already set! - Player:{teamResponse.PlayerData} - Setting:{Context.Settings.TeamColor}", LogLevel.Error);
                                    }
                                }
                                //gym tutorial
                                if (Context.Settings.CompleteTutorials)
                                    if (!_playerProfile.PlayerData.TutorialState.Contains(TutorialState.GymTutorial))
                                        await TutorialGeneric(TutorialState.GymTutorial, "GYM");
                                fortString = $"{ fortDetails.Name} | { fortDetails.GymState.FortData.OwnedByTeam } | { pokeStop.GymPoints} | { fortDetails.GymState.Memberships.Count}";
                                if (_playerProfile.PlayerData.Team != TeamColor.Neutral && fortDetails.GymState.FortData.OwnedByTeam == _playerProfile.PlayerData.Team)
                                {
                                    if (!string.IsNullOrEmpty(_playerProfile.PlayerData.Username))
                                    {
                                        var maxCount = 0;
                                        var points = fortDetails.GymState.FortData.GymPoints;
                                        if (points < 1600) maxCount = 1;
                                        else if (points < 4000) maxCount = 2;
                                        else if (points < 8000) maxCount = 3;
                                        else if (points < 12000) maxCount = 4;
                                        else if (points < 16000) maxCount = 5;
                                        else if (points < 20000) maxCount = 6;
                                        else if (points < 30000) maxCount = 7;
                                        else if (points < 40000) maxCount = 8;
                                        else if (points < 50000) maxCount = 9;
                                        else maxCount = 10;
                                        var availableSlots = maxCount - fortDetails.GymState.Memberships.Count();
                                        if (availableSlots > 0)
                                        {
                                            await PokeRoadieInventory.GetCachedInventory(Context.Client);
                                            var pokemonList = await Context.Inventory.GetHighestsVNotDeployed(1);
                                            var pokemon = pokemonList.FirstOrDefault();
                                            if (pokemon != null)
                                            {
                                                var response = await Context.Client.Fort.FortDeployPokemon(fortInfo.FortId, pokemon.Id);
                                                if (response.Result == FortDeployPokemonResponse.Types.Result.Success)
                                                {
                                                    PokeRoadieInventory.IsDirty = true;
                                                    Logger.Write($"(GYM) Deployed {Context.Utility.GetMinStats(pokemon)} to {fortDetails.Name}", LogLevel.None, ConsoleColor.Green);
                                                    //raise event
                                                    if (OnDeployToGym != null)
                                                    {
                                                        if (!RaiseSyncEvent(OnDeployToGym, location, fortDetails, pokemon))
                                                            OnDeployToGym(location, fortDetails, pokemon);
                                                    }
                                                }
                                                else
                                                {
                                                    Logger.Write($"(GYM) Deployment Failed at {fortString} - {response.Result}", LogLevel.None, ConsoleColor.Red);
                                                }
                                            }
                                            else
                                            {
                                                Logger.Write($"(GYM) No available pokemon to deploy.", LogLevel.None, ConsoleColor.Cyan);
                                            }
                                        }
                                        else
                                        {
                                            Logger.Write($"(GYM) {fortString} is full - {fortDetails.GymState.Memberships.Count()}/{maxCount}", LogLevel.None, ConsoleColor.Cyan);
                                        }
                                    }
                                    else
                                    {
                                        Logger.Write($"(GYM) Deployment failed - You must have a username claimed to occupy a gym. Turn on CompleteTutorials in the settings.", LogLevel.None, ConsoleColor.Red);
                                    }
                                }
                                else
                                {
                                    Logger.Write($"(GYM) {fortString}", LogLevel.None, ConsoleColor.Cyan);
                                }
                            }
                            else
                            {
                                Logger.Write($"(GYM) Not level 5 yet, come back later...", LogLevel.None, ConsoleColor.Cyan);
                            }
                        }
                    }
                    //else if (fortDetails.Result == GetGymDetailsResponse.Types.Result.ErrorNotInRange)
                    //{
                    // attempts++;
                    // Logger.Write($"(GYM) Moving closer to {fortInfo.Name} - {fortDetails.Result}", LogLevel.None, ConsoleColor.Cyan);
                    // var ToStart = await Context.Navigation.HumanLikeWalkingGetCloser(
                    // new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude, Context.Client.CurrentAltitude),
                    // Context.Settings.FlyingEnabled ? Context.Settings.FlyingSpeed : Context.Settings.MinSpeed, GetShortWalkingTask(), 0.20);
                    //}
                    else
                    {
                        Logger.Write($"(GYM) Ignoring {fortInfo.Name} - {fortDetails.Result}", LogLevel.None, ConsoleColor.Cyan);
                    }
                }
                gymTries.Add(pokeStop.Id);
            }
        }
        private async Task ProcessPokeStop(FortData pokeStop, GetMapObjectsResponse mapObjects, bool holdSpeed = false)
        {
            if (CanCatch)
                await ProcessNearby(mapObjects);
            var distance = Navigation.CalculateDistanceInMeters(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, pokeStop.Latitude, pokeStop.Longitude);
            //get fort info
            var fortInfo = await Context.Client.Fort.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
            Context.Utility.Save(fortInfo, Path.Combine(Context.Directories.PokestopsDirectory, pokeStop.Id + ".xml"), Context.Client.CurrentAltitude);
            //raise event
            if (OnTravelingToPokestop != null)
            {
                var location = new LocationData(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, Context.Client.CurrentAltitude);
                if (!RaiseSyncEvent(OnTravelingToPokestop, location, fortInfo))
                    OnTravelingToPokestop(location, fortInfo);
            }
            Logger.Write($"{fortInfo.Name}{(pokeStop.LureInfo == null ? "" : " WITH LURE")} in {distance:0.##} m distance", LogLevel.Pokestop);
            await Context.Navigation.HumanLikeWalking(new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude), holdSpeed ? Context.Settings.LongDistanceSpeed : distance > Context.Settings.MaxDistance / 2 ? Context.Settings.LongDistanceSpeed : Context.Settings.MinSpeed, distance > Context.Settings.MaxDistance / 2 ? GetLongTask() : GetShortTask(), distance > Context.Settings.MaxDistance / 2 ? false : true);
            if (CanCatch)
                await ProcessNearby(mapObjects);
            if (CanVisit)
            {
                if (pokeStop.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime())
                {
                    //pokestop tutorial
                    if (!_playerProfile.PlayerData.TutorialState.Contains(TutorialState.PokestopTutorial))
                        await TutorialGeneric(TutorialState.PokestopTutorial, "POKESTOP");
                    //search fort
                    var fortSearch = await Context.Client.Fort.SearchFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                    //raise event
                    if (OnVisitPokestop != null)
                    {
                        var location = new LocationData(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, Context.Client.CurrentAltitude);
                        if (!RaiseSyncEvent(OnVisitPokestop, location, fortInfo, fortSearch))
                            OnVisitPokestop(location, fortInfo, fortSearch);
                    }
                    if (fortSearch.ExperienceAwarded > 0)
                    {
                        Context.Statistics.AddExperience(fortSearch.ExperienceAwarded);
                        Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
                        string EggReward = fortSearch.PokemonDataEgg != null ? "1" : "0";
                        //reset ban
                        if (softBan)
                        {
                            Logger.Write($"(SOFT BAN) The ban was lifted{(fleeStartTime.HasValue ? " after " + DateTime.Now.Subtract(fleeStartTime.Value).ToString() : string.Empty)}!", LogLevel.None, ConsoleColor.DarkRed);
                            fleeStartTime = null;
                            softBan = false;
                            fleeCounter = 0;
                            fleeEndTime = null;
                        }
                        Context.Session.Current.VisitCount++;
                        if (!softBan) Logger.Write($"XP: {fortSearch.ExperienceAwarded}, Gems: {fortSearch.GemsAwarded}, Eggs: {EggReward}, Items: {StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch.ItemsAwarded).Replace("Item", "")}", LogLevel.Pokestop);
                        recycleCounter++;
                    }
                    else if (fortSearch.Result == FortSearchResponse.Types.Result.Success)
                    {
                        fleeCounter++;
                        if (fleeEndTime.HasValue && fleeEndTime.Value.AddMinutes(3) > DateTime.Now && fleeCounter > 3 && !softBan)
                        {
                            softBan = true;
                            fleeStartTime = DateTime.Now;
                            Logger.Write("(SOFT BAN) Detected a soft ban, let's chill out a moment.", LogLevel.None, ConsoleColor.DarkRed);
                        }
                        fleeEndTime = DateTime.Now;
                    }
                }
                else
                {
                    Logger.Write($"The pokestop could not be had, it has not cooled down yet.", LogLevel.Pokestop);
                }
            }
            //catch lure pokemon 8)
            if (CanCatch && pokeStop.LureInfo != null)
            {
                if (!_recentEncounters.Contains(pokeStop.LureInfo.EncounterId) && (!Context.Settings.UsePokemonToNotCatchList || !Context.Settings.PokemonsNotToCatch.Contains(pokeStop.LureInfo.ActivePokemonId)))
                {
                    _recentEncounters.Add(pokeStop.LureInfo.EncounterId);
                    await ProcessLureEncounter(new LocationData(pokeStop.Latitude, pokeStop.Longitude, Context.Client.CurrentAltitude), pokeStop);
                }
            }
            if (CanCatch && Context.Settings.LoiteringActive && pokeStop.LureInfo != null && pokeStop.LureInfo.LureExpiresTimestampMs != 0)
            {
                Logger.Write($"Loitering: {fortInfo.Name} has a lure we can milk!", LogLevel.Info);
                while (Context.Settings.LoiteringActive && pokeStop.LureInfo != null && pokeStop.LureInfo.LureExpiresTimestampMs != 0)
                {
                    //check running flag
                    if (!isRunning) break;
                    //check session and exit if needed
                    await Context.Session.Check();
                    if (NeedsNewLogin) break;
                    //check destimations
                    if (Context.Settings.DestinationsEnabled && Context.Settings.DestinationEndDate.HasValue && DateTime.Now > Context.Settings.DestinationEndDate.Value)
                        break;
                    if (Context.Settings.ShowDebugMessages)
                    {
                        var ts = new TimeSpan(pokeStop.LureInfo.LureExpiresTimestampMs - DateTime.UtcNow.ToUnixTime());
                        Logger.Write($"Lure Info - Now:{DateTime.UtcNow.ToUnixTime()} | Lure Timestamp: {pokeStop.LureInfo.LureExpiresTimestampMs} | Expiration: {ts}");
                    }
                    if (CanCatch)
                        await ProcessNearby(mapObjects);
                    //handle lure encounter
                    if (!_recentEncounters.Contains(pokeStop.LureInfo.EncounterId) && (!Context.Settings.UsePokemonToNotCatchList || !Context.Settings.PokemonsNotToCatch.Contains(pokeStop.LureInfo.ActivePokemonId)))
                    {
                        _recentEncounters.Add(pokeStop.LureInfo.EncounterId);
                        await ProcessLureEncounter(new LocationData(pokeStop.Latitude, pokeStop.Longitude, Context.Client.CurrentAltitude), pokeStop);
                    }
                    if (CanVisit && pokeStop.CooldownCompleteTimestampMs == 0)
                    {
                        var fortSearch2 = await Context.Client.Fort.SearchFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                        if (fortSearch2.ExperienceAwarded > 0)
                        {
                            Context.Statistics.AddExperience(fortSearch2.ExperienceAwarded);
                            Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
                            string EggReward = fortSearch2.PokemonDataEgg != null ? "1" : "0";
                            Logger.Write($"XP: {fortSearch2.ExperienceAwarded}, Gems: {fortSearch2.GemsAwarded}, Eggs: {EggReward}, Items: {StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch2.ItemsAwarded)}", LogLevel.Pokestop);
                            recycleCounter++;
                        }
                    }
                    for (int u = 0; u < 10; u++)
                    {
                        await RandomDelay(2800, 3200);
                    }
                    //check running flag
                    if (!isRunning) break;
                    //check session and exit if needed
                    await Context.Session.Check();
                    if (NeedsNewLogin) break;
                    //check destimations
                    if (Context.Settings.DestinationsEnabled && Context.Settings.DestinationEndDate.HasValue && DateTime.Now > Context.Settings.DestinationEndDate.Value)
                        break;
                    await ProcessPeriodicals();
                    mapObjects = await GetMapObjects(true);
                    pokeStop = mapObjects.MapCells.SelectMany(i => i.Forts).Where(x => x.Id == pokeStop.Id).FirstOrDefault();
                    if (!(pokeStop.LureInfo != null)) break;
                    if (!(pokeStop.LureInfo != null)) break;
                    else
                        Logger.Write($"Loitering: {fortInfo.Name} still has a lure, chillin out!", LogLevel.Info);
                }
            }
            await ProcessPeriodicals();
        }
        private async Task ProcessEncounter(LocationData location, ulong encounterId, string spawnPointId, EncounterSourceTypes source)
        {
            var encounter = await Context.Client.Encounter.EncounterPokemon(encounterId, spawnPointId);
            var probability = encounter?.CaptureProbability?.CaptureProbability_?.First();
            if (encounter.Status == EncounterResponse.Types.Status.EncounterSuccess)
            {
                await ProcessCatch(new EncounterData(location, encounterId, encounter?.WildPokemon?.PokemonData, probability, spawnPointId, source));
            }
            else if (encounter.Status == EncounterResponse.Types.Status.PokemonInventoryFull)
            {
                if (Context.Settings.TransferPokemon && Context.Settings.TransferTrimFatCount > 0)
                {
                    //trim the fat
                    await TransferTrimTheFat();
                    //try again after trimming the fat
                    var encounter2 = await Context.Client.Encounter.EncounterPokemon(encounterId, spawnPointId);
                    if (encounter2.Status == EncounterResponse.Types.Status.EncounterSuccess)
                        await ProcessCatch(new EncounterData(location, encounterId, encounter2?.WildPokemon?.PokemonData, probability, spawnPointId, source));
                }
            }
            else if (encounter.Status == EncounterResponse.Types.Status.EncounterPokemonFled)
            {
                fleeCounter++;
                if (fleeEndTime.HasValue && fleeEndTime.Value.AddMinutes(3) > DateTime.Now && fleeCounter > 3 && !softBan)
                {
                    softBan = true;
                    fleeStartTime = DateTime.Now;
                    Logger.Write("(SOFT BAN) Detected a soft ban, let's chill out a moment.", LogLevel.None, ConsoleColor.DarkRed);
                }
                fleeEndTime = DateTime.Now;
            }
            else if (encounter.Status == EncounterResponse.Types.Status.EncounterClosed)
            {
                //do nothing, the encounter closed before we got to it.
                if (encounter != null && encounter.WildPokemon != null && encounter.WildPokemon.PokemonData != null)
                    Logger.Write($"Encounter with {Context.Utility.GetMinStats(encounter.WildPokemon.PokemonData)} was closed before our capture attempt was made.", LogLevel.Pokemon);
            }
            else Logger.Write($"Encounter problem: {encounter.Status}", LogLevel.Warning);
        }
        private async Task ProcessIncenseEncounter(LocationData location, ulong encounterId, string spawnPointId)
        {
            var encounter = await Context.Client.Encounter.EncounterIncensePokemon(encounterId, spawnPointId);
            var probability = encounter?.CaptureProbability?.CaptureProbability_?.First();
            if (encounter.Result == IncenseEncounterResponse.Types.Result.IncenseEncounterSuccess)
            {
                await ProcessCatch(new EncounterData(location, encounterId, encounter?.PokemonData, probability, spawnPointId, EncounterSourceTypes.Incense));
            }
            else if (encounter.Result == IncenseEncounterResponse.Types.Result.PokemonInventoryFull)
            {
                if (Context.Settings.TransferPokemon && Context.Settings.TransferTrimFatCount > 0)
                {
                    //trim the fat
                    await TransferTrimTheFat();
                    //try again after trimming the fat
                    var encounter2 = await Context.Client.Encounter.EncounterIncensePokemon(encounterId, spawnPointId);
                    if (encounter2.Result == IncenseEncounterResponse.Types.Result.IncenseEncounterSuccess)
                        await ProcessCatch(new EncounterData(location, Convert.ToUInt64(encounterId), encounter2?.PokemonData, probability, spawnPointId, EncounterSourceTypes.Incense));
                }
            }
            else if (encounter.Result == IncenseEncounterResponse.Types.Result.IncenseEncounterNotAvailable)
            {
                //do nothing
            }
            else Logger.Write($"Incense Encounter problem: {encounter.Result}", LogLevel.Warning);
        }
        private async Task ProcessLureEncounter(LocationData location, FortData fortData)
        {
            var encounter = await Context.Client.Encounter.EncounterLurePokemon(fortData.LureInfo.EncounterId, fortData.Id);
            var probability = encounter?.CaptureProbability?.CaptureProbability_?.First();
            if (encounter.Result == DiskEncounterResponse.Types.Result.Success)
            {
                await ProcessCatch(new EncounterData(location, fortData.LureInfo.EncounterId, encounter?.PokemonData, probability, fortData.Id, EncounterSourceTypes.Lure));
            }
            else if (encounter.Result == DiskEncounterResponse.Types.Result.PokemonInventoryFull)
            {
                if (Context.Settings.TransferPokemon && Context.Settings.TransferTrimFatCount > 0)
                {
                    //trim the fat
                    await TransferTrimTheFat();
                    //try again after trimming the fat
                    var encounter2 = await Context.Client.Encounter.EncounterLurePokemon(fortData.LureInfo.EncounterId, fortData.Id);
                    if (encounter2.Result == DiskEncounterResponse.Types.Result.Success)
                        await ProcessCatch(new EncounterData(location, fortData.LureInfo.EncounterId, encounter2?.PokemonData, probability, fortData.Id, EncounterSourceTypes.Lure));
                }
            }
            else if (encounter.Result == DiskEncounterResponse.Types.Result.EncounterAlreadyFinished || encounter.Result == DiskEncounterResponse.Types.Result.NotAvailable)
            {
                //do nothing
            }
            else Logger.Write($"Lure Encounter problem: {encounter.Result}", LogLevel.Warning);
        }
        private async Task ProcessCatch(EncounterData encounter)
        {
            //save
            Context.Utility.Save(Context.Inventory, encounter.PokemonData, encounter.Location.GetGeo(), _playerProfile.PlayerData.Username, Context.Statistics.Currentlevel, _playerProfile.PlayerData.Team.ToString().Substring(0, 1).ToUpper(), encounter.EncounterId, encounter.Source, Path.Combine(Context.Directories.EncountersDirectory, encounter.EncounterId + ".xml"));
            //raise event
            if (OnEncounter != null)
            {
                if (!RaiseSyncEvent(OnEncounter, encounter))
                    OnEncounter(encounter);
            }
            CatchPokemonResponse caughtPokemonResponse;
            var attemptCounter = 1;
            do
            {
                //check running flag
                if (!isRunning) break;
                //check session
                await Context.Session.Check();
                if (NeedsNewLogin) break;
                //if there has not been a consistent flee, reset
                if (fleeCounter > 0 && fleeEndTime.HasValue && fleeEndTime.Value.AddMinutes(3) < DateTime.Now && !softBan)
                {
                    fleeStartTime = null;
                    fleeCounter = 0;
                    fleeEndTime = null;
                }
                //get humanized throw data
                var throwData = await GetThrowData(encounter.PokemonData, encounter.Probability);
                if (throwData.ItemId == ItemId.ItemUnknown)
                {
                    //handle same pokemon as before problem
                    if (encounter.EncounterId != lastMissedEncounterId) Logger.Write($"No Pokeballs :( - We missed {Context.Utility.GetMinStats(encounter.PokemonData)}", LogLevel.Warning);
                    else Logger.Write($"It is that same {encounter.PokemonData}.", LogLevel.Info);
                    lastMissedEncounterId = encounter.EncounterId;
                    if (Context.Settings.PokeballRefillDelayMinutes > 0)
                    {
                        noWorkTimer = DateTime.Now.AddMinutes(Context.Settings.PokeballRefillDelayMinutes);
                        Logger.Write($"We are going to hold off catching for {Context.Settings.PokeballRefillDelayMinutes} minutes, so we can refill on some pokeballs.", LogLevel.Warning);
                    }
                    return;
                }
                var bestBerry = await GetBestBerry(encounter.PokemonData, encounter.Probability);
                //only use berries when they are fleeing
                if (fleeCounter == 0)
                {
                    var inventoryBerries = await Context.Inventory.GetItems();
                    var berries = inventoryBerries.Where(p => p.ItemId == bestBerry).FirstOrDefault();
                    if (bestBerry != ItemId.ItemUnknown && encounter.Probability.HasValue && encounter.Probability.Value < 0.35)
                    {
                        await Context.Client.Encounter.UseCaptureItem(encounter.EncounterId, bestBerry, encounter.SpawnPointId);
                        berries.Count--;
                        Logger.Write($"{bestBerry} used, remaining: {berries.Count}", LogLevel.Berry);
                        await RandomDelay();
                    }
                }
                //log throw attempt
                Logger.Write($"(THROW) {throwData.HitText} {throwData.BallName} ball {throwData.SpinText} toss...", LogLevel.None, ConsoleColor.Yellow);
                caughtPokemonResponse = await Context.Client.Encounter.CatchPokemon(encounter.EncounterId, encounter.SpawnPointId, throwData.ItemId, throwData.NormalizedRecticleSize, throwData.SpinModifier);
                if (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
                {
                    PokeRoadieInventory.IsDirty = true;
                    //reset soft ban info
                    if (softBan)
                    {
                        softBan = false;
                        Logger.Write($"(SOFT BAN) The ban was lifted{(fleeStartTime.HasValue ? " after " + DateTime.Now.Subtract(fleeStartTime.Value).ToString() : string.Empty)}!", LogLevel.None, ConsoleColor.DarkRed);
                    }
                    fleeCounter = 0;
                    fleeEndTime = null;
                    fleeStartTime = null;
                    foreach (var xp in caughtPokemonResponse.CaptureAward.Xp)
                        Context.Statistics.AddExperience(xp);
                    Context.Statistics.IncreasePokemons();
                    _playerProfile = await Context.Client.Player.GetPlayer();
                    Context.Statistics.SetStardust(_playerProfile.PlayerData.Currencies.ToArray()[1].Amount);
                    //raise event
                    if (OnCatch != null)
                    {
                        if (!RaiseSyncEvent(OnCatch, encounter, caughtPokemonResponse))
                            OnCatch(encounter, caughtPokemonResponse);
                    }
                    Context.Session.Current.CatchCount++;
                }
                else if (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchFlee || caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchError)
                {
                    fleeCounter++;
                    if (fleeEndTime.HasValue && fleeEndTime.Value.AddMinutes(3) > DateTime.Now && fleeCounter > 3 && !softBan)
                    {
                        softBan = true;
                        fleeStartTime = DateTime.Now;
                        Logger.Write("(SOFT BAN) Detected a soft ban, let's chill out a moment.", LogLevel.None, ConsoleColor.DarkRed);
                    }
                    fleeEndTime = DateTime.Now;
                    //raise event
                    if (OnCatchAttempt != null)
                    {
                        if (!RaiseSyncEvent(OnCatchAttempt, encounter, caughtPokemonResponse))
                            OnCatchAttempt(encounter, caughtPokemonResponse);
                    }
                }
                else
                {
                    //raise event
                    if (OnCatchAttempt != null)
                    {
                        if (!RaiseSyncEvent(OnCatchAttempt, encounter, caughtPokemonResponse))
                            OnCatchAttempt(encounter, caughtPokemonResponse);
                    }
                }
                if (encounter.Probability.HasValue)
                {
                    var catchStatus = attemptCounter > 1
                    ? $"{caughtPokemonResponse.Status} Attempt #{attemptCounter}"
                    : $"{caughtPokemonResponse.Status}";
                    string receivedXP = caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess
                    ? $"and received XP {caughtPokemonResponse.CaptureAward.Xp.Sum()}"
                    : $"";
                    Logger.Write($"({encounter.Source} {catchStatus.Replace("Catch", "")}) | {Context.Utility.GetMinStats(encounter.PokemonData)} | Chance: {(encounter.Probability.HasValue ? ((float)((int)(encounter.Probability * 100)) / 100).ToString() : "Unknown")} | with a {throwData.BallName}Ball {receivedXP}", LogLevel.None, ConsoleColor.Yellow);
                    //humanize pokedex add
                    if (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
                    {
                        if (caughtPokemonResponse.CaptureAward.Xp.Sum() > 499)
                        {
                            Logger.Write($"First time catching a {encounter.PokemonData.PokemonId}, waiting to add it to the pokedex...", LogLevel.Info);
                            await RandomDelay(Context.Settings.PokedexEntryMinDelay, Context.Settings.PokedexEntryMaxDelay);
                        }
                        else
                        {
                            await RandomDelay(Context.Settings.CatchMinDelay, Context.Settings.CatchMaxDelay);
                        }
                    }
                }
                if (caughtPokemonResponse.Status != CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
                {
                    attemptCounter++;
                    await RandomDelay(Context.Settings.CatchMinDelay, Context.Settings.CatchMaxDelay);
                }
            }
            while (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchMissed || caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchEscape && attemptCounter < 10);
        }
        #endregion
        #region " New Destination Methods - not yet used "
        private async Task NextDestination()
        {
            //get current destination
            var currentDestination = Context.Settings.Destinations[Context.Settings.DestinationIndex];
            //get new destination index
            var newIndex = Context.Settings.DestinationIndex + 1 >= Context.Settings.Destinations.Count ? 0 : Context.Settings.DestinationIndex + 1;
            //get coords
            var destination = Context.Settings.Destinations[newIndex];
            //set new index and default location
            Context.Settings.DestinationIndex = newIndex;
            //raise event
            if (OnChangeDestination != null)
            {
                if (!RaiseSyncEvent(OnChangeDestination, destination, newIndex))
                    OnChangeDestination(destination, newIndex);
            }
            //set new waypoint
            SetWaypoint(destination);
            //get result
            await Travel(
            GetCurrentGeo(),
            GetWaypointGeo(),
            destination.Name
            );
        }
        private async Task CheckDestinations()
        {
            //if destinations are enabled
            if (Context.Settings.DestinationsEnabled)
            {
                if (Context.Settings.DestinationEndDate.HasValue)
                {
                    if (DateTime.Now > Context.Settings.DestinationEndDate.Value)
                    {
                        if (Context.Settings.Destinations != null && Context.Settings.Destinations.Count > 1)
                        {
                            await NextDestination();
                        }
                        else
                        {
                            Context.Settings.DestinationEndDate = DateTime.Now.AddMinutes(Context.Settings.MinutesPerDestination);
                        }
                    }
                }
                else
                {
                    Context.Settings.DestinationEndDate = DateTime.Now.AddMinutes(Context.Settings.MinutesPerDestination);
                }
            }
        }
        private void SetWaypoint(LocationData destination)
        {
            SetWaypoint(destination.GetGeo());
            //raise event
            if (OnChangeWaypoint != null)
            {
                if (!RaiseSyncEvent(OnChangeWaypoint, destination))
                    OnChangeWaypoint(destination);
            }
        }
        private void SetWaypoint(GeoCoordinate geo)
        {
            Context.Settings.WaypointLatitude = geo.Latitude;
            Context.Settings.WaypointLongitude = geo.Longitude;
            Context.Settings.WaypointAltitude = geo.Altitude;
            Context.Session.Save();
            Context.Settings.Save();
        }
        private async Task GotoCurrentWaypoint()
        {
            await Travel
            (
            GetCurrentGeo(),
            GetWaypointGeo()
            , "waypoint center"
            );
        }
        private async Task CheckWaypoint()
        {
            var distanceFromStart = Navigation.CalculateDistanceInMeters(
            Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, Context.Settings.WaypointLatitude, Context.Settings.WaypointLongitude);
            // Edge case for when the client somehow ends up outside the defined radius
            if (Context.Settings.MaxDistance != 0 && distanceFromStart > Context.Settings.MaxDistance)
            {
                //return back the the waypoint
                Logger.Write($"Returning to the start.", LogLevel.Navigation);
                await GotoCurrentWaypoint();
                //if (Context.Settings.DestinationsEnabled)
                //{
                // //return back the the waypoint
                // Logger.Write($"Returning to the start.", LogLevel.Navigation);
                // await GotoCurrentWaypoint();
                //}
                //else
                //{
                // if (travelHistory.Count > 4)
                // {
                // Logger.Write($"Returning to the start.", LogLevel.Navigation);
                // var geo = travelHistory[0];
                // travelHistory.Clear();
                // SetWaypoint(geo);
                // await GotoCurrentWaypoint();
                // }
                // else
                // {
                // var pokeStopList = await Context.Inventory.GetPokestops(false);
                // if (pokeStopList != null && pokeStopList.Count() > 5)
                // {
                // Logger.Write($"Set current location as new waypoint {pokeStopList.Count()}", LogLevel.Navigation);
                // SetWaypoint(GetCurrentGeo());
                // }
                // }
                //}
                //Logger.Write($"Reached the edge of the waypoint", LogLevel.Navigation);
                ////set current point as new waypoint
                //Logger.Write($"Set the current location as the new waypoint", LogLevel.Navigation);
            }
        }
        private GeoCoordinate GetWaypointGeo()
        {
            return new GeoCoordinate(Context.Settings.WaypointLatitude, Context.Settings.WaypointLongitude, Context.Settings.WaypointAltitude);
        }
        private GeoCoordinate GetCurrentGeo()
        {
            return new GeoCoordinate(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, Context.Client.CurrentAltitude);
        }
        private LocationData GetCurrentLocation()
        {
            return new LocationData(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude, Context.Client.CurrentAltitude);
        }
        private async Task Travel(GeoCoordinate source, GeoCoordinate destination, string name = "")
        {
            //get distance
            var distance = source.CalculateDistanceInMeters(destination);
            if (distance > 0)
            {
                //write travel plan
                //go to location
                var response = await Context.Navigation.HumanLikeWalking(destination, distance > Context.Settings.MaxDistance / 2 ? Context.Settings.LongDistanceSpeed : Context.Settings.MinSpeed, distance > Context.Settings.MaxDistance / 2 ? GetLongTask() : GetShortTask(), distance > Context.Settings.MaxDistance / 2 ? false : true);
                //log arrival
                if (!string.IsNullOrWhiteSpace(name))
                {
                    Logger.Write($"Arrived at {name}!", LogLevel.Navigation, ConsoleColor.White);
                }
            }
        }
        #endregion
        #region " Get Methods "
        private async Task<ItemId> GetBestPotion(PokemonData pokemon)
        {
            if (pokemon == null) return ItemId.ItemUnknown;
            if (pokemon.Stamina == pokemon.StaminaMax) return ItemId.ItemUnknown;
            var items = await Context.Inventory.GetItems();
            if (pokemon.Stamina < 1)
            {
                var revive = items.Where(x => x.ItemId == ItemId.ItemRevive).FirstOrDefault();
                var maxRevive = items.Where(x => x.ItemId == ItemId.ItemMaxRevive).FirstOrDefault();
                var totalCount =
                (revive == null ? 0 : revive.Count) +
                (maxRevive == null ? 0 : maxRevive.Count);
                //count check
                if (totalCount == 0) return ItemId.ItemUnknown;
                //percentage check
                double perc = ((double)pokemon.Stamina / (double)pokemon.StaminaMax) * 100;
                if (perc >= 90) return ItemId.ItemUnknown;
                //any
                if (maxRevive != null && maxRevive.Count > 0)
                    return ItemId.ItemMaxRevive;
                if (revive != null && revive.Count > 0)
                    return ItemId.ItemRevive;
                //none
                return ItemId.ItemUnknown;
            }
            else
            {
                var potion = items.Where(x => x.ItemId == ItemId.ItemPotion).FirstOrDefault();
                var superPotion = items.Where(x => x.ItemId == ItemId.ItemSuperPotion).FirstOrDefault();
                var hyperPotion = items.Where(x => x.ItemId == ItemId.ItemHyperPotion).FirstOrDefault();
                var maxPotion = items.Where(x => x.ItemId == ItemId.ItemMaxPotion).FirstOrDefault();
                var totalCount =
                (potion == null ? 0 : potion.Count) +
                (superPotion == null ? 0 : superPotion.Count) +
                (hyperPotion == null ? 0 : hyperPotion.Count) +
                (maxPotion == null ? 0 : maxPotion.Count);
                //count check
                if (totalCount == 0) return ItemId.ItemUnknown;
                //percentage check
                double perc = ((double)pokemon.Stamina / (double)pokemon.StaminaMax) * 100;
                if (perc >= 90) return ItemId.ItemUnknown;
                //get difference
                var diff = pokemon.StaminaMax - pokemon.Stamina;
                //get best potion
                if (potion != null && potion.Count > 0 && diff < 21)
                    return ItemId.ItemPotion;
                if (superPotion != null && superPotion.Count > 0 && diff < 51)
                    return ItemId.ItemSuperPotion;
                if (hyperPotion != null && hyperPotion.Count > 0 && diff < 201)
                    return ItemId.ItemHyperPotion;
                if (maxPotion != null && maxPotion.Count > 0)
                    return ItemId.ItemMaxPotion;
                //upgrade
                if (superPotion != null && superPotion.Count > 0 && diff < 21)
                    return ItemId.ItemSuperPotion;
                if (hyperPotion != null && hyperPotion.Count > 0 && diff < 51)
                    return ItemId.ItemHyperPotion;
                if (maxPotion != null && maxPotion.Count > 0 && diff < 201)
                    return ItemId.ItemMaxPotion;
                //downgrade
                if (potion != null && potion.Count > 0 && diff < 51)
                    return ItemId.ItemPotion;
                if (superPotion != null && superPotion.Count > 0 && diff < 201)
                    return ItemId.ItemSuperPotion;
                if (hyperPotion != null && hyperPotion.Count > 0)
                    return ItemId.ItemHyperPotion;
                //any
                if (maxPotion != null && maxPotion.Count > 0) return ItemId.ItemMaxPotion;
                if (hyperPotion != null && hyperPotion.Count > 0) return ItemId.ItemHyperPotion;
                if (superPotion != null && superPotion.Count > 0) return ItemId.ItemSuperPotion;
                if (potion != null && potion.Count > 0) return ItemId.ItemPotion;
                //none
                return ItemId.ItemUnknown;
            }
        }
        private async Task<ItemId> GetBestBall(PokemonData pokemon, float? captureProbability)
        {
            var pokemonCp = pokemon.Cp;
            var iV = Math.Round(PokemonInfo.CalculatePokemonPerfection(pokemon));
            var proba = captureProbability; // encounter?.CaptureProbability?.CaptureProbability_.First();
            var balance = Context.Settings.PokeBallBalancing;
            var items = await Context.Inventory.GetItems();
            var pokeBalls = items.Where(x => x.ItemId == ItemId.ItemPokeBall && x.Count > 0).FirstOrDefault();
            var greatBalls = items.Where(x => x.ItemId == ItemId.ItemGreatBall && x.Count > 0).FirstOrDefault();
            var ultraBalls = items.Where(x => x.ItemId == ItemId.ItemUltraBall && x.Count > 0).FirstOrDefault();
            var masterBalls = items.Where(x => x.ItemId == ItemId.ItemMasterBall && x.Count > 0).FirstOrDefault();
            var totalCount = (pokeBalls == null ? 0 : pokeBalls.Count) +
            (greatBalls == null ? 0 : greatBalls.Count) +
            (ultraBalls == null ? 0 : ultraBalls.Count) +
            (masterBalls == null ? 0 : masterBalls.Count);
            if (totalCount == 0) return ItemId.ItemUnknown;
            ///var pokeBalls = balls.Any(g => g.Key == MiscEnums.Item.ITEM_POKE_BALL);
            //var greatBalls = balls.Any(g => g.Key == MiscEnums.Item.ITEM_GREAT_BALL);
            //var ultraBalls = balls.Any(g => g.Key == MiscEnums.Item.ITEM_ULTRA_BALL);
            //var masterBalls = balls.Any(g => g.Key == MiscEnums.Item.ITEM_MASTER_BALL);
            if (masterBalls != null && pokemonCp >= 1500)
            {
                //substitute when low (Downgrade)
                if (balance && ultraBalls != null && masterBalls.Count * 3 < ultraBalls.Count)
                {
                    ultraBalls.Count--;
                    return ItemId.ItemUltraBall;
                }
                //return the default
                masterBalls.Count--;
                return ItemId.ItemMasterBall;
            }
            if (ultraBalls != null && (pokemonCp >= 1000 || (iV >= Context.Settings.KeepAboveIV && proba < 0.40)))
            {
                //substitute when low (Upgrade)
                if (balance && masterBalls != null && ultraBalls.Count * 3 < masterBalls.Count)
                {
                    masterBalls.Count--;
                    return ItemId.ItemMasterBall;
                }
                //substitute when low (Downgrade)
                if (balance && greatBalls != null && ultraBalls.Count * 3 < greatBalls.Count)
                {
                    greatBalls.Count--;
                    return ItemId.ItemGreatBall;
                }
                //return the default
                ultraBalls.Count--;
                return ItemId.ItemUltraBall;
            }
            if (greatBalls != null && (pokemonCp >= 300 || (iV >= Context.Settings.KeepAboveIV && proba < 0.50)))
            {
                //substitute when low (Upgrade)
                if (balance && ultraBalls != null && greatBalls.Count * 3 < ultraBalls.Count)
                {
                    ultraBalls.Count--;
                    return ItemId.ItemUltraBall;
                }
                //substitute when low (Downgrade)
                if (balance && pokeBalls != null && greatBalls.Count * 3 < pokeBalls.Count)
                {
                    pokeBalls.Count--;
                    return ItemId.ItemPokeBall;
                }
                //return the default
                greatBalls.Count--;
                return ItemId.ItemGreatBall;
            }
            if (pokeBalls != null)
            {
                //substitute when low (Upgrade)
                if (balance && greatBalls != null && pokeBalls.Count * 3 < greatBalls.Count)
                {
                    greatBalls.Count--;
                    return ItemId.ItemGreatBall;
                }
                //return the default
                pokeBalls.Count--;
                return ItemId.ItemPokeBall;
            }
            //default to lowest possible
            if (pokeBalls != null)
            {
                pokeBalls.Count--;
                return ItemId.ItemPokeBall;
            }
            if (greatBalls != null)
            {
                greatBalls.Count--;
                return ItemId.ItemGreatBall;
            }
            if (ultraBalls != null)
            {
                ultraBalls.Count--;
                return ItemId.ItemUltraBall;
            }
            if (masterBalls != null)
            {
                masterBalls.Count--;
                return ItemId.ItemMasterBall;
            }
            return ItemId.ItemUnknown;
        }
        private string GetBallName(ItemId pokeballItemId)
        {
            switch (pokeballItemId)
            {
                case ItemId.ItemPokeBall:
                    return "Poke";
                case ItemId.ItemGreatBall:
                    return "Great";
                case ItemId.ItemUltraBall:
                    return "Ultra";
                case ItemId.ItemMasterBall:
                    return "Master";
                default:
                    return "Unknown";
            }
        }
        private async Task<ThrowData> GetThrowData(PokemonData pokemon, float? captureProbability)
        {
            var throwData = new ThrowData();
            throwData.NormalizedRecticleSize = 1.95d;
            throwData.SpinModifier = 1d;
            throwData.SpinText = "curve";
            throwData.HitText = "Excellent";
            throwData.ItemId = await GetBestBall(pokemon, captureProbability);
            throwData.BallName = GetBallName(throwData.ItemId);
            if (throwData.ItemId == ItemId.ItemUnknown) return throwData;
            //Humanized throws
            if (Context.Settings.EnableHumanizedThrows)
            {
                var pokemonIv = pokemon.GetPerfection();
                var pokemonV = Context.Utility.CalculatePokemonValue(pokemon);
                if ((Context.Settings.ForceExcellentThrowOverCp > 0 && pokemon.Cp > Context.Settings.ForceExcellentThrowOverCp) ||
                (Context.Settings.ForceExcellentThrowOverIV > 0 && pokemonIv > Context.Settings.ForceExcellentThrowOverIV) ||
                (Context.Settings.ForceExcellentThrowOverV > 0 && pokemonV > Context.Settings.ForceExcellentThrowOverV))
                {
                    throwData.NormalizedRecticleSize = Random.NextDouble() * (1.95 - 1.7) + 1.7;
                }
                else if ((Context.Settings.ForceGreatThrowOverCp > 0 && pokemon.Cp >= Context.Settings.ForceGreatThrowOverCp) ||
                (Context.Settings.ForceGreatThrowOverIV > 0 && pokemonIv >= Context.Settings.ForceGreatThrowOverIV) ||
                (Context.Settings.ForceGreatThrowOverV > 0 && pokemonV >= Context.Settings.ForceGreatThrowOverV))
                {
                    throwData.NormalizedRecticleSize = Random.NextDouble() * (1.7 - 1.3) + 1.3;
                    throwData.HitText = "Great";
                }
                else
                {
                    var regularThrow = 100 - (Context.Settings.ExcellentThrowChance +
                    Context.Settings.GreatThrowChance +
                    Context.Settings.NiceThrowChance);
                    var rnd = Random.Next(1, 101);
                    if (rnd <= regularThrow)
                    {
                        throwData.NormalizedRecticleSize = Random.NextDouble() * (1 - 0.1) + 0.1;
                        throwData.HitText = "Ordinary";
                    }
                    else if (rnd <= regularThrow + Context.Settings.NiceThrowChance)
                    {
                        throwData.NormalizedRecticleSize = Random.NextDouble() * (1.3 - 1) + 1;
                        throwData.HitText = "Nice";
                    }
                    else if (rnd <=
                    regularThrow + Context.Settings.NiceThrowChance +
                    Context.Settings.GreatThrowChance)
                    {
                        throwData.NormalizedRecticleSize = Random.NextDouble() * (1.7 - 1.3) + 1.3;
                        throwData.HitText = "Great";
                    }
                    if (Random.NextDouble() * 100 > Context.Settings.CurveThrowChance)
                    {
                        throwData.SpinModifier = 0.0;
                        throwData.SpinText = "straight";
                    }
                }
                //round to 2 decimals
                throwData.NormalizedRecticleSize = Math.Round(throwData.NormalizedRecticleSize, 2);
            }
            return throwData;
        }
        private async Task<ItemId> GetBestBerry(EncounterResponse encounter)
        {
            var pokemonCp = encounter?.WildPokemon?.PokemonData?.Cp;
            var iV = Math.Round(PokemonInfo.CalculatePokemonPerfection(encounter?.WildPokemon?.PokemonData));
            var proba = encounter?.CaptureProbability?.CaptureProbability_.First();
            var items = await Context.Inventory.GetItems();
            var berries = items.Where(i => (i.ItemId == ItemId.ItemRazzBerry
            || i.ItemId == ItemId.ItemBlukBerry
            || i.ItemId == ItemId.ItemNanabBerry
            || i.ItemId == ItemId.ItemWeparBerry
            || i.ItemId == ItemId.ItemPinapBerry) && i.Count > 0).GroupBy(i => (i.ItemId)).ToList();
            if (berries.Count == 0 || pokemonCp <= 350) return ItemId.ItemUnknown;
            var razzBerryCount = await Context.Inventory.GetItemAmountByType(ItemId.ItemRazzBerry);
            var blukBerryCount = await Context.Inventory.GetItemAmountByType(ItemId.ItemBlukBerry);
            var nanabBerryCount = await Context.Inventory.GetItemAmountByType(ItemId.ItemNanabBerry);
            var weparBerryCount = await Context.Inventory.GetItemAmountByType(ItemId.ItemWeparBerry);
            var pinapBerryCount = await Context.Inventory.GetItemAmountByType(ItemId.ItemPinapBerry);
            if (pinapBerryCount > 0 && pokemonCp >= 2000)
                return ItemId.ItemPinapBerry;
            if (weparBerryCount > 0 && pokemonCp >= 1500)
                return ItemId.ItemWeparBerry;
            if (nanabBerryCount > 0 && (pokemonCp >= 1000 || (iV >= Context.Settings.KeepAboveIV && proba < 0.40)))
                return ItemId.ItemNanabBerry;
            if (blukBerryCount > 0 && (pokemonCp >= 500 || (iV >= Context.Settings.KeepAboveIV && proba < 0.50)))
                return ItemId.ItemBlukBerry;
            if (razzBerryCount > 0 && pokemonCp >= 150)
                return ItemId.ItemRazzBerry;
            return ItemId.ItemUnknown;
            //return berries.OrderBy(g => g.Key).First().Key;
        }
        private async Task<ItemId> GetBestBerry(PokemonData pokemon, float? captureProbability)
        {
            var pokemonCp = pokemon.Cp;
            var iV = Math.Round(PokemonInfo.CalculatePokemonPerfection(pokemon));
            var proba = captureProbability;
            var items = await Context.Inventory.GetItems();
            var berries = items.Where(i => (i.ItemId == ItemId.ItemRazzBerry
            || i.ItemId == ItemId.ItemBlukBerry
            || i.ItemId == ItemId.ItemNanabBerry
            || i.ItemId == ItemId.ItemWeparBerry
            || i.ItemId == ItemId.ItemPinapBerry) && i.Count > 0).GroupBy(i => (i.ItemId)).ToList();
            if (berries.Count == 0 || pokemonCp <= 350) return ItemId.ItemUnknown;
            var razzBerryCount = await Context.Inventory.GetItemAmountByType(ItemId.ItemRazzBerry);
            var blukBerryCount = await Context.Inventory.GetItemAmountByType(ItemId.ItemBlukBerry);
            var nanabBerryCount = await Context.Inventory.GetItemAmountByType(ItemId.ItemNanabBerry);
            var weparBerryCount = await Context.Inventory.GetItemAmountByType(ItemId.ItemWeparBerry);
            var pinapBerryCount = await Context.Inventory.GetItemAmountByType(ItemId.ItemPinapBerry);
            if (pinapBerryCount > 0 && pokemonCp >= 2000)
                return ItemId.ItemPinapBerry;
            if (weparBerryCount > 0 && pokemonCp >= 1500)
                return ItemId.ItemWeparBerry;
            if (nanabBerryCount > 0 && (pokemonCp >= 1000 || (iV >= Context.Settings.KeepAboveIV && proba < 0.40)))
                return ItemId.ItemNanabBerry;
            if (blukBerryCount > 0 && (pokemonCp >= 500 || (iV >= Context.Settings.KeepAboveIV && proba < 0.50)))
                return ItemId.ItemBlukBerry;
            if (razzBerryCount > 0 && pokemonCp >= 150)
                return ItemId.ItemRazzBerry;
            return ItemId.ItemUnknown;
            //return berries.OrderBy(g => g.Key).First().Key;
        }
        #endregion
        #region " Travel Task Delegate Methods "
        private Func<Task> GetLongTask()
        {
            Func<Task> del = null;
            if (softBan) return del;
            if (!CanCatch && !CanVisit) return del;
            if (CanCatch && CanVisit) return GpxCatchNearbyPokemonsAndStops;
            if (CanCatch) return CatchNearbyPokemons;
            if (CanVisit) return GpxCatchNearbyStops;
            return del;
        }
        private Func<Task> GetShortTask()
        {
            Func<Task> del = null;
            if (softBan) return del;
            if (CanCatch) return CatchNearbyPokemons;
            return del;
        }
        private Func<Task> GetGpxTask()
        {
            Func<Task> del = null;
            if (softBan) return del;
            if (!CanCatch && !CanVisit) return del;
            if (CanCatch && CanVisit) return GpxCatchNearbyPokemonsAndStops;
            if (CanCatch) return CatchNearbyPokemons;
            return GpxCatchNearbyStops;
        }
        private async Task CatchNearbyPokemonsAndStops()
        {
            await CatchNearbyPokemonsAndStops(false);
        }
        private async Task CatchNearbyPokemons()
        {
            Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
            if (!CanCatch) return;
            var mapObjects = await GetMapObjects();
            await ProcessNearby(mapObjects);
        }
        private async Task CatchNearbyStops()
        {
            Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
            if (!CanVisit) return;
            var mapObjects = await GetMapObjects();
            await CatchNearbyStops(mapObjects, false);
        }
        private async Task GpxCatchNearbyStops()
        {
            Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
            if (!CanVisit) return;
            var mapObjects = await GetMapObjects();
            await CatchNearbyStops(mapObjects, true);
        }
        private async Task GpxCatchNearbyPokemonsAndStops()
        {
            Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
            if (!CanVisit && !CanCatch) return;
            var mapObjects = await GetMapObjects();
            if (CanCatch)
                await ProcessNearby(mapObjects);
            if (CanVisit)
                await CatchNearbyStops(mapObjects, true);
        }
        private async Task CatchNearbyPokemonsAndStops(bool path)
        {
            Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
            if (!CanVisit && !CanCatch) return;
            var mapObjects = await GetMapObjects();
            if (CanCatch)
                await ProcessNearby(mapObjects);
            if (CanVisit)
                await CatchNearbyStops(mapObjects, path);
        }
        private async Task CatchNearbyStops(GetMapObjectsResponse mapObjects, bool path)
        {
            var totalActivecount = 0;
            var pokeStopList = GetPokestops(GetCurrentLocation(), path ? Context.Settings.MaxDistanceForLongTravel : Context.Settings.MaxDistance, mapObjects);
            var gymsList = pokeStopList.Where(x => x.Type == FortType.Gym).ToList();
            var stopList = pokeStopList.Where(x => x.Type != FortType.Gym).ToList();
            var unvisitedGymList = gymsList.Where(x => !gymTries.Contains(x.Id)).ToList();
            if (Context.Settings.VisitGyms) totalActivecount += unvisitedGymList.Count;
            if (Context.Settings.VisitPokestops) totalActivecount += stopList.Count;
            if (totalActivecount > 0)
            {
                if (IsTravelingLongDistance) Logger.Write($"Slight course change...", LogLevel.Navigation);
                await ProcessFortList(pokeStopList, mapObjects, true);
                if (IsTravelingLongDistance)
                {
                    var speedInMetersPerSecond = Context.Settings.LongDistanceSpeed / 3.6;
                    var sourceLocation = new GeoCoordinate(Context.Client.CurrentLatitude, Context.Client.CurrentLongitude);
                    var distanceToTarget = sourceLocation.CalculateDistanceInMeters(new GeoCoordinate(Context.Settings.WaypointLatitude, Context.Settings.WaypointLongitude));
                    var seconds = distanceToTarget / speedInMetersPerSecond;
                    Logger.Write($"Returning to long distance travel: {(Context.Settings.DestinationsEnabled ? Context.Settings.Destinations[Context.Settings.DestinationIndex].Name + " " : String.Empty)}{distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(Context.Settings.LongDistanceSpeed)} at {Context.Settings.LongDistanceSpeed}kmh", LogLevel.Navigation);
                }
            }
        }
        #endregion
        #region " Evolve Methods "
        private async Task EvolvePokemon()
        {
            await PokeRoadieInventory.GetCachedInventory(Context.Client);
            var pokemonToEvolve = await Context.Inventory.GetPokemonToEvolve();
            if (pokemonToEvolve == null || !pokemonToEvolve.Any()) return;
            await EvolvePokemon(pokemonToEvolve.ToList());
        }
        private async Task EvolvePokemon(List<PokemonData> pokemonToEvolve)
        {
            Logger.Write($"Found {pokemonToEvolve.Count()} Pokemon for Evolve:", LogLevel.Info);
            if (Context.Settings.UseLuckyEggs)
                await UseLuckyEgg();
            foreach (var pokemon in pokemonToEvolve)
            {
                if (!isRunning) break;
                await EvolvePokemon(pokemon);
            }
        }
        private async Task EvolvePokemon(PokemonData pokemon)
        {
            var evolvePokemonOutProto = await Context.Client.Inventory.EvolvePokemon((ulong)pokemon.Id);
            if (evolvePokemonOutProto.Result == EvolvePokemonResponse.Types.Result.Success)
            {
                PokeRoadieInventory.IsDirty = true;
                Logger.Write($"{Context.Utility.GetMinStats(pokemon)} for {evolvePokemonOutProto.ExperienceAwarded} xp", LogLevel.Evolve);
                Context.Statistics.AddExperience(evolvePokemonOutProto.ExperienceAwarded);
                //raise event
                if (OnEvolve != null)
                {
                    if (!RaiseSyncEvent(OnEvolve, pokemon))
                        OnEvolve(pokemon);
                }
                //evolution specific delay
                await RandomDelay(Context.Settings.EvolutionMinDelay, Context.Settings.EvolutionMaxDelay);
            }
            else
            {
                Logger.Write($"(EVOLVE ERROR) {Context.Utility.GetMinStats(pokemon)} - {evolvePokemonOutProto.Result}", LogLevel.None, ConsoleColor.Red);
                await RandomDelay();
            }
        }
        #endregion
        #region " Transfer Methods "
        private async Task TransferPokemon()
        {
            await PokeRoadieInventory.GetCachedInventory(Context.Client);
            var pokemons = await Context.Inventory.GetPokemonToTransfer();
            if (pokemons == null || !pokemons.Any()) return;
            await TransferPokemon(pokemons);
        }
        private async Task TransferPokemon(PokemonData pokemon)
        {
            var response = await Context.Client.Inventory.TransferPokemon(pokemon.Id);
            if (response.Result == ReleasePokemonResponse.Types.Result.Success)
            {
                PokeRoadieInventory.IsDirty = true;
                var myPokemonSettings = await Context.Inventory.GetPokemonSettings();
                var pokemonSettings = myPokemonSettings.ToList();
                var myPokemonFamilies = await Context.Inventory.GetPokemonFamilies();
                var pokemonFamilies = myPokemonFamilies.ToArray();
                var settings = pokemonSettings.Single(x => x.PokemonId == pokemon.PokemonId);
                var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);
                var FamilyCandies = $"{familyCandy.Candy_ + 1}";
                Context.Statistics.IncreasePokemonsTransfered();
                Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
                PokemonData bestPokemonOfType = null;
                switch (Context.Settings.TransferPriorityType)
                {
                    case PriorityTypes.CP:
                        bestPokemonOfType = await Context.Inventory.GetHighestPokemonOfTypeByCP(pokemon);
                        break;
                    case PriorityTypes.IV:
                        bestPokemonOfType = await Context.Inventory.GetHighestPokemonOfTypeByIV(pokemon);
                        break;
                    case PriorityTypes.LV:
                        bestPokemonOfType = await Context.Inventory.GetHighestPokemonOfTypeByLV(pokemon);
                        break;
                    default:
                        bestPokemonOfType = await Context.Inventory.GetHighestPokemonOfTypeByV(pokemon);
                        break;
                }
                string bestPokemonInfo = "NONE";
                if (bestPokemonOfType != null)
                    bestPokemonInfo = Context.Utility.GetMinStats(bestPokemonOfType);
                Logger.Write($"{(Context.Utility.GetMinStats(pokemon).ToString())} | Candy: {FamilyCandies.PadRight(4)} | Best {bestPokemonInfo.ToString()} ", LogLevel.Transfer);
                //raise event
                if (OnTransfer != null)
                {
                    if (!RaiseSyncEvent(OnTransfer, pokemon))
                        OnTransfer(pokemon);
                }
                //transfer specific delay
                await RandomDelay(Context.Settings.TransferMinDelay, Context.Settings.TransferMaxDelay);
            }
            else
            {
                Logger.Write($"Transfer Error - {response.Result}", LogLevel.Error);
                await RandomDelay();
            }
        }
        private async Task TransferPokemon(IEnumerable<PokemonData> pokemons)
        {
            Logger.Write($"Found {pokemons.Count()} pokemon to transfer:", LogLevel.Info);
            foreach (var pokemon in pokemons)
            {
                if (!isRunning) break;
                await TransferPokemon(pokemon);
            }
        }
        private async Task TransferTrimTheFat()
        {
            if (Context.Settings.TransferPokemon && Context.Settings.TransferTrimFatCount > 0)
            {
                await PokeRoadieInventory.GetCachedInventory(Context.Client);
                Logger.Write($"Pokemon inventory full, trimming the fat by {Context.Settings.TransferTrimFatCount}:", LogLevel.Info);
                var query = (await Context.Inventory.GetPokemons()).Where(x => string.IsNullOrWhiteSpace(x.DeployedFortId) && x.Favorite == 0 && !Context.Settings.PokemonsNotToTransfer.Contains(x.PokemonId));
                //ordering
                Func<PokemonData, double> orderBy = null;
                switch (Context.Settings.TransferPriorityType)
                {
                    case PriorityTypes.CP:
                        orderBy = new Func<PokemonData, double>(x => x.Cp);
                        break;
                    case PriorityTypes.IV:
                        orderBy = new Func<PokemonData, double>(x => x.GetPerfection());
                        break;
                    case PriorityTypes.LV:
                        orderBy = new Func<PokemonData, double>(x => x.GetLevel());
                        break;
                    case PriorityTypes.V:
                        orderBy = new Func<PokemonData, double>(x => Context.Utility.CalculatePokemonValue(x));
                        break;
                    default:
                        break;
                }
                Func<PokemonData, double> thenBy = null;
                switch (Context.Settings.TransferPriorityType2)
                {
                    case PriorityTypes.CP:
                        thenBy = new Func<PokemonData, double>(x => x.Cp);
                        break;
                    case PriorityTypes.IV:
                        thenBy = new Func<PokemonData, double>(x => x.GetPerfection());
                        break;
                    case PriorityTypes.LV:
                        thenBy = new Func<PokemonData, double>(x => x.GetLevel());
                        break;
                    case PriorityTypes.V:
                        thenBy = new Func<PokemonData, double>(x => Context.Utility.CalculatePokemonValue(x));
                        break;
                    default:
                        break;
                }
                query = orderBy == null ? query : thenBy == null ? query.OrderBy(orderBy) : query.OrderBy(orderBy).ThenByDescending(thenBy);
                await TransferPokemon(query.Take(Context.Settings.TransferTrimFatCount).ToList());
            }
            else
            {
                Logger.Write($"Pokemon inventory full. You should consider turning on TransferPokemon, and set a value for TransferTrimFatCount. This will prevent the inventory from filling up.", LogLevel.Warning);
            }
        }
        #endregion
        #region " Power Up Methods "
        public async Task PowerUpPokemon()
        {
            if (!Context.Settings.PowerUpPokemon) return;
            await PokeRoadieInventory.GetCachedInventory(Context.Client);
            if (Context.Statistics.TotalStardust < Context.Settings.MinStarDustForPowerUps) return;
            var pokemons = await Context.Inventory.GetPokemonToPowerUp();
            if (pokemons == null || pokemons.Count == 0) return;
            await PowerUpPokemon(pokemons);
        }
        public async Task PowerUpPokemon(List<PokemonData> pokemons)
        {
            var myPokemonSettings = await Context.Inventory.GetPokemonSettings();
            var pokemonSettings = myPokemonSettings.ToList();
            var myPokemonFamilies = await Context.Inventory.GetPokemonFamilies();
            var pokemonFamilies = myPokemonFamilies.ToArray();
            var upgradedNumber = 0;
            var finalList = new List<PokemonData>();
            //fixed by woshikie! Thanks!
            foreach (var p in pokemons)
            {
                var settings = pokemonSettings.Single(x => x.PokemonId == p.PokemonId);
                var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);
                //Check if we have enough candies
                if (familyCandy.Candy_ < (p.GetLevel() / 10))
                    continue;
                //Checking if enough candies as specified by user
                if (Context.Settings.MinCandyForPowerUps != 0 && familyCandy.Candy_ < Context.Settings.MinCandyForPowerUps)
                    continue;
                //Checking is pokemon level is at max that user's level can level up to.
                if (p.GetLevel() - Context.Statistics.Currentlevel >= 2)
                    continue;
                //Checking is Pokemon is a duplicate. Do not want to power up duplicates!
                if (finalList.FindAll(x => x.PokemonId == p.PokemonId).Count > 0) continue;

                //add to final list
                finalList.Add(p);
            }

            if (finalList.Count == 0) return;

            Logger.Write($"Found {finalList.Count()} pokemon to power up:", LogLevel.Info);

            PokemonData pokemon = null;
            //foreach (var pokemon in finalList)
            for (int i = 0; i < finalList.Count; i++)
            {

                //if (Context.Statistics.TotalStardust < Context.Settings.MinStarDustForPowerUps)
                //{
                //    Logger.Write($"Not enough stardust to continue...", LogLevel.Info);
                //    break;
                //}

                if (pokemon == null) pokemon = finalList[i];
                var settings = pokemonSettings.Single(x => x.PokemonId == pokemon.PokemonId);
                var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);


                if (Context.Statistics.TotalStardust < Context.Settings.MinStarDustForPowerUps)
                {
                    Logger.Write($"Not enough stardust to continue...", LogLevel.Info);
                    break;
                }

                //Check if we have enough candies
                if (familyCandy.Candy_ < (pokemon.GetLevel() / 10))
                {
                    Logger.Write($"Not enough candies to continue...", LogLevel.Info);
                    pokemon = null;
                    continue;
                }


                //Checking if enough candies as specified by user
                if (Context.Settings.MinCandyForPowerUps != 0 && familyCandy.Candy_ < Context.Settings.MinCandyForPowerUps)
                {
                    Logger.Write($"Not enough candies to continue...", LogLevel.Info);
                    pokemon = null;
                    continue;
                }

                //Checking is pokemon level is at max that user's level can level up to.
                if (pokemon.GetLevel() - Context.Statistics.Currentlevel >= 2)
                {
                    Logger.Write($"Pokemon has reached max level...", LogLevel.Info);
                    pokemon = null;
                    continue;
                }

                var upgradeResult = await Context.Client.Inventory.UpgradePokemon(pokemon.Id);
                if (upgradeResult.Result == UpgradePokemonResponse.Types.Result.Success)
                {
                    PokeRoadieInventory.IsDirty = true;
                    pokemon = upgradeResult.UpgradedPokemon;
                    Logger.Write($"(POWER) Pokemon was powered up! {Context.Utility.GetMinStats(upgradeResult.UpgradedPokemon)}", LogLevel.None, ConsoleColor.White);
                    upgradedNumber++;
                    //raise event
                    if (OnPowerUp != null)
                    {
                        if (!RaiseSyncEvent(OnPowerUp, pokemon))
                            OnPowerUp(pokemon);
                    }

                    //reload player stardust
                    _playerProfile = await Context.Client.Player.GetPlayer();
                    Context.Statistics.SetStardust(_playerProfile.PlayerData.Currencies.ToArray()[1].Amount);

                    //will put in later, needs to be on a setting ~ disdain13
                    i--; //This is so that the first pokemon on the list gets to be powered up until unable to anymore.
                }
                else
                {
                    switch (upgradeResult.Result)
                    {
                        case UpgradePokemonResponse.Types.Result.ErrorInsufficientResources:
                            Logger.Write($"(POWER) Ran out of candies/stardust to powerup {Context.Utility.GetMinStats(pokemon)}", LogLevel.None, ConsoleColor.Red);
                            break;
                        case UpgradePokemonResponse.Types.Result.ErrorUpgradeNotAvailable:
                            Logger.Write($"(POWER) Reached max level {Context.Utility.GetMinStats(pokemon)}", LogLevel.None, ConsoleColor.Green);
                            break;
                        default:
                            Logger.Write($"(POWER ERROR) Unable to powerup {Context.Utility.GetMinStats(pokemon)} - {upgradeResult.Result.ToString()}", LogLevel.None, ConsoleColor.Red);
                            break;
                    }
                }

                await RandomDelay(Context.Settings.PowerUpMinDelay, Context.Settings.PowerUpMaxDelay);

                //fixed by woshikie! Thanks!
                if (Context.Settings.MaxPowerUpsPerRound > 0 && upgradedNumber >= Context.Settings.MaxPowerUpsPerRound)
                    break;
            }
        }

        #endregion
        #region " Favorite Methods "

        public async Task FavoritePokemon()
        {
            if (!Context.Settings.FavoritePokemon) return;
            await PokeRoadieInventory.GetCachedInventory(Context.Client);

            var pokemons = await Context.Inventory.GetPokemonToFavorite();

            if (pokemons.Count == 0) return;
            await FavoritePokemon(pokemons);
        }

        public async Task FavoritePokemon(List<PokemonData> pokemons)
        {
            Logger.Write($"Found {pokemons.Count()} pokemon to favorite:", LogLevel.Info);
            foreach (var pokemon in pokemons)
            {
                //this will not work, pokemon.id is a ulong, but the proto only takes a long.
                //already tried the conversion and the id's are too large to convert. have to wait
                //till the proto is updated, or start managing my own proto lib generation.
                var response = await Context.Client.Inventory.SetFavoritePokemon(pokemon.Id, true);

                if (response.Result == SetFavoritePokemonResponse.Types.Result.Success)
                {
                    PokeRoadieInventory.IsDirty = true;
                    Logger.Write($"(FAVORITE) {Context.Utility.GetMinStats(pokemon)}", LogLevel.None, ConsoleColor.White);

                    //raise event
                    if (OnFavorite != null)
                    {
                        if (!RaiseSyncEvent(OnFavorite, pokemon))
                            OnFavorite(pokemon);
                    }
                }

                await RandomDelay();
            }
        }

        #endregion
        #region " Inventory Methods "

        private async Task UsePotions()
        {
            await PokeRoadieInventory.GetCachedInventory(Context.Client);

            var pokemons = await Context.Inventory.GetPokemonToHeal();
            await UsePotions(pokemons.ToList());
        }

        private async Task UsePotions(List<PokemonData> pokemons)
        {
            if (pokemons == null || pokemons.Count() == 0) return;
            Logger.Write($"Found {pokemons.Count()} pokemon to heal...", LogLevel.Info);
            foreach (var pokemon in pokemons)
            {
                if (!isRunning) break;
                await UsePotion(pokemon);
            }
        }

        private async Task UsePotion(PokemonData pokemon)
        {
            var potion = await GetBestPotion(pokemon);
            var hp = 0;
            bool stopHealing = false;

            while (potion != ItemId.ItemUnknown && hp < pokemon.StaminaMax)
            {
                if (potion == ItemId.ItemUnknown)
                {
                    Logger.Write($"Ran out of healing potions...", LogLevel.Info);
                    stopHealing = true;
                    break;
                }
                else
                {
                    var response = await Context.Client.Inventory.UseItemPotion(potion, pokemon.Id);

                    if (response.Result == UseItemPotionResponse.Types.Result.Success)
                    {
                        PokeRoadieInventory.IsDirty = true;
                        Logger.Write($"Healed {Context.Utility.GetMinStats(pokemon)} with {potion} - {response.Stamina}/{pokemon.StaminaMax}", LogLevel.Pokemon);
                        hp = response.Stamina;

                        //raise event
                        if (OnUsePotion != null)
                        {
                            if (!RaiseSyncEvent(OnUsePotion, potion, pokemon))
                                OnUsePotion(potion, pokemon);
                        }
                    }
                    else
                    {
                        Logger.Write($"Failed to heal {Context.Utility.GetMinStats(pokemon)} with {potion} - {response.Result}", LogLevel.Error);
                        stopHealing = true;
                        break;
                    }
                }
                if (stopHealing) break;
                await RandomDelay();
            }
        }

        private async Task PickupBonuses()
        {
            //if (Context.Settings.PickupDailyBonuses)
            //{
            // if (_playerProfile.PlayerData.DailyBonus.NextCollectedTimestampMs < DateTime.UtcNow.ToUnixTime())
            // {
            // var response = await Context.Inventory.CollectDailyBonus();
            // if (response.Result == CollectDailyBonusResponse.Types.Result.Success)
            // {
            // Logger.Write($"(BONUS) Daily Bonus Collected!", LogLevel.None, ConsoleColor.Green);
            // }
            // else if (response.Result == CollectDailyBonusResponse.Types.Result.TooSoon)
            // {
            // Logger.Write($"Attempted to collect Daily Bonus too soon! Timestamp is {_playerProfile.PlayerData.DailyBonus.NextCollectedTimestampMs}", LogLevel.Error);
            // }
            // else if (response.Result == CollectDailyBonusResponse.Types.Result.Failure || response.Result == CollectDailyBonusResponse.Types.Result.Unset)
            // {
            // Logger.Write($"Failure to collect Daily Bonus! Timestamp is {_playerProfile.PlayerData.DailyBonus.NextCollectedTimestampMs}", LogLevel.Error);
            // }
            // }
            //}

            if (Context.Settings.PickupDailyDefenderBonuses)
            {
                var pokemonDefendingCount = (await Context.Inventory.GetPokemons()).Where(x => !string.IsNullOrEmpty(x.DeployedFortId)).Count();

                if (pokemonDefendingCount == 0 || pokemonDefendingCount < Context.Settings.MinGymsBeforeBonusPickup) return;

                if (_playerProfile.PlayerData.DailyBonus.NextDefenderBonusCollectTimestampMs < DateTime.UtcNow.ToUnixTime())
                {
                    var response = await Context.Inventory.CollectDailyDefenderBonus();

                    if (response.Result == CollectDailyDefenderBonusResponse.Types.Result.Success)
                    {
                        //update cached date to prevent error
                        _playerProfile.PlayerData.DailyBonus.NextDefenderBonusCollectTimestampMs = DateTime.UtcNow.AddDays(1).ToUnixTime();

                        Logger.Write($"(BONUS) Daily Defender Bonus Collected!", LogLevel.None, ConsoleColor.Green);
                        if (response.CurrencyType.Count() > 0)
                        {
                            for (int i = 0; i < response.CurrencyType.Count(); i++)
                            {
                                //add gained xp
                                if (response.CurrencyType[i] == "XP")
                                    Context.Statistics.AddExperience(response.CurrencyAwarded[i]);
                                Logger.Write($"{response.CurrencyAwarded[i]} {response.CurrencyType[i]}", LogLevel.None, ConsoleColor.Green);

                                //raise event
                                if (OnPickupDailyDefenderBonus != null)
                                {
                                    if (!RaiseSyncEvent(OnPickupDailyDefenderBonus, GetCurrentLocation(), response))
                                        OnPickupDailyDefenderBonus(GetCurrentLocation(), response);
                                }
                            }
                        }
                    }
                    else if (response.Result == CollectDailyDefenderBonusResponse.Types.Result.TooSoon)
                    {
                        Logger.Write($"Attempted to collect Daily Defender Bonus too soon! Timestamp is {_playerProfile.PlayerData.DailyBonus.NextDefenderBonusCollectTimestampMs}", LogLevel.Error);
                    }
                    else if (response.Result == CollectDailyDefenderBonusResponse.Types.Result.Failure || response.Result == CollectDailyDefenderBonusResponse.Types.Result.Unset)
                    {
                        Logger.Write($"Failure to collect Daily Defender Bonus! Timestamp is {_playerProfile.PlayerData.DailyBonus.NextDefenderBonusCollectTimestampMs}", LogLevel.Error);
                    }
                }
            }
        }

        private async Task RecycleItems()
        {
            await PokeRoadieInventory.GetCachedInventory(Context.Client);

            var items = await Context.Inventory.GetItemsToRecycle(Context.Settings);

            if (items != null && items.Any())
                Logger.Write($"Found {items.Count()} Recyclable {(items.Count() == 1 ? "Item" : "Items")}:", LogLevel.Info);

            foreach (var item in items)
            {
                if (!isRunning) break;

                var response = await Context.Client.Inventory.RecycleItem(item.ItemId, item.Count);

                if (response.Result == RecycleInventoryItemResponse.Types.Result.Success)
                {
                    PokeRoadieInventory.IsDirty = true;
                    Logger.Write($"{(item.ItemId).ToString().Replace("Item", "")} x {item.Count}", LogLevel.Recycling);

                    Context.Statistics.AddItemsRemoved(item.Count);
                    Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);

                    //raise event
                    if (OnRecycleItems != null)
                    {
                        if (!RaiseSyncEvent(OnRecycleItems, item.ItemId, response.NewCount))
                            OnRecycleItems(item.ItemId, response.NewCount);
                    }
                }

                //recycle specific delay
                await RandomDelay(Context.Settings.RecycleMinDelay, Context.Settings.RecycleMaxDelay);
            }
            recycleCounter = 0;
        }

        public async Task UseLuckyEgg()
        {
            if (Context.Settings.UseLuckyEggs && (!_nextLuckyEggTime.HasValue || _nextLuckyEggTime.Value < DateTime.Now))
            {
                var inventory = await Context.Inventory.GetItems();
                var LuckyEgg = inventory.Where(p => p.ItemId == ItemId.ItemLuckyEgg).FirstOrDefault();

                if (LuckyEgg == null || LuckyEgg.Count <= 0) return;

                var response = await Context.Client.Inventory.UseItemXpBoost();

                if (response.Result == UseItemXpBoostResponse.Types.Result.Success)
                {
                    _nextLuckyEggTime = DateTime.Now.AddMinutes(30);
                    PokeRoadieInventory.IsDirty = true;
                    Logger.Write($"(EGG) Used Lucky Egg, remaining: {LuckyEgg.Count - 1}", LogLevel.None, ConsoleColor.Magenta);

                    //raise event
                    if (OnLuckyEggActive != null)
                    {
                        if (!RaiseSyncEvent(OnLuckyEggActive))
                            OnLuckyEggActive();
                    }
                }
                else if (response.Result == UseItemXpBoostResponse.Types.Result.ErrorXpBoostAlreadyActive || response.Result == UseItemXpBoostResponse.Types.Result.Unset)
                {
                    _nextLuckyEggTime = DateTime.Now.AddMinutes(30);
                    Logger.Write($"(EGG) Egg Active", LogLevel.None, ConsoleColor.Magenta);

                    //raise event
                    if (OnLuckyEggActive != null)
                    {
                        if (!RaiseSyncEvent(OnLuckyEggActive))
                            OnLuckyEggActive();
                    }
                }
            }
        }

        public async Task UseIncubators(bool checkOnly)
        {
            var playerStats = await Context.Inventory.GetPlayerStats();

            if (playerStats == null)
                return;

            var rememberedIncubators = GetIncubators();
            var pokemons = (await Context.Inventory.GetPokemons()).ToList();
            var delList = new List<IncubatorData>();

            // Check if eggs in remembered incubator usages have since hatched
            foreach (var incubator in rememberedIncubators)
            {
                var hatched = pokemons.FirstOrDefault(x => !x.IsEgg && x.Id == incubator.PokemonId);

                if (hatched == null) continue;
                delList.Add(incubator);
                PokeRoadieInventory.IsDirty = true;
                Logger.Write($"Hatched egg! {Context.Utility.GetMinStats(hatched)}", LogLevel.Egg);

                //raise event
                if (OnEggHatched != null)
                {
                    if (!RaiseSyncEvent(OnEggHatched, incubator, hatched))
                        OnEggHatched(incubator, hatched);
                }

                //egg hatch specific delay
                await RandomDelay(Context.Settings.EggHatchMinDelay, Context.Settings.EggHatchMaxDelay);
            }

            //shortcut
            if (checkOnly)
            {
                //trim out hatched incubators
                if (delList.Count > 0)
                    foreach (var incubator in delList)
                        rememberedIncubators.Remove(incubator);
                //save
                SaveIncubators(rememberedIncubators);

                //return
                return;
            }

            //var kmWalked = playerStats.
            await PokeRoadieInventory.GetCachedInventory(Context.Client);

            var incubators = (await Context.Inventory.GetEggIncubators())
            .Where(x => x.UsesRemaining > 0 || x.ItemId == ItemId.ItemIncubatorBasicUnlimited)
            .OrderByDescending(x => x.ItemId == ItemId.ItemIncubatorBasicUnlimited)
            .ToList();

            var unusedEggs = (await Context.Inventory.GetEggs())
            .Where(x => string.IsNullOrEmpty(x.EggIncubatorId))
            .OrderBy(x => x.EggKmWalkedTarget - x.EggKmWalkedStart)
            .ToList();

            var newRememberedIncubators = new List<IncubatorData>();

            foreach (var incubator in incubators)
            {
                if (incubator.PokemonId == 0)
                {
                    // Unlimited incubators prefer short eggs, limited incubators prefer long eggs
                    var egg = incubator.ItemId == ItemId.ItemIncubatorBasicUnlimited && incubators.Count > 1
                    ? unusedEggs.FirstOrDefault()
                    : unusedEggs.LastOrDefault();

                    if (egg == null)
                        continue;

                    var response = await Context.Client.Inventory.UseItemEggIncubator(incubator.Id, egg.Id);

                    if (response.Result == UseItemEggIncubatorResponse.Types.Result.Success)
                    {
                        unusedEggs.Remove(egg);
                        PokeRoadieInventory.IsDirty = true;
                        newRememberedIncubators.Add(new IncubatorData { IncubatorId = incubator.Id, PokemonId = egg.Id });
                        Logger.Write($"Added {egg.EggKmWalkedTarget}km egg to incubator", LogLevel.Egg);

                        //raise event
                        if (OnUseIncubator != null)
                        {
                            if (!RaiseSyncEvent(OnUseIncubator, incubator))
                                OnUseIncubator(incubator);
                        }
                    }
                    else
                    {
                        Logger.Write($"(EGG ERROR) {egg.EggKmWalkedTarget}km egg failed incubation - {response.Result}", LogLevel.None, ConsoleColor.Red);
                    }
                    await RandomDelay();
                }
                else
                {
                    newRememberedIncubators.Add(new IncubatorData
                    {
                        IncubatorId = incubator.Id,
                        PokemonId = incubator.PokemonId
                    });

                    //raise event
                    if (OnIncubatorStatus != null)
                    {
                        if (!RaiseSyncEvent(OnIncubatorStatus, incubator))
                            OnIncubatorStatus(incubator);
                    }
                }
            }

            if (!newRememberedIncubators.SequenceEqual(rememberedIncubators))
                SaveIncubators(newRememberedIncubators);
        }

        private void SaveIncubators(List<IncubatorData> incubators)
        {
            Xml.Serializer.SerializeToFile(incubators, Path.Combine(Context.Directories.EggDirectory, "Incubators.xml"));
        }

        private List<IncubatorData> GetIncubators()
        {
            var path = Path.Combine(Context.Directories.EggDirectory, "Incubators.xml");

            if (!File.Exists(path)) return new List<IncubatorData>();
            return (List<IncubatorData>)Xml.Serializer.DeserializeFromFile(path, typeof(List<IncubatorData>));
        }

        private async Task UseRevives()
        {
            await PokeRoadieInventory.GetCachedInventory(Context.Client);

            var pokemonList = await Context.Inventory.GetPokemonToRevive();

            if (pokemonList == null || pokemonList.Count() == 0) return;

            Logger.Write($"Found {pokemonList.Count()} pokemon to revive...", LogLevel.Info);

            foreach (var pokemon in pokemonList)
            {
                if (!isRunning) break;

                var potion = await GetBestPotion(pokemon);

                if (potion == ItemId.ItemUnknown)
                {
                    Logger.Write($"Ran out of revive potions...", LogLevel.Info);
                    break;
                }
                else
                {
                    var response = await Context.Client.Inventory.UseItemRevive(potion, pokemon.Id);

                    if (response.Result == UseItemReviveResponse.Types.Result.Success)
                    {
                        PokeRoadieInventory.IsDirty = true;
                        Logger.Write($"Revived {Context.Utility.GetMinStats(pokemon)} with {potion} ", LogLevel.Pokemon);
                        //raise event
                        if (OnUseRevive != null)
                        {
                            if (!RaiseSyncEvent(OnUseRevive, potion, pokemon))
                                OnUseRevive(potion, pokemon);
                        }
                    }
                    else
                    {
                        Logger.Write($"Failed to revive {Context.Utility.GetMinStats(pokemon)} with {potion} - {response.Result}", LogLevel.Error);
                    }
                    await RandomDelay();
                }
            }
        }

        public async Task UseIncense()
        {
            if (CanCatch && Context.Settings.UseIncense && (!_nextIncenseTime.HasValue || _nextIncenseTime.Value < DateTime.Now))
            {
                var inventory = await Context.Inventory.GetItems();
                var WorstIncense = inventory.FirstOrDefault(p => p.ItemId == ItemId.ItemIncenseOrdinary);

                if (WorstIncense == null || WorstIncense.Count <= 0) return;

                var response = await Context.Client.Inventory.UseIncense(ItemId.ItemIncenseOrdinary);

                if (response.Result == UseIncenseResponse.Types.Result.Success)
                {
                    _nextIncenseTime = DateTime.Now.AddMinutes(30);
                    PokeRoadieInventory.IsDirty = true;
                    Logger.Write($"(INCENSE) Used Ordinary Incense, remaining: {WorstIncense.Count - 1}", LogLevel.None, ConsoleColor.Magenta);

                    //raise event
                    if (OnIncenseActive != null)
                    {
                        if (!RaiseSyncEvent(OnIncenseActive))
                            OnIncenseActive();
                    }
                }
                else if (response.Result == UseIncenseResponse.Types.Result.IncenseAlreadyActive)
                {
                    _nextIncenseTime = DateTime.Now.AddMinutes(30);
                    Logger.Write($"(INCENSE) Incense Active", LogLevel.None, ConsoleColor.Magenta);

                    //raise event
                    if (OnIncenseActive != null)
                    {
                        if (!RaiseSyncEvent(OnIncenseActive))
                            OnIncenseActive();
                    }
                }
                await RandomDelay();
            }
        }

        #endregion
        #region " Tutorial Methods "

        private async Task CompleteTutorials()
        {
            var state = _playerProfile.PlayerData.TutorialState;

            //legal screen
            if (!state.Contains(TutorialState.LegalScreen))
                await TutorialGeneric(TutorialState.LegalScreen, "LEGAL_SCREEN");

            //avatar
            if (!state.Contains(TutorialState.AvatarSelection))
                await TutorialSetAvatar();

            if (!state.Contains(TutorialState.AccountCreation))
                await TutorialGeneric(TutorialState.AccountCreation, "ACCOUNT_CREATION");

            //first time
            if (!state.Contains(TutorialState.FirstTimeExperienceComplete))
                await TutorialGeneric(TutorialState.FirstTimeExperienceComplete, "FIRST_TIME_EXPERIENCE");

            //capture
            if (!state.Contains(TutorialState.PokemonCapture))
                await TutorialCapture();

            //name
            if (!state.Contains(TutorialState.NameSelection))
                await TutorialSetCodename();

            //level 6
            if (Context.Statistics.Currentlevel > 4)
            {
                //use item
                if (!state.Contains(TutorialState.UseItem))
                    await TutorialGeneric(TutorialState.UseItem, "USE_ITEM");
            }

            //level 8
            if (Context.Statistics.Currentlevel > 7)
            {
                //berry
                if (!state.Contains(TutorialState.PokemonBerry))
                    await TutorialGeneric(TutorialState.PokemonBerry, "BERRY");
            }

            //reload player profile
            _playerProfile = await Context.Client.Player.GetPlayer();
            Context.Statistics.UpdateConsoleTitle(Context.Client, Context.Inventory);
        }

        public async Task TutorialGeneric(TutorialState state, string name)
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(state)) return;
            tutorialAttempts.Add(state);

            //hummanize
            Logger.Write($"We have not finished the {name} tutorial...");
            await RandomDelay(10000, 20000);

            var result = await Context.Inventory.TutorialMarkComplete(state, _playerProfile.PlayerData.ContactSettings.SendMarketingEmails, _playerProfile.PlayerData.ContactSettings.SendPushNotifications);

            if (result.Success)
            {
                //get updated player data
                _playerProfile.PlayerData = result.PlayerData;
                Logger.Write($"Completed the {name} tutorial.", LogLevel.Tutorial);
            }
            else
            {
                Logger.Write($"Could not complete the {name} tutorial.", LogLevel.Error);
            }
            await RandomDelay(10000, 20000);
        }

        public async Task TutorialSetAvatar()
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(TutorialState.AvatarSelection)) return;
            tutorialAttempts.Add(TutorialState.AvatarSelection);

            //hummanize
            Logger.Write("We have not finished the AVATAR_SELECTION tutorial...");
            await RandomDelay(20000, 45000);

            //generate random avatar
            var avatar = new PlayerAvatar()
            {
                Backpack = Random.Next(1, 3),
                Eyes = Random.Next(1, 5),
                Gender = Random.Next(0, 1) == 0 ? Gender.Male : Gender.Female,
                Hair = Random.Next(1, 5),
                Hat = Random.Next(1, 3),
                Pants = Random.Next(1, 3),
                Shirt = Random.Next(1, 3),
                Shoes = Random.Next(1, 3),
                Skin = Random.Next(1, 5)
            };

            var response = await Context.Inventory.TutorialSetAvatar(avatar);

            if (response.Status == SetAvatarResponse.Types.Status.Success)
            {
                await RandomDelay(10000, 30000);

                var result = await Context.Inventory.TutorialMarkComplete(TutorialState.AvatarSelection, _playerProfile.PlayerData.ContactSettings.SendMarketingEmails, _playerProfile.PlayerData.ContactSettings.SendPushNotifications);

                if (result.Success)
                {
                    //remove cached tutorial entry, so we do not try again before player data is updated.
                    _playerProfile.PlayerData = result.PlayerData;

                    Logger.Write($"Completed AVATAR_SELECTION tutorial.", LogLevel.Tutorial);
                }
                else
                {
                    Logger.Write($"Could not complete the AVATAR_SELECTION tutorial. TutorialMarkComplete:{response.Status}", LogLevel.Error);
                }
            }

            else
            {
                Logger.Write($"Could not complete the AVATAR_SELECTION tutorial. TutorialSetAvatar:{response.Status}", LogLevel.Error);
            }

            if (Context.Settings.ShowDebugMessages)
                Logger.Write($"Backpack:{avatar.Backpack}|Eyes:{avatar.Eyes}|Gender:{avatar.Gender}|Hair:{avatar.Hair}|Hat:{avatar.Hat}|Pants:{avatar.Pants}|Shirt:{avatar.Shirt}|Shoes:{avatar.Shoes}|Skin:{avatar.Skin}", LogLevel.Debug);

            await RandomDelay(5000, 10000);
        }

        private void ProcessCaptureAward(CaptureAward awards)
        {
            if (awards == null) return;
            if (awards.Xp.Count > 0)
                foreach (var i in awards.Xp)
                {
                    if (i > 0) Logger.Write($"Received {i} Xp!", LogLevel.Info);
                    Context.Statistics.AddExperience(i);
                }

            if (awards.Candy.Count > 0)
                foreach (var i in awards.Candy)
                    if (i > 0) Logger.Write($"Received {i} Candy!", LogLevel.Info);
            if (awards.Stardust.Count > 0)
                foreach (var i in awards.Stardust)
                {
                    if (i > 0) Logger.Write($"Received {i} Stardust!", LogLevel.Info);
                }

        }

        public async Task TutorialCapture()
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(TutorialState.PokemonCapture)) return;
            tutorialAttempts.Add(TutorialState.PokemonCapture);

            //hummanize
            Logger.Write("We have not finished the POKEMON_CAPTURE tutorial...");
            await RandomDelay(10000, 30000);

            var result = await Context.Inventory.TutorialPokemonCapture(Context.Settings.TutorialPokmonId);

            if (result.Result == EncounterTutorialCompleteResponse.Types.Result.Success)
            {
                //remove cached tutorial entry, so we do not try again before player data is updated.
                _playerProfile.PlayerData.TutorialState.Remove(TutorialState.PokemonCapture);

                Logger.Write($"Completed the POKEMON_CAPTURE tutorial.", LogLevel.Tutorial);
                Logger.Write($"Received {Context.Utility.GetMinStats(result.PokemonData)}", LogLevel.Pokemon);
                ProcessCaptureAward(result.CaptureAward);

                //hummanize
                Logger.Write("Now waiting for the pokedex entry...");
                await RandomDelay(10000, 30000);
            }
            else
            {
                Logger.Write($"Could not complete the POKEMON_CAPTURE tutorial. {result.Result}.", LogLevel.Error);
            }
            await RandomDelay(10000, 20000);
        }

        public async Task TutorialSetCodename(bool silent = false)
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(TutorialState.NameSelection)) return;
            tutorialAttempts.Add(TutorialState.NameSelection);

            //hummanize
            if (!silent) Logger.Write("We have not finished the NAME_SELECTION tutorial...");
            await RandomDelay(3000, 6000);

            ////////////////////////////////////////
            //  determine a valid available name  //
            ////////////////////////////////////////

            var nameFound = false;
            var nameOwned = false;

            //get desired name in settings (if there is one)
            var name = Context.Settings.TutorialCodename;

            if (!String.IsNullOrEmpty(name))
            {
                //check desired name
                var nameCheck = await Context.Inventory.CheckCodenameAvailable(name);
                //name is available
                if (nameCheck.Status == CheckCodenameAvailableResponse.Types.Status.Success)
                {
                    nameFound = true;
                }
                //name is not available
                else if (nameCheck.Status == CheckCodenameAvailableResponse.Types.Status.CurrentOwner)
                {
                    nameFound = true;
                    nameOwned = true;
                }
            }

            //try the username
            if (!nameFound)
            {
                //set
                name = Context.Settings.Username;
                if (name.Contains("@")) name = name.Substring(0, name.IndexOf("@"));
                //check desired name
                var nameCheck = await Context.Inventory.CheckCodenameAvailable(name);
                //name is available
                if (nameCheck.Status == CheckCodenameAvailableResponse.Types.Status.Success)
                {
                    nameFound = true;
                }
                //name is not available
                else if (nameCheck.Status == CheckCodenameAvailableResponse.Types.Status.CurrentOwner)
                {
                    nameFound = true;
                    nameOwned = true;
                }
                else
                { await RandomDelay(2000, 4000); }
            }

            //if we still don't have a verified name, let's try a name suggestion
            if (!nameFound)
            {
                var suggestedNames = await Context.Inventory.TutorialGetSuggestedCodenames();
                if (suggestedNames.Success && suggestedNames.Codenames != null && suggestedNames.Codenames.Count > 0)
                {
                    if (suggestedNames.Codenames.Count() > 1)
                    {
                        var randomIndex = Random.Next(0, suggestedNames.Codenames.Count - 1);
                        name = suggestedNames.Codenames[randomIndex];
                    }
                    else
                    {
                        name = suggestedNames.Codenames[0];
                    }
                    nameFound = true;
                }
                else
                { await RandomDelay(2000, 4000); }
            }

            //still no name? make one up incrementally
            if (!nameFound)
            {
                var baseName = Context.Settings.Username;
                if (baseName.Contains("@")) baseName = baseName.Substring(0, baseName.IndexOf("@"));
                if (!string.IsNullOrEmpty(baseName))
                {
                    for (int i = 2; i < 100; i++)
                    {
                        //set
                        name = baseName + i.ToString();

                        //check desired name
                        var nameCheck = await Context.Inventory.CheckCodenameAvailable(name);

                        //name is available
                        if (nameCheck.Status == CheckCodenameAvailableResponse.Types.Status.Success)
                        {
                            nameFound = true;
                            break;
                        }
                        //name is not available
                        else if (nameCheck.Status == CheckCodenameAvailableResponse.Types.Status.CurrentOwner)
                        {
                            nameFound = true;
                            nameOwned = true;
                            break;
                        }
                        else
                        { await RandomDelay(2000, 4000); }
                    }
                }
            }

            //still no name? make one up incrementally
            if (!nameFound)
            {
                do
                {
                    name = Guid.NewGuid().ToString().Replace("{", "").Replace("}", "").Replace("-", "").Substring(Random.Next(9, 12));

                    //check desired name
                    var nameCheck = await Context.Inventory.CheckCodenameAvailable(name);

                    //name is available
                    if (nameCheck.Status == CheckCodenameAvailableResponse.Types.Status.Success)
                    {
                        nameFound = true;
                        break;
                    }
                    else
                    { await RandomDelay(2000, 4000); }

                } while (!nameFound);

            }

            //if we have found a valid unregistered name, let's do it!
            if (nameFound && !nameOwned && !string.IsNullOrWhiteSpace(name))
            {
                var response = await Context.Client.Misc.ClaimCodename(name);

                if (response.Status == ClaimCodenameResponse.Types.Status.Success || response.Status == ClaimCodenameResponse.Types.Status.CurrentOwner)
                {
                    if (!silent) Logger.Write($"Name claimed : {name}", LogLevel.Tutorial);
                    nameOwned = true;
                    await RandomDelay();
                }
            }


            var result = await Context.Inventory.TutorialMarkComplete(TutorialState.NameSelection, _playerProfile.PlayerData.ContactSettings.SendMarketingEmails, _playerProfile.PlayerData.ContactSettings.SendPushNotifications);
            if (result.Success)
            {
                //remove cached tutorial entry, so we do not try again before player data is updated.
                _playerProfile.PlayerData = result.PlayerData;
                if (!silent) Logger.Write($"Completed the NAME_SELECTION tutorial.", LogLevel.Tutorial);
            }
            else
            {
                if (!silent) Logger.Write($"We could not complete the NAME_SELECTION tutorial.", LogLevel.Error);
            }

        }

        #endregion
    }

}