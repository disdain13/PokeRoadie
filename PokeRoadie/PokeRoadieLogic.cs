#region " Imports "

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;

using PokemonGo.RocketAPI;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.Helpers;
using PokemonGo.RocketAPI.Logging;
using PokemonGo.RocketAPI.Exceptions;

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
        public event Action<ItemId,int> OnRecycleItems;
        public event Action OnLuckyEggActive;
        public event Action OnIncenseActive;
        public event Action<ItemId,PokemonData> OnUsePotion;
        public event Action<ItemId, PokemonData> OnUseRevive;
        public event Action<IncubatorData, PokemonData> OnEggHatched;
        public event Action<EggIncubator> OnUseIncubator;
        public event Action<EggIncubator> OnIncubatorStatus;

        //used to raise syncronized events
        private bool RaiseSyncEvent(Delegate method, params object[] args)
        {
            if (method == null || _invoker == null || !_invoker.InvokeRequired) return false;
            _invoker.Invoke(method, args);
            return true;
        }

        #endregion
        #region " Static Members "

        private static string tempDir = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
        private static string configsDir = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
        private static string pokestopsDir = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Pokestops");
        private static string encountersDir = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Encounters");
        private static string gymDir = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Gyms");
        private static string eggDir = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Eggs");

        private object xloLock = new object();
        private int xloCount = 0;
        private volatile bool isRunning;
        //private volatile bool inFlight = false;
        private volatile bool inTravel = false;

        #endregion
        #region " Primary Objects "

        private readonly PokeRoadieClient _client;
        private readonly PokeRoadieInventory _inventory;
        private readonly Statistics _stats;
        private readonly PokeRoadieNavigation _navigation;
        private readonly PokeRoadieSettings _settings;

        #endregion
        #region " Members "

        private ISynchronizeInvoke _invoker;
        private DateTime? _nextLuckyEggTime;
        private DateTime? _nextIncenseTime;
        private DateTime? _nextExportTime;
        public DateTime? _nextWriteStatsTime;
        private GetPlayerResponse _playerProfile;
        private int recycleCounter = 0;
        private bool IsInitialized = false;
        private int fleeCounter = 0;
        private DateTime? fleeEndTime;
        private DateTime? fleeStartTime;
        private bool softBan = false;
        private bool hasDisplayedConfigSettings;
        private ApiFailureStrategy _apiFailureStrategy;
        private List<string> gymTries = new List<string>();
        private ulong lastEnconterId = 0;
        private Random Random = new Random(DateTime.Now.Millisecond);
        private ulong lastMissedEncounterId = 0;
        private int locationAttemptCount = 0;
        private DateTime? nextTransEvoPowTime;
        private List<TutorialState> tutorialAttempts = new List<TutorialState>();
        private DateTime noWorkTimer = DateTime.Now;
        private DateTime mapsTimer = DateTime.Now;
        private GetMapObjectsResponse _map = null;
        private List<ulong> _recentEncounters = new List<ulong>();
        #endregion
        #region " Helper Properties "

        public bool CanCatch { get { return _settings.CatchPokemon && _settings.Session.CatchEnabled && !softBan && _navigation.LastKnownSpeed <= _settings.MaxCatchSpeed && noWorkTimer <= DateTime.Now; } }
        public bool CanVisit { get { return _settings.VisitPokestops && _settings.Session.VisitEnabled && !softBan; } }
        public bool CanVisitGyms { get { return _settings.VisitGyms && _stats.Currentlevel > 4 && !softBan; } }
        public async Task<GetMapObjectsResponse> GetMapObjects()
        {
            if (_map == null || mapsTimer <= DateTime.Now)
            {
                var objects = await _client.Map.GetMapObjects();
                //if (_settings.ShowDebugMessages) Logger.Write("Map objects pull made from server", LogLevel.Debug);
                if (objects != null && objects.Item1 != null)
                {
                    mapsTimer = DateTime.Now.AddMilliseconds(3000);
                    _map = objects.Item1;

                }
                else
                    mapsTimer = DateTime.Now.AddMilliseconds(5000);
            }
            return _map;
        }

        #endregion
        #region " Constructors "

        public PokeRoadieLogic() : this(null)
        {
            //check temp dir
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            //check pokestop dir
            if (!Directory.Exists(pokestopsDir)) Directory.CreateDirectory(pokestopsDir);
            //check gym dir
            if (!Directory.Exists(gymDir)) Directory.CreateDirectory(gymDir);
            //check egg dir
            if (!Directory.Exists(eggDir)) Directory.CreateDirectory(eggDir);
            //check encounters dir
            if (!Directory.Exists(encountersDir)) Directory.CreateDirectory(encountersDir);
        }

        public PokeRoadieLogic(ISynchronizeInvoke form) : base()
        {
            _invoker = form;
            _settings = PokeRoadieSettings.Current;
            _apiFailureStrategy = new ApiFailureStrategy();
            _client = new PokeRoadieClient(_settings, _apiFailureStrategy);
            _apiFailureStrategy.Client = _client;
            _inventory = new PokeRoadieInventory(_client, _settings);
            _stats = new Statistics(_inventory);
            _navigation = new PokeRoadieNavigation(_client);
            _navigation.OnChangeLocation += RelayLocation;
        }

        #endregion
        #region " Application Methods "

        public void Stop()
        {
            isRunning = false;
        }

        private async Task CloseApplication(int exitCode)
        {
            for (int i = 3; i > 0; i--)
            {
                Logger.Write($"PokeRoadie will be closed in {i * 5} seconds!", LogLevel.Warning);
                await Task.Delay(5000);
            }
            await Task.Delay(15000);
            System.Environment.Exit(exitCode);
        }

        #endregion
        #region " Maintenance/Utility Methods "

        private void Maintenance()
        {

            //delete old temp files
            DeleteOldFiles(pokestopsDir);

            //clear old temp files
            DeleteOldFiles(gymDir);

            //run temp data serializer on own thread
            Task.Run(new Action(Xlo));

        }

        private void DeleteOldFiles(string dir)
        {
            if (!Directory.Exists(dir)) return;
            var files = Directory.GetFiles(dir).Where(x=>x.EndsWith(".txt")).ToList();
            foreach (var file in files)
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    //ignore
                }
        }

        private async Task Export()
        {
            if (!_nextExportTime.HasValue || _nextExportTime.Value < DateTime.Now)
            {
                _nextExportTime = DateTime.Now.AddMinutes(5);
                await _inventory.ExportPokemonToCSV(_playerProfile.PlayerData);
            }
        }

        private async Task WriteStats()
        {
            if (!_nextWriteStatsTime.HasValue || _nextWriteStatsTime.Value <= DateTime.Now)
            {
                await PokeRoadieInventory.GetCachedInventory(_client);
                _playerProfile = await _client.Player.GetPlayer();
                var playerName = _stats.GetUsername(_client, _playerProfile);
                _stats.UpdateConsoleTitle(_client, _inventory);
                var currentLevelInfos = await _stats._getcurrentLevelInfos(_inventory);
                //get all ordered by id, then cp
                var allPokemon = (await _inventory.GetPokemons()).OrderBy(x => x.PokemonId).ThenByDescending(x => x.Cp).ToList();

                Logger.Write("====== User Info ======", LogLevel.None, ConsoleColor.Yellow);
                Logger.Write($"Name: {playerName}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Team: {_playerProfile.PlayerData.Team}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Level: {currentLevelInfos}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Pokemon: {allPokemon.Count}", LogLevel.None, ConsoleColor.White);
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
                if (_settings.ShowDebugMessages)
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

                var items = await _inventory.GetItems();
                Logger.Write($"====== Items ({items.Select(x => x.Count).Sum()}) ======", LogLevel.None, ConsoleColor.Yellow);
                foreach (var item in items)
                {
                    Logger.Write($"{(item.ItemId).ToString().Replace("Item", "")} x {item.Count}", LogLevel.None, ConsoleColor.White);
                }

                
                if (!hasDisplayedConfigSettings)
                {
                    hasDisplayedConfigSettings = true;


                    //write transfer settings
                    if (_settings.TransferPokemon)
                    {
                        Logger.Write("====== Transfer Settings ======", LogLevel.None, ConsoleColor.Yellow);
                        Logger.Write($"{("Keep Above CP:").PadRight(25)}{_settings.KeepAboveCP}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Keep Above IV:").PadRight(25)}{_settings.KeepAboveLV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Keep Above IV:").PadRight(25)}{_settings.KeepAboveIV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Keep Above V:").PadRight(25)}{_settings.KeepAboveV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Transfer Below CP:").PadRight(25)}{_settings.AlwaysTransferBelowCp}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Transfer Below IV:").PadRight(25)}{_settings.AlwaysTransferBelowIV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Transfer Below IV:").PadRight(25)}{_settings.AlwaysTransferBelowLV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Transfer Below V:").PadRight(25)}{_settings.AlwaysTransferBelowV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Transfer Evolvable:").PadRight(25)}{!_settings.NotTransferPokemonsThatCanEvolve}", LogLevel.None, ConsoleColor.White);
                        if (_settings.PokemonsNotToTransfer.Count > 0)
                        {
                            Logger.Write($"{("Pokemons Not To Transfer:").PadRight(25)} {_settings.PokemonsNotToTransfer.Count}", LogLevel.None, ConsoleColor.White);
                            foreach (PokemonId i in _settings.PokemonsNotToTransfer)
                            {
                                Logger.Write(i.ToString(), LogLevel.None, ConsoleColor.White);
                            }
                        }
                    }


                    //write evolution settings
                    if (_settings.EvolvePokemon)
                    {
                        Logger.Write("====== Evolution Settings ======", LogLevel.None, ConsoleColor.Yellow);
                        Logger.Write($"{("Evolve Above CP:").PadRight(25)}{_settings.EvolveAboveCp}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Evolve Above IV:").PadRight(25)}{_settings.EvolveAboveIV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Evolve Above V:").PadRight(25)}{_settings.EvolveAboveV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Use Evolution List:").PadRight(25)}{_settings.UsePokemonsToEvolveList}", LogLevel.None, ConsoleColor.White);
                        if (_settings.UsePokemonsToEvolveList && _settings.PokemonsToEvolve.Count > 0)
                        {
                            Logger.Write($"{("Pokemons To Evolve:").PadRight(25)} {_settings.PokemonsToEvolve.Count}", LogLevel.None, ConsoleColor.White);
                            foreach (PokemonId i in _settings.PokemonsToEvolve)
                            {
                                Logger.Write(i.ToString(), LogLevel.None, ConsoleColor.White);
                            }
                        }
                    }
                  
                    
                    //write powerup settings
                    if (_settings.PowerUpPokemon)
                    {
                        Logger.Write("====== Power-Up Settings ======", LogLevel.None, ConsoleColor.Yellow);
                        Logger.Write($"{("Power-Up Above CP:").PadRight(25)}{_settings.PowerUpAboveCp}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Power-Up Above IV:").PadRight(25)}{_settings.PowerUpAboveIV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Power-Up Above V:").PadRight(25)}{_settings.PowerUpAboveV}", LogLevel.None, ConsoleColor.White);
                        Logger.Write($"{("Use Power-Up List:").PadRight(25)}{_settings.UsePokemonsToPowerUpList}", LogLevel.None, ConsoleColor.White);
                        if (_settings.UsePokemonsToPowerUpList && _settings.PokemonsToPowerUp.Count > 0)
                        {
                            Logger.Write($"{("Pokemons To Power-up:").PadRight(25)} {_settings.PokemonsToPowerUp.Count}", LogLevel.None, ConsoleColor.White);
                            foreach (PokemonId i in _settings.PokemonsToPowerUp)
                            {
                                Logger.Write(i.ToString(), LogLevel.None, ConsoleColor.White);
                            }
                        }
                    }

                }


                if (_settings.DestinationsEnabled && _settings.Destinations != null && _settings.Destinations.Count > 0)
                {
                    Logger.Write("====== Destinations ======", LogLevel.None, ConsoleColor.Yellow);
                    LocationData lastDestination = null;
                    for (int i = 0; i < _settings.Destinations.Count; i++)
                    {
                        var destination = _settings.Destinations[i];
                        var str = $"{i} - {destination.Name} - {Math.Round(destination.Latitude,5)}:{Math.Round(destination.Longitude,5)}:{Math.Round(destination.Altitude,5)}";
                        if (_settings.DestinationIndex < i)
                        {
                            if (lastDestination != null)
                            {

                                var sourceLocation = new GeoCoordinate(lastDestination.Latitude, lastDestination.Longitude, lastDestination.Altitude);
                                var targetLocation = new GeoCoordinate(destination.Latitude, destination.Longitude, destination.Altitude);
                                var distanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);
                                var speed = _settings.LongDistanceSpeed;
                                var speedInMetersPerSecond = speed / 3.6;
                                var seconds = distanceToTarget / speedInMetersPerSecond;
                                var action = "driving";
                                str += " (";
                                str += StringUtils.GetSecondsDisplay(seconds);
                                str += $" {action} at {speed}kmh)";

                            }
                        }
                        else if (_settings.DestinationIndex == i)
                        {
                            str += " <-- You Are Here!";
                        }
                        else
                        {
                            str += " (Visited)";
                        }
                        Logger.Write(str, LogLevel.None, _settings.DestinationIndex == i ? ConsoleColor.Red : _settings.DestinationIndex < i ? ConsoleColor.White : ConsoleColor.DarkGray);
                        lastDestination = destination;
                    }
                }
                            
                //write top candy list
			    Logger.Write("====== Top Candies ======", LogLevel.None, ConsoleColor.Yellow);
			    var highestsPokemonCandy = await _inventory.GetHighestsCandies(_settings.DisplayTopCandy);
			    foreach (var candy in highestsPokemonCandy)
			    {
				    Logger.Write($"{candy.FamilyId.ToString().Replace("Family", "").PadRight(19,' ')} Candy: { candy.Candy_ }", LogLevel.None, ConsoleColor.White);
			    }                
                
                
                Logger.Write("====== Most Valuable ======", LogLevel.None, ConsoleColor.Yellow);
                var highestsPokemonV = await _inventory.GetHighestsV(_settings.DisplayPokemonCount);
                foreach (var pokemon in highestsPokemonV) {
                    Logger.Write(pokemon.GetStats(), LogLevel.None, ConsoleColor.White);
                }
                
                
                Logger.Write("====== Highest CP ======", LogLevel.None, ConsoleColor.Yellow);
                var highestsPokemonCp = await _inventory.GetHighestsCP(_settings.DisplayPokemonCount);
                foreach (var pokemon in highestsPokemonCp) {
                    Logger.Write(pokemon.GetStats(), LogLevel.None, ConsoleColor.White);
                }
                
                
                Logger.Write("====== Most Perfect Genetics ======", LogLevel.None, ConsoleColor.Yellow);
                var highestsPokemonPerfect = await _inventory.GetHighestsPerfect(_settings.DisplayPokemonCount);
                foreach (var pokemon in highestsPokemonPerfect)
                {
                    Logger.Write(pokemon.GetStats(), LogLevel.None, ConsoleColor.White);
                }
                
                
                if (_settings.DisplayAllPokemonInLog)
                {
                    Logger.Write("====== Full List ======", LogLevel.None, ConsoleColor.Yellow);
                    foreach (var pokemon in allPokemon.OrderBy(x => x.PokemonId).ThenByDescending(x => x.Cp))
                    {
                        Logger.Write(pokemon.GetStats(), LogLevel.None, ConsoleColor.White);
                    }
                }
                if (_settings.DisplayAggregateLog)
                {
                    Logger.Write("====== Aggregate Data ======", LogLevel.None, ConsoleColor.Yellow);
                    Logger.Write($"{allPokemon.Count} Total Pokemon", LogLevel.None, ConsoleColor.White);
                    Logger.Write("====== Cp ======", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"< 100 Cp: {allPokemon.Where(x => x.Cp < 100).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"100-499 Cp: {allPokemon.Where(x => x.Cp >= 100 && x.Cp < 500).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"500-999 Cp: {allPokemon.Where(x => x.Cp >= 500 && x.Cp < 1000).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"1000-1499 Cp: {allPokemon.Where(x => x.Cp >= 1000 && x.Cp < 1500).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"> 1499 Cp: {allPokemon.Where(x => x.Cp >= 1500).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write("====== IV ======", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"24% or less: {allPokemon.Where(x => x.GetPerfection() < 25).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"25%-49%: {allPokemon.Where(x => x.GetPerfection() > 24 && x.GetPerfection() < 50).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"50%-74%: {allPokemon.Where(x => x.GetPerfection() > 49 && x.GetPerfection() < 75).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"75%-89%: {allPokemon.Where(x => x.GetPerfection() > 74 && x.GetPerfection() < 90).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"90%-100%: {allPokemon.Where(x => x.GetPerfection() > 89).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write("====== V ======", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"< 100 Cp: {allPokemon.Where(x => x.CalculatePokemonValue() < 100).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"100-199 Cp: {allPokemon.Where(x => x.CalculatePokemonValue() >= 100 && x.CalculatePokemonValue() < 200).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"200-299 Cp: {allPokemon.Where(x => x.CalculatePokemonValue() >= 200 && x.CalculatePokemonValue() < 300).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"300-399 Cp: {allPokemon.Where(x => x.CalculatePokemonValue() >= 300 && x.CalculatePokemonValue() < 400).Count()}", LogLevel.None, ConsoleColor.White);
                    Logger.Write($"> 400 Cp: {allPokemon.Where(x => x.CalculatePokemonValue() >= 400).Count()}", LogLevel.None, ConsoleColor.White);
                }

                _nextWriteStatsTime = DateTime.Now.AddMinutes(_settings.DisplayRefreshMinutes);
            }

        }

        public void Xlo()
        {
            if (xloCount > 0) return;
            lock (xloLock)
            {
                xloCount++;

                if (!isRunning) return;
                if (Directory.Exists(pokestopsDir))
                {
                    var files = Directory.GetFiles(pokestopsDir)
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

                if (Directory.Exists(gymDir))
                {
                    var files = Directory.GetFiles(gymDir)
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
                                    var gym = (Xml.Gym)Xml.Serializer.DeserializeFromFile(filePath, typeof(Xml.Gym));
                                    var f = Xml.Serializer.Xlo(gym, info.CreationTime);
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

                if (Directory.Exists(encountersDir))
                {
                    var files = Directory.GetFiles(encountersDir)
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
            await RandomHelper.RandomDelay(_settings.MinDelay, _settings.MaxDelay);
        }

        private async Task RandomDelay(int min, int max)
        {
            await RandomHelper.RandomDelay(min, max);
        }

        private async Task CheckSession()
        {
            var maxTimespan = TimeSpan.Parse(_settings.MaxRunTimespan);
            var minBreakTimespan = TimeSpan.Parse(_settings.MinBreakTimespan);
            var nowdate = DateTime.Now;
            var session = _settings.Session;
            var endDate = session.StartDate.Add(maxTimespan);
            var totalEndDate = endDate.Add(minBreakTimespan);

            //session is still active
            if (session.PlayerName == _playerProfile.PlayerData.Username && endDate > nowdate)
            {
                if (_settings.Session.CatchEnabled && _settings.Session.CatchCount >= _settings.MaxPokemonCatches)
                {
                    _settings.Session.CatchEnabled = false;
                    Logger.Write($"Limit reached! The bot caught {_settings.Session.CatchCount} pokemon since {session.StartDate}.", LogLevel.Warning);
                }
                if (_settings.Session.VisitEnabled && _settings.Session.VisitCount >= _settings.MaxPokestopVisits)
                {
                    _settings.Session.VisitEnabled = false;
                    Logger.Write($"Limit reached! The bot visited {_settings.Session.VisitCount} pokestops since {session.StartDate}.", LogLevel.Warning);
                }
                if (!_settings.Session.CatchEnabled && !_settings.Session.VisitEnabled)
                {
                    var diff = totalEndDate.Subtract(nowdate);
                    Logger.Write($"All limits reached! The bot visited {_settings.Session.VisitCount} pokestops, and caught {_settings.Session.CatchCount} pokemon since {session.StartDate}. The bot will wait until {totalEndDate.ToShortTimeString()} to continue...", LogLevel.Warning);
                    await Task.Delay(diff);
                    _settings.Session = _settings.NewSession();
                }
                return;
            }

            //session has expired
            if (totalEndDate < nowdate)
            {
                var s = _settings.NewSession();
                s.PlayerName = _playerProfile.PlayerData.Username;
                _settings.Session = s;
                return;
            }

            //session expired, but break not completed   
            if (endDate < nowdate && totalEndDate > nowdate)
            {
                //must wait the difference before start
                var diff = totalEndDate.Subtract(nowdate);
                Logger.Write($"Your last recorded session ended {endDate.ToShortTimeString()}, but the required break time has not passed. The bot will wait until {totalEndDate.ToShortTimeString()} to continue...", LogLevel.Warning);
                await Task.Delay(diff);
                _settings.Session = _settings.NewSession();
            }
        }

        public static bool CheckForInternetConnection()
        {
            var hasConnection = false;
            var c = 0;
            while (!hasConnection && c < 120)
            {
                c++; //i like writing that
                try
                {
                    using (var client = new WebClient())
                    {
                        using (var stream = client.OpenRead("http://www.google.com"))
                        {
                            hasConnection= true;
                            break;
                        }
                    }
                }
                catch
                {
                    hasConnection = false;
                }

                if (!hasConnection)
                {
                    switch (c)
                    {
                        case 1:
                            Logger.Write("Lost internet connection, waiting to re-establish...", LogLevel.Warning);
                            break;
                        default:
                            Logger.Append(".");
                            break;
                    }
                }
                else
                {
                    if (c > 1)
                    {
                        Logger.Write("Internet connection re-established!", LogLevel.Warning);
                    }
                }

                var i = 0;
                while(i<30)
                {
                    i++;
                    System.Threading.Thread.Sleep(1000);
                }

            }
            return hasConnection; 

        }

        #endregion
        #region " Navigation Methods "

        private List<GpxReader.Trk> GetGpxTracks()
        {
            var xmlString = File.ReadAllText(_settings.GPXFile);
            var readgpx = new GpxReader(xmlString);
            return readgpx.Tracks;
        }


        private void RelayLocation(LocationData location)
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

        public async Task Execute()
        {

            //check version
            Git.CheckVersion();

            //flag as running
            if (!isRunning)
                isRunning = true;

            //check lat long
            if (_settings.CurrentLongitude == 0 && _settings.CurrentLatitude == 0)
            {
                Logger.Write("CurrentLatitude and CurrentLongitude not set in the Configs/Settings.xml. Application will exit in 15 seconds...", LogLevel.Error);
                if (_settings.MoveWhenNoStops && _client != null) _settings.DestinationEndDate = DateTime.Now;
                await CloseApplication(1);
            }

            //do maint
            Maintenance();

            Logger.Write($"Logging in via: {_settings.AuthType}", LogLevel.Info);
            while (isRunning)
            {
                try
                {
                    await _client.Login.DoLogin();
                    await PostLoginExecute();
                }
                catch(PtcOfflineException e)
                {
                    var eMessage = e.Message;
                    Logger.Write($"(LOGIN ERROR) The Ptc servers are currently offline - {eMessage}. Waiting 30 seconds... ", LogLevel.None, ConsoleColor.Red);
                    await Task.Delay(30000);
                    e = null;
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("NeedsBrowser"))
                    {
                        Logger.Write("(LOGIN ERROR) Please login to your google account and turn off 'Two-Step Authentication' under security settings. If you do NOT want to disable your two-factor auth, please visit the following link and setup an app password. This is the only way of using the bot without disabling two-factor authentication: https://security.google.com/settings/security/apppasswords. Trying automatic restart in 15 seconds...", LogLevel.None, ConsoleColor.Red);
                        await Task.Delay(15000);
                    }
                    else if (e.Message.Contains("BadAuthentication") || e is LoginFailedException)
                    {
                        Logger.Write("(LOGIN ERROR) The username and password provided failed. " + e.Message, LogLevel.None, ConsoleColor.Red);
                        //raise event
                        if (OnPromptForCredentials != null)
                        {
                            //raise event
                            bool result = false;
                            if (_invoker != null && _invoker.InvokeRequired)
                                result = (bool)_invoker.Invoke(OnPromptForCredentials, new object[] { });
                            else
                                result = OnPromptForCredentials.Invoke();

                            if (!result)
                            {
                                Logger.Write("Username and password for login not provided. Login screen closed.");
                                await CloseApplication(0);
                            }
                        }
                    }
                    else if (e.Message.Contains("Object reference"))
                    {
                        Logger.Write($"(PGO SERVER) It appears the PokemonGo servers are down, or not taking our requests. Let's wait one minute.", LogLevel.None, ConsoleColor.Red);
                        await Task.Delay(60000);
                    }
                    else
                    {
                        Logger.Write($"(FATAL ERROR) Unhandled exception encountered: {e.Message.ToString()}.", LogLevel.None, ConsoleColor.Red);
                        Logger.Write("Restarting the application due to error...", LogLevel.Warning);
                        await Task.Delay(15000);
                    }
                    await Execute();
                }          
            }
            isRunning = false;
        }

        public async Task ProcessPeriodicals()
        {
            //only do this once, calling this 14 times every iteration could be
            //detectable for banning
            await PokeRoadieInventory.GetCachedInventory(_client);

            //write stats
            await WriteStats();

            //session
            await CheckSession();

            //handle tutorials - pissed this is not working
            //await CompleteTutorials();

            //pickup bonuses
            if (_settings.PickupDailyDefenderBonuses)
                await PickupBonuses();

            //revive
            if (_settings.UseRevives) await UseRevives();

            //heal
            if (_settings.UsePotions) await UsePotions();

            //egg incubators
            await UseIncubators(!_settings.UseEggIncubators);

            //delay transfer/power ups/evolutions with a 5 minute window unless needed.
            var pokemonCount = (await _inventory.GetPokemons()).Count();
            var maxPokemonCount = _playerProfile.PlayerData.MaxPokemonStorage;
            if (maxPokemonCount - pokemonCount < 20 ||  !nextTransEvoPowTime.HasValue || nextTransEvoPowTime.Value <= DateTime.Now)
            {
                //evolve
                if (_settings.EvolvePokemon) await EvolvePokemon();

                //power up
                if (_settings.PowerUpPokemon) await PowerUpPokemon();

                //favorite
                if (_settings.FavoritePokemon) await FavoritePokemon();

                //transfer
                if (_settings.TransferPokemon) await TransferPokemon();
                nextTransEvoPowTime = DateTime.Now.AddMinutes(5);
            }


            //export
            await Export();

            //incense
            if (_settings.UseIncense) await UseIncense();

            //incense
            if (_settings.UseLuckyEggs) await UseLuckyEgg();

            //recycle
            if (recycleCounter >= 5)
            {
                await RecycleItems();
            }
                
        }

        public async Task PostLoginExecute()
        {
            Logger.Write($"Client logged in", LogLevel.Info);
            while (true)
            {

                if (!isRunning) break;
                if (!IsInitialized)
                {
                    await ProcessPeriodicals();
                }
                IsInitialized = true;
                await ExecuteFarming(_settings.UseGPXPathing);

                /*
                * Example calls below
                *
                var profile = await _client.GetProfile();
                var settings = await _client.GetSettings();
                var mapObjects = await _client.GetMapObjects();
                var inventory = await _client.GetInventory();
                var pokemons = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.Pokemon).Where(p => p != null && p?.PokemonId > 0);
                */

                //await Task.Delay(100);
            }
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
                            if (!isRunning) break;
                            var nextPoint = trackPoints.ElementAt(curTrkPt);
                            var distance_check = LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude,
                                _client.CurrentLongitude, Convert.ToDouble(nextPoint.Lat), Convert.ToDouble(nextPoint.Lon));

                            //if (distance_check > 5000)
                            //{
                            //    Logger.Write(
                            //        $"Your desired destination of {nextPoint.Lat}, {nextPoint.Lon} is too far from your current position of {_client.CurrentLatitude}, {_client.CurrentLongitude}",
                            //        LogLevel.Error);
                            //    break;
                            //}

                            //Logger.Write(
                            //    $"Your desired destination is {nextPoint.Lat}, {nextPoint.Lon} your location is {_client.CurrentLatitude}, {_client.CurrentLongitude}",
                            //    LogLevel.Warning);

                            //await CatchNearbyStops(true);
                            await _navigation.HumanPathWalking(
                                trackPoints.ElementAt(curTrkPt),
                                _settings.MinSpeed,
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

            if (!_settings.VisitGyms && !_settings.VisitPokestops)
            {
                Logger.Write("Both VisitGyms and VisitPokestops settings are false... Standing around I guess...");
                
            }

            var wayPointGeo = GetWaypointGeo();

            var distanceFromStart = LocationUtils.CalculateDistanceInMeters(
            _client.CurrentLatitude, _client.CurrentLongitude,
            wayPointGeo.Latitude, wayPointGeo.Longitude);


            // Edge case for when the client somehow ends up outside the defined radius
            if (_settings.MaxDistance != 0 &&
                distanceFromStart > _settings.MaxDistance)
            {
                inTravel = true;
                Logger.Write($"We have traveled outside the max distance of {_settings.MaxDistance}, returning to center at {wayPointGeo}", LogLevel.Navigation, ConsoleColor.White);
                await _navigation.HumanLikeWalking(wayPointGeo,  distanceFromStart > _settings.MaxDistance / 2 ? _settings.LongDistanceSpeed : _settings.MinSpeed, distanceFromStart > _settings.MaxDistance / 2 ? GetLongTask() : GetShortTask(),  distanceFromStart > _settings.MaxDistance / 2 ? false : true);
                gymTries.Clear();
                locationAttemptCount = 0;
                Logger.Write($"Arrived at center point {Math.Round(wayPointGeo.Latitude,5)}", LogLevel.Navigation);
                inTravel = false;
            }

            //if destinations are enabled
            if (_settings.DestinationsEnabled)
            {
                if (_settings.DestinationEndDate.HasValue)
                {
                    if (DateTime.Now > _settings.DestinationEndDate.Value)
                    {

                        if (_settings.Destinations != null && _settings.Destinations.Count > 1)
                        {
                            //get new destination index
                            var newIndex = _settings.DestinationIndex + 1 >= _settings.Destinations.Count ? 0 : _settings.DestinationIndex + 1;
                            //get coords
                            var destination = _settings.Destinations[newIndex];

                            //set new index and default location
                            _settings.DestinationIndex = newIndex;
                            _settings.WaypointLatitude = destination.Latitude;
                            _settings.WaypointLongitude = destination.Longitude;
                            _settings.WaypointAltitude = destination.Altitude;
                            _settings.DestinationEndDate = DateTime.Now.AddSeconds(distanceFromStart / (_settings.MinSpeed / 3.6)).AddMinutes(_settings.MinutesPerDestination);
                            _settings.Save();

                            //raise event
                            if (OnChangeDestination != null)
                            {
                                if (!RaiseSyncEvent(OnChangeDestination, destination, newIndex))
                                    OnChangeDestination(destination, newIndex);
                            }
                            inTravel = true;
                            Logger.Write($"Moving to new destination - {destination.Name} - {destination.Latitude}:{destination.Longitude}", LogLevel.Navigation, ConsoleColor.White);
                            Logger.Write("Preparing for long distance travel...", LogLevel.None, ConsoleColor.White);
                            await _navigation.HumanLikeWalking(destination.GetGeo(), _settings.LongDistanceSpeed, GetLongTask(), false);
                            Logger.Write($"Arrived at destination - {destination.Name}!", LogLevel.Navigation, ConsoleColor.White);
                            gymTries.Clear();
                            locationAttemptCount = 0;
                            inTravel = false;

                            //reset destination timer
                            _settings.DestinationEndDate = DateTime.Now.AddMinutes(_settings.MinutesPerDestination);

                           
                        }
                        else
                        {
                            _settings.DestinationEndDate = DateTime.Now.AddMinutes(_settings.MinutesPerDestination);
                        }
                    }
                }
                else
                {
                    _settings.DestinationEndDate = DateTime.Now.AddMinutes(_settings.MinutesPerDestination);
                }
            }
            //await CheckDestinations();


            var totalActivecount = 0;
            var mapObjects = await GetMapObjects();
            var dynamicDistance = _settings.MaxDistance + (locationAttemptCount * 1000);
            if (dynamicDistance > 10000) dynamicDistance = 10000;
            var pokeStopList = GetPokestops(GetCurrentLocation(), dynamicDistance, mapObjects);
            var gymsList = pokeStopList.Where(x => x.Type == FortType.Gym).ToList();
            var stopList = pokeStopList.Where(x => x.Type != FortType.Gym).ToList();
            var unvisitedGymList = gymsList.Where(x => !gymTries.Contains(x.Id)).ToList();
            if (_settings.VisitGyms) totalActivecount += unvisitedGymList.Count;
            if (_settings.VisitPokestops) totalActivecount += stopList.Count;

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

                if (locationAttemptCount >= _settings.MaxLocationAttempts)
                {
                   
                    if (_settings.DestinationsEnabled && _settings.MoveWhenNoStops)
                    {
                        Logger.Write("Setting new destination...", LogLevel.Info);
                        _settings.DestinationEndDate = DateTime.Now;
                    }
                    else
                    {
                        if (_settings.EnableWandering && distanceFromStart < _settings.MaxDistance)
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
                            await _navigation.HumanLikeWalking(current, _settings.MinSpeed, GetLongTask(), false);
                        }
                        else
                        {
                            
                            inTravel = true;
                            Logger.Write($"Since there are no locations, let's go back to the waypoint center {wayPointGeo} {distanceFromStart}m", LogLevel.Navigation, ConsoleColor.White);
                            await _navigation.HumanLikeWalking(wayPointGeo,  distanceFromStart > _settings.MaxDistance / 2 ? _settings.LongDistanceSpeed : _settings.MinSpeed, distanceFromStart > _settings.MaxDistance / 2 ? GetLongTask() : GetShortTask(),  distanceFromStart > _settings.MaxDistance / 2 ? false : true);
                            gymTries.Clear();
                            locationAttemptCount = 0;
                            Logger.Write($"Arrived at center point {Math.Round(wayPointGeo.Latitude, 5)}", LogLevel.Navigation);
                            inTravel = false;
                        }
                    }
                }
                await RandomDelay(_settings.LocationsMinDelay, _settings.LocationsMaxDelay);
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
            var fullPokestopList = PokeRoadieNavigation.PathByNearestNeighbour(
                mapObjects.MapCells.SelectMany(i => i.Forts)
                    .Where(i =>
                        (maxDistance == 0 ||
                        LocationUtils.CalculateDistanceInMeters(location.Latitude, location.Longitude, i.Latitude, i.Longitude) < maxDistance))
                    .OrderBy(i =>
                         LocationUtils.CalculateDistanceInMeters(location.Latitude, location.Longitude, i.Latitude, i.Longitude)).ToArray());

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

            var pokeStopList = _settings.IncludeHotPokestops ?
                fullPokestopList :
                fullPokestopList.Where(i => i.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime());

            if (!CanVisitGyms)
                pokeStopList = pokeStopList.Where(x => x.Type != FortType.Gym);

            if (!_settings.VisitPokestops)
                pokeStopList = pokeStopList.Where(x => x.Type == FortType.Gym);

            return pokeStopList.ToList();
        }

        private async Task ProcessNearby(GetMapObjectsResponse mapObjects)
        {

            //incense pokemon
            if (CanCatch && _settings.UseIncense && (_nextIncenseTime.HasValue && _nextIncenseTime.Value >= DateTime.Now))
            {
                var incenseRequest = await _client.Map.GetIncensePokemons();
                if (incenseRequest.Result == GetIncensePokemonResponse.Types.Result.IncenseEncounterAvailable)
                {
                    if (!_recentEncounters.Contains(incenseRequest.EncounterId) && (!_settings.UsePokemonToNotCatchList || !_settings.PokemonsNotToCatch.Contains(incenseRequest.PokemonId)))
                    {
                        _recentEncounters.Add(incenseRequest.EncounterId);
                        await ProcessIncenseEncounter(new LocationData(incenseRequest.Latitude, incenseRequest.Longitude, _client.CurrentAltitude), incenseRequest.EncounterId, incenseRequest.EncounterLocation);
                    }
                }
            }

            //wild pokemon
            var pokemons =
                mapObjects.MapCells.SelectMany(i => i.CatchablePokemons)
                .Where(x=> !_recentEncounters.Contains(x.EncounterId))
                .OrderBy(i => LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, i.Latitude, i.Longitude));

            //filter out not to catch list
            if (_settings.UsePokemonToNotCatchList)
                pokemons = pokemons.Where(p => !_settings.PokemonsNotToCatch.Contains(p.PokemonId)).OrderBy(i => LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, i.Latitude, i.Longitude));

            //clean up old recent encounters
            while (_recentEncounters != null && _recentEncounters.Count > 100)
             _recentEncounters.RemoveAt(0);

            if (pokemons == null || !pokemons.Any()) return;
            Logger.Write($"Found {pokemons.Count()} catchable Pokemon", LogLevel.Info);

            foreach (var pokemon in pokemons)
            {
                if (!isRunning) break;
                var distance = LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, pokemon.Latitude, pokemon.Longitude);

                if (!_recentEncounters.Contains(pokemon.EncounterId) && (!_settings.UsePokemonToNotCatchList || !_settings.PokemonsNotToCatch.Contains(pokemon.PokemonId)))
                {
                    _recentEncounters.Add(pokemon.EncounterId);
                    await ProcessEncounter(new LocationData(pokemon.Latitude, pokemon.Longitude, _client.CurrentAltitude), pokemon.EncounterId, pokemon.SpawnPointId, EncounterSourceTypes.Wild);
                }    
           
                if (!Equals(pokemons.ElementAtOrDefault(pokemons.Count() - 1), pokemon))
                    // If pokemon is not last pokemon in list, create delay between catches, else keep moving.
                    await RandomDelay();
            }

            await ProcessPeriodicals();
            ////revive
            //if (_settings.UseRevives) await UseRevives();

            ////heal
            //if (_settings.UsePotions) await UsePotions();

            ////egg incubators
            //await UseIncubators(!_settings.UseEggIncubators);

            ////evolve
            //if (_settings.EvolvePokemon) await EvolvePokemon();

            ////power up
            //if (_settings.PowerUpPokemon) await PowerUpPokemon();

            ////trasnfer
            //if (_settings.TransferPokemon) await TransferPokemon();
        }

        private async Task ProcessFortList(List<FortData> pokeStopList, GetMapObjectsResponse mapObjects)
        {

            if (pokeStopList.Count == 0) return;
            var gymsList = pokeStopList.Where(x => x.Type == FortType.Gym).ToList();
            var stopList = pokeStopList.Where(x => x.Type != FortType.Gym).ToList();
            var unvisitedGymList = gymsList.Where(x => !gymTries.Contains(x.Id)).ToList();
            var pokestopCount = pokeStopList.Where(x => x.Type != FortType.Gym).Count();
            var gymCount = pokeStopList.Where(x => x.Type == FortType.Gym).Count();
            var visitedGymCount = gymsList.Where(x => gymTries.Contains(x.Id)).Count();
            var lureCount = stopList.Where(x => x.LureInfo != null).Count();


            Logger.Write($"Found {pokestopCount} {(pokestopCount == 1 ? "Pokestop" : "Pokestops")}{( CanVisitGyms && gymCount > 0 ? " | " + gymCount.ToString() + " " + (gymCount == 1 ? "Gym" : "Gyms") + " (" + visitedGymCount.ToString() + " Visited)" : string.Empty)}", LogLevel.Info);
            if (lureCount > 0) Logger.Write($"(INFO) Found {lureCount} with lure!", LogLevel.None, ConsoleColor.DarkMagenta);

            var priorityList = new List<FortData>();
            if (lureCount > 0)
            {
                var stopListWithLures = stopList.Where(x => x.LureInfo != null).ToList();
                if (stopListWithLures.Count > 0)
                {
            
                    //if we are prioritizing stops with lures
                    if (_settings.PrioritizeStopsWithLures)
                    {
                        int counter = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            for (int x = 0; x < stopListWithLures.Count; x++)
                            {
                                var lureStop = stopListWithLures[x];
                                stopList.Remove(lureStop);
                                priorityList.Insert(counter, lureStop);
                                counter++;
                            }
                        }
                    }
                }
            }

            //merge location lists
            var tempList = new List<FortData>(stopList);
            tempList.AddRange(unvisitedGymList);
            tempList = PokeRoadieNavigation.PathByNearestNeighbour(tempList.ToArray()).ToList();

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
                var location = new LocationData(_client.CurrentLatitude, _client.CurrentLongitude, _client.CurrentAltitude);
                if (!RaiseSyncEvent(OnVisitForts, location, finalList))
                    OnVisitForts(location, finalList);
            }

       
            while (finalList.Any())
            {
                if (!isRunning) break;
                if (_settings.DestinationsEnabled && _settings.DestinationEndDate.HasValue && DateTime.Now > _settings.DestinationEndDate.Value)
                {
                    break;
                }

                await WriteStats();
                await Export();

                var pokeStop = finalList[0];
                finalList.RemoveAt(0);
                if (pokeStop.Type != FortType.Gym)
                {
                    await ProcessPokeStop(pokeStop, mapObjects);
                }
                else
                {
                    await ProcessGym(pokeStop, mapObjects);
                }
                //if (pokestopCount == 0 && gymCount > 0)
                //    await RandomHelper.RandomDelay(1000, 2000);
                //else
                    //await RandomHelper.RandomDelay(50, 200);
            }

        }

        private async Task ProcessGym(FortData pokeStop, GetMapObjectsResponse mapObjects)
        {
            if (!gymTries.Contains(pokeStop.Id))
            {

                if (CanCatch)
                    await ProcessNearby(mapObjects);

                var distance = LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, pokeStop.Latitude, pokeStop.Longitude);
                var fortInfo = await _client.Fort.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                if (fortInfo != null)
                {

                    //raise event
                    if (OnTravelingToGym != null)
                    {
                        var location = new LocationData(_client.CurrentLatitude, _client.CurrentLongitude, _client.CurrentAltitude);
                        if (!RaiseSyncEvent(OnTravelingToGym, location, fortInfo))
                            OnTravelingToGym(location, fortInfo);
                    }

                    var name = $"{fortInfo.Name}{(pokeStop.LureInfo == null ? "" : " WITH LURE")} in {distance:0.##} m distance";
                    Logger.Write(name, LogLevel.Pokestop);
                    await _navigation.HumanLikeWalking(new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude), _settings.MinSpeed, GetShortTask());
    
                    var fortDetails = await _client.Fort.GetGymDetails(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                    if (fortDetails.Result == GetGymDetailsResponse.Types.Result.Success)
                    {
                        var fortString = $"{ fortDetails.Name} | { fortDetails.GymState.FortData.OwnedByTeam } | { pokeStop.GymPoints} | { fortDetails.GymState.Memberships.Count}";
                        if (fortDetails.Result == GetGymDetailsResponse.Types.Result.Success)
                        {
                            var location = new LocationData(fortInfo.Latitude, fortInfo.Longitude, _client.CurrentAltitude);
                            fortDetails.Save(fortInfo, Path.Combine(gymDir, fortInfo.FortId + ".xml"), _client.CurrentAltitude);

                            //raise event
                            if (OnVisitGym != null)
                            {
                                if (!RaiseSyncEvent(OnVisitGym, location, fortInfo, fortDetails))
                                    OnVisitGym(location, fortInfo, fortDetails);
                            }

                            if (_stats.Currentlevel > 4)
                            {

                                //set team color
                                if (_playerProfile.PlayerData.Team == TeamColor.Neutral && _settings.TeamColor != TeamColor.Neutral)
                                {
                                    var teamResponse = await _inventory.SetPlayerTeam(_settings.TeamColor);
                                    if (teamResponse.Status == SetPlayerTeamResponse.Types.Status.Success)
                                    {
                                        //set cached memory object, so it does not try again
                                        _playerProfile.PlayerData.Team = _settings.TeamColor;

                                        //re-pull player information
                                        //_playerProfile = await _client.Player.GetPlayer();

                                        var color = ConsoleColor.Blue;
                                        switch (_settings.TeamColor)
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
                                        Logger.Write($"(TEAM) Joined the {_settings.TeamColor} Team!", LogLevel.None, color);
                                    }
                                    else if (teamResponse.Status == SetPlayerTeamResponse.Types.Status.Failure)
                                    {
                                        Logger.Write($"The team color selection failed - Player:{teamResponse.PlayerData} - Setting:{_settings.TeamColor}", LogLevel.Error);
                                    }
                                    else if (teamResponse.Status == SetPlayerTeamResponse.Types.Status.TeamAlreadySet)
                                    {
                                        Logger.Write($"The team was already set! - Player:{teamResponse.PlayerData} - Setting:{_settings.TeamColor}", LogLevel.Error);
                                    }
                                }

                                fortString = $"{ fortDetails.Name} | { fortDetails.GymState.FortData.OwnedByTeam } | { pokeStop.GymPoints} | { fortDetails.GymState.Memberships.Count}";
                                if (fortDetails.GymState.FortData.OwnedByTeam == _playerProfile.PlayerData.Team)
                                {

                                    await PokeRoadieInventory.GetCachedInventory(_client);
                                    var pokemonList = await _inventory.GetHighestsVNotDeployed(1);
                                    var pokemon = pokemonList.FirstOrDefault();
                                    if (pokemon != null)
                                    {
                                        var response = await _client.Fort.FortDeployPokemon(fortInfo.FortId, pokemon.Id);
                                        if (response.Result == FortDeployPokemonResponse.Types.Result.Success)
                                        {
                                            PokeRoadieInventory.IsDirty = true;
                                            Logger.Write($"(GYM) Deployed {pokemon.GetMinStats()} to {fortDetails.Name}", LogLevel.None, ConsoleColor.Green);

                                            //raise event
                                            if (OnDeployToGym != null)
                                            {
                                                if (!RaiseSyncEvent(OnDeployToGym, location, fortDetails, pokemon))
                                                    OnDeployToGym(location, fortDetails, pokemon);
                                            }
                                        }
                                        //else if (response.Result == FortDeployPokemonResponse.Types.Result.ErrorPokemonNotFullHp)
                                        //{
                                        //    var figureThisShitOut = pokemon;
                                        //}
                                        //else
                                        //{
                                        //    Logger.Write($"(GYM) Deployment Failed at {fortString} - {response.Result}", LogLevel.None, ConsoleColor.Green);
                                        //}
                                    }
                                }
                                else
                                {
                                    Logger.Write($"(GYM) Wasted walk on {fortString}", LogLevel.None, ConsoleColor.Cyan);
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
                    //    attempts++;
                    //    Logger.Write($"(GYM) Moving closer to {fortInfo.Name} - {fortDetails.Result}", LogLevel.None, ConsoleColor.Cyan);
                    //    var ToStart = await _navigation.HumanLikeWalkingGetCloser(
                    //        new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude, _client.CurrentAltitude),
                    //        _settings.FlyingEnabled ? _settings.FlyingSpeed : _settings.MinSpeed, GetShortWalkingTask(), 0.20);

                    //}
                    else
                    {
                        Logger.Write($"(GYM) Ignoring {fortInfo.Name} - {fortDetails.Result}", LogLevel.None, ConsoleColor.Cyan);
                    }
                }
                gymTries.Add(pokeStop.Id);
            }
           
        }

        private async Task ProcessPokeStop(FortData pokeStop, GetMapObjectsResponse mapObjects)
        {

            if (CanCatch)
                await ProcessNearby(mapObjects);

            var distance = LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, pokeStop.Latitude, pokeStop.Longitude);
            
            //get fort info
            var fortInfo = await _client.Fort.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
            fortInfo.Save(Path.Combine(pokestopsDir, pokeStop.Id + ".xml"), _client.CurrentAltitude);

            //raise event
            if (OnTravelingToPokestop != null)
            {
                var location = new LocationData(_client.CurrentLatitude, _client.CurrentLongitude, _client.CurrentAltitude);
                if (!RaiseSyncEvent(OnTravelingToPokestop, location, fortInfo))
                    OnTravelingToPokestop(location, fortInfo);
            }

            Logger.Write($"{fortInfo.Name}{(pokeStop.LureInfo == null ? "" : " WITH LURE")} in {distance:0.##} m distance", LogLevel.Pokestop);
            await _navigation.HumanLikeWalking(new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude),  distance > _settings.MaxDistance / 2 ? _settings.LongDistanceSpeed : _settings.MinSpeed,  distance > _settings.MaxDistance / 2 ? GetLongTask() : GetShortTask(), distance > _settings.MaxDistance / 2 ? false : true);

            if (CanCatch)
                await ProcessNearby(mapObjects);

            if (CanVisit)
            {
                if (pokeStop.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime())
                {
                    //search fort
                    var fortSearch = await _client.Fort.SearchFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);

                    //raise event
                    if (OnVisitPokestop != null)
                    {
                        var location = new LocationData(_client.CurrentLatitude, _client.CurrentLongitude, _client.CurrentAltitude);
                        if (!RaiseSyncEvent(OnVisitPokestop, location, fortInfo, fortSearch))
                            OnVisitPokestop(location, fortInfo, fortSearch);
                    }

                    if (fortSearch.ExperienceAwarded > 0)
                    {
                        _stats.AddExperience(fortSearch.ExperienceAwarded);
                        _stats.UpdateConsoleTitle(_client, _inventory);
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

                        _settings.Session.VisitCount++;

                        if (!softBan) Logger.Write($"XP: {fortSearch.ExperienceAwarded}, Gems: {fortSearch.GemsAwarded}, Eggs: {EggReward}, Items: {StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch.ItemsAwarded)}", LogLevel.Pokestop);
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
            if (CanCatch && pokeStop.LureInfo != null && (lastEnconterId == 0 || lastEnconterId != pokeStop.LureInfo.EncounterId))
            {
                if (!_recentEncounters.Contains(pokeStop.LureInfo.EncounterId) && (!_settings.UsePokemonToNotCatchList || !_settings.PokemonsNotToCatch.Contains(pokeStop.LureInfo.ActivePokemonId)))
                {
                    _recentEncounters.Add(pokeStop.LureInfo.EncounterId);
                    await ProcessLureEncounter(new LocationData(pokeStop.Latitude, pokeStop.Longitude, _client.CurrentAltitude), pokeStop);
                }
            }

            if (CanCatch && _settings.LoiteringActive && pokeStop.LureInfo != null && pokeStop.LureInfo.LureExpiresTimestampMs < DateTime.UtcNow.ToUnixTime())
            {

                Logger.Write($"Loitering: {fortInfo.Name} has a lure we can milk!", LogLevel.Info);                  
                while (_settings.LoiteringActive && pokeStop.LureInfo != null && pokeStop.LureInfo.LureExpiresTimestampMs < DateTime.UtcNow.ToUnixTime())
                {

                    if (_settings.ShowDebugMessages)
                    {
                        var ts = new TimeSpan(DateTime.UtcNow.ToUnixTime() - pokeStop.LureInfo.LureExpiresTimestampMs);
                        Logger.Write($"Lure Info - Now:{DateTime.UtcNow.ToUnixTime()} | Lure Timestamp: {pokeStop.LureInfo.LureExpiresTimestampMs} | Expiration: {ts}");
                    }

                    if (CanCatch)
                        await ProcessNearby(mapObjects);

                    if (lastEnconterId == 0 || lastEnconterId != pokeStop.LureInfo.EncounterId)
                    {
                        if (!_recentEncounters.Contains(pokeStop.LureInfo.EncounterId) && (!_settings.UsePokemonToNotCatchList || !_settings.PokemonsNotToCatch.Contains(pokeStop.LureInfo.ActivePokemonId)))
                        {
                            _recentEncounters.Add(pokeStop.LureInfo.EncounterId);
                            await ProcessLureEncounter(new LocationData(pokeStop.Latitude, pokeStop.Longitude, _client.CurrentAltitude), pokeStop);
                        }              
                    }
                    if (CanVisit)
                    {
                        var fortSearch2 = await _client.Fort.SearchFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                        if (fortSearch2.ExperienceAwarded > 0)
                        {
                            _stats.AddExperience(fortSearch2.ExperienceAwarded);
                            _stats.UpdateConsoleTitle(_client, _inventory);
                            string EggReward = fortSearch2.PokemonDataEgg != null ? "1" : "0";
                            Logger.Write($"XP: {fortSearch2.ExperienceAwarded}, Gems: {fortSearch2.GemsAwarded}, Eggs: {EggReward}, Items: {StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch2.ItemsAwarded)}", LogLevel.Pokestop);
                            recycleCounter++;
                        }

                    }
                    await RandomHelper.RandomDelay(15000, 45000);
                    await ProcessPeriodicals();

                    var mapObjectsTuple = await GetMapObjects();
                    mapObjects = mapObjectsTuple;
                    pokeStop = mapObjects.MapCells.SelectMany(i => i.Forts).Where(x => x.Id == pokeStop.Id).FirstOrDefault();
                    if (pokeStop.LureInfo == null || pokeStop.LureInfo.LureExpiresTimestampMs < 1)
                        break;
                    else
                        Logger.Write($"Loitering: {fortInfo.Name} still has a lure, chillin out!", LogLevel.Info);

                }
            }

            await ProcessPeriodicals();

        }

        private async Task ProcessEncounter(LocationData location, ulong encounterId, string spawnPointId, EncounterSourceTypes source)
        {

            var encounter = await _client.Encounter.EncounterPokemon(encounterId, spawnPointId);
            var probability = encounter?.CaptureProbability?.CaptureProbability_?.First();

            if (encounter.Status == EncounterResponse.Types.Status.EncounterSuccess)
            {
                
                await ProcessCatch(new EncounterData(location, encounterId, encounter?.WildPokemon?.PokemonData, probability, spawnPointId, source));
            }
            else if (encounter.Status == EncounterResponse.Types.Status.PokemonInventoryFull)
            {

                if (_settings.TransferPokemon && _settings.TransferTrimFatCount > 0)
                {
                    Logger.Write($"Pokemon inventory full, trimming the fat...", LogLevel.Info);
                    var query = (await _inventory.GetPokemons()).Where(x => string.IsNullOrWhiteSpace(x.DeployedFortId) && x.Favorite == 0 && !_settings.PokemonsNotToTransfer.Contains(x.PokemonId));

                    //ordering
                    Func<PokemonData, double> orderBy = null;
                    switch (_settings.TransferPriorityType)
                    {
                        case PriorityTypes.CP:
                            orderBy = new Func<PokemonData, double>(x => x.Cp);
                            break;
                        case PriorityTypes.IV:
                            orderBy = new Func<PokemonData, double>(x => x.GetPerfection());
                            break;
                        case PriorityTypes.V:
                            orderBy = new Func<PokemonData, double>(x => x.CalculatePokemonValue());
                            break;
                        default:
                            break;
                    }

                    Func<PokemonData, double> thenBy = null;
                    switch (_settings.TransferPriorityType2)
                    {
                        case PriorityTypes.CP:
                            thenBy = new Func<PokemonData, double>(x => x.Cp);
                            break;
                        case PriorityTypes.IV:
                            thenBy = new Func<PokemonData, double>(x => x.GetPerfection());
                            break;
                        case PriorityTypes.V:
                            thenBy = new Func<PokemonData, double>(x => x.CalculatePokemonValue());
                            break;
                        default:
                            break;
                    }

                    query = orderBy == null ? query : thenBy == null ? query.OrderBy(orderBy) : query.OrderBy(orderBy).ThenBy(thenBy);

                    await TransferPokemon(query.Take(_settings.TransferTrimFatCount).ToList());
                    
                    //try again after trimming the fat
                    var encounter2 = await _client.Encounter.EncounterPokemon(encounterId, spawnPointId);
                    if (encounter2.Status == EncounterResponse.Types.Status.EncounterSuccess)
                        await ProcessCatch(new EncounterData(location, encounterId, encounter2?.WildPokemon?.PokemonData, probability, spawnPointId, source));
                }
                else
                {
                    Logger.Write($"Pokemon inventory full. You should consider turning on TransferPokemon, and set a value for TransferTrimFatCount. This will prevent the inventory from filling up.", LogLevel.Warning);
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
            else Logger.Write($"Encounter problem: {encounter.Status}", LogLevel.Warning);

        }

        private async Task ProcessIncenseEncounter(LocationData location, ulong encounterId, string spawnPointId)
        {

            var encounter = await _client.Encounter.EncounterIncensePokemon(encounterId, spawnPointId);
            var probability = encounter?.CaptureProbability?.CaptureProbability_?.First();

            if (encounter.Result == IncenseEncounterResponse.Types.Result.IncenseEncounterSuccess)
            {
                await ProcessCatch(new EncounterData(location, encounterId, encounter?.PokemonData, probability, spawnPointId, EncounterSourceTypes.Incense));
            }

            else if (encounter.Result == IncenseEncounterResponse.Types.Result.PokemonInventoryFull)
            {

                if (_settings.TransferPokemon && _settings.TransferTrimFatCount > 0)
                {
                    //trim the fat
                    await TransferTrimTheFat();
                    //try again after trimming the fat
                    var encounter2 = await _client.Encounter.EncounterIncensePokemon(encounterId, spawnPointId);
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

            var encounter = await _client.Encounter.EncounterLurePokemon(fortData.LureInfo.EncounterId, fortData.Id);
            var probability = encounter?.CaptureProbability?.CaptureProbability_?.First();

            if (encounter.Result == DiskEncounterResponse.Types.Result.Success)
            {
                await ProcessCatch(new EncounterData(location, fortData.LureInfo.EncounterId, encounter?.PokemonData, probability, fortData.Id, EncounterSourceTypes.Lure));
            }
             
            else if (encounter.Result == DiskEncounterResponse.Types.Result.PokemonInventoryFull)
            {

                if (_settings.TransferPokemon && _settings.TransferTrimFatCount > 0)
                {
                    //trim the fat
                    await TransferTrimTheFat();

                    //try again after trimming the fat
                    var encounter2 = await _client.Encounter.EncounterLurePokemon(fortData.LureInfo.EncounterId, fortData.Id);
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
            _inventory.Save(encounter.PokemonData, encounter.Location.GetGeo(), _playerProfile.PlayerData.Username, _stats.Currentlevel, _playerProfile.PlayerData.Team.ToString().Substring(0, 1).ToUpper(), encounter.EncounterId, encounter.Source, Path.Combine(encountersDir, encounter.EncounterId + ".xml"));
            
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
                if (!isRunning) break;
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
                    if (encounter.EncounterId != lastMissedEncounterId) Logger.Write($"No Pokeballs :( - We missed {encounter.PokemonData.GetMinStats()}", LogLevel.Warning);
                    else Logger.Write($"It is that same {encounter.PokemonData}.", LogLevel.Info);
                    lastMissedEncounterId = encounter.EncounterId;

                    if (_settings.PokeballRefillDelayMinutes > 0)
                    {
                        noWorkTimer = DateTime.Now.AddMinutes(_settings.PokeballRefillDelayMinutes);
                        Logger.Write($"We are going to hold off catching for {_settings.PokeballRefillDelayMinutes} minutes, so we can refill on some pokeballs.", LogLevel.Warning);
                    }
                    return;
                }

                var bestBerry = await GetBestBerry(encounter.PokemonData, encounter.Probability);
                //only use berries when they are fleeing
                if (fleeCounter == 0)
                {
                    var inventoryBerries = await _inventory.GetItems();
                    var berries = inventoryBerries.Where(p => p.ItemId == bestBerry).FirstOrDefault();
                    if (bestBerry != ItemId.ItemUnknown && encounter.Probability.HasValue && encounter.Probability.Value < 0.35)
                    {
                        await _client.Encounter.UseCaptureItem(encounter.EncounterId, bestBerry, encounter.SpawnPointId);
                        berries.Count--;
                        Logger.Write($"{bestBerry} used, remaining: {berries.Count}", LogLevel.Berry);
                        await RandomDelay();
                    }
                }

                //log throw attempt
                Logger.Write($"(THROW) {throwData.HitText} {throwData.BallName} ball {throwData.SpinText} toss...", LogLevel.None, ConsoleColor.Yellow);

                caughtPokemonResponse = await _client.Encounter.CatchPokemon(encounter.EncounterId, encounter.SpawnPointId, throwData.ItemId, throwData.NormalizedRecticleSize,throwData.SpinModifier);
                
                if (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
                {
                    PokeRoadieInventory.IsDirty = true;
                    if (encounter.Source == EncounterSourceTypes.Lure) lastEnconterId = encounter.EncounterId;
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
                        _stats.AddExperience(xp);
                    _stats.IncreasePokemons();
                    var profile = await _client.Player.GetPlayer();
                    _stats.GetStardust(profile.PlayerData.Currencies.ToArray()[1].Amount);

                    //raise event
                    if (OnCatch != null)
                    {
                        if (!RaiseSyncEvent(OnCatch, encounter, caughtPokemonResponse))
                            OnCatch(encounter, caughtPokemonResponse);
                    }
                    _settings.Session.CatchCount++;
   
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

                    string receivedXP = catchStatus == "CatchSuccess"
                        ? $"and received XP {caughtPokemonResponse.CaptureAward.Xp.Sum()}"
                        : $"";

                    Logger.Write($"({encounter.Source} {catchStatus.Replace("Catch","")}) | {encounter.PokemonData.GetMinStats()} | Chance: {(encounter.Probability.HasValue ? ((float)((int)(encounter.Probability * 100)) / 100).ToString() : "Unknown")} | with a {throwData.BallName}Ball {receivedXP}", LogLevel.None, ConsoleColor.Yellow);
                    
                    //humanize pokedex add
                    if (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
                    {
                        if (caughtPokemonResponse.CaptureAward.Xp.Sum() > 499)
                        {
                            Logger.Write($"First time catching a {encounter.PokemonData.PokemonId}, waiting to add it to the pokedex...", LogLevel.Info);
                            await RandomDelay(_settings.PokedexEntryMinDelay, _settings.PokedexEntryMaxDelay);
                        }
                        else
                        {
                            await RandomDelay(_settings.CatchMinDelay, _settings.CatchMaxDelay);
                        }
                    }
                }

                
                if (caughtPokemonResponse.Status != CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
                {
                    attemptCounter++;
                    await RandomDelay(_settings.CatchMinDelay, _settings.CatchMaxDelay);
                }
                

            }
            while (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchMissed || caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchEscape && attemptCounter < 10);
        }

        #endregion
        #region " New Destination Methods - not yet used "

        private async Task NextDestination()
        {
            //get current destination
            var currentDestination = _settings.Destinations[_settings.DestinationIndex];
            //get new destination index
            var newIndex = _settings.DestinationIndex + 1 >= _settings.Destinations.Count ? 0 : _settings.DestinationIndex + 1;
            //get coords
            var destination = _settings.Destinations[newIndex];

            //set new index and default location
            _settings.DestinationIndex = newIndex;

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
            if (_settings.DestinationsEnabled)
            {
                if (_settings.DestinationEndDate.HasValue)
                {
                    if (DateTime.Now > _settings.DestinationEndDate.Value)
                    {

                        if (_settings.Destinations != null && _settings.Destinations.Count > 1)
                        {
                            await NextDestination();
                        }
                        else
                        {
                            _settings.DestinationEndDate = DateTime.Now.AddMinutes(_settings.MinutesPerDestination);
                        }
                    }
                }
                else
                {
                    _settings.DestinationEndDate = DateTime.Now.AddMinutes(_settings.MinutesPerDestination);
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
            _settings.WaypointLatitude = geo.Latitude;
            _settings.WaypointLongitude = geo.Longitude;
            _settings.WaypointAltitude = geo.Altitude;
            _settings.Save();

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
            var distanceFromStart = LocationUtils.CalculateDistanceInMeters(
            _client.CurrentLatitude, _client.CurrentLongitude, _settings.WaypointLatitude, _settings.WaypointLongitude);

            // Edge case for when the client somehow ends up outside the defined radius
            if (_settings.MaxDistance != 0 && distanceFromStart > _settings.MaxDistance)
            {
                //return back the the waypoint
                Logger.Write($"Returning to the start.", LogLevel.Navigation);
                await GotoCurrentWaypoint();


                //if (_settings.DestinationsEnabled)
                //{
                //    //return back the the waypoint
                //    Logger.Write($"Returning to the start.", LogLevel.Navigation);
                //    await GotoCurrentWaypoint();
                //}
                //else
                //{
                //    if (travelHistory.Count > 4)
                //    {
                //        Logger.Write($"Returning to the start.", LogLevel.Navigation);
                //        var geo = travelHistory[0];
                //        travelHistory.Clear();
                //        SetWaypoint(geo);
                //        await GotoCurrentWaypoint();
                //    }
                //    else
                //    {
                //        var pokeStopList = await _inventory.GetPokestops(false);
                //        if (pokeStopList != null && pokeStopList.Count() > 5)
                //        {
                //            Logger.Write($"Set current location as new waypoint {pokeStopList.Count()}", LogLevel.Navigation);
                //            SetWaypoint(GetCurrentGeo());
                //        }
                //    }
                //}
                //Logger.Write($"Reached the edge of the waypoint", LogLevel.Navigation);

                ////set current point as new waypoint
                //Logger.Write($"Set the current location as the new waypoint", LogLevel.Navigation);

            }
        }

        private GeoCoordinate GetWaypointGeo()
        {
            return new GeoCoordinate(_settings.WaypointLatitude, _settings.WaypointLongitude, _settings.WaypointAltitude);
        }

        private GeoCoordinate GetCurrentGeo()
        {
            return new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude, _client.CurrentAltitude);
        }

        private LocationData GetCurrentLocation()
        {
            return new LocationData(_client.CurrentLatitude, _client.CurrentLongitude, _client.CurrentAltitude);
        }

        private async Task Travel(GeoCoordinate source, GeoCoordinate destination, string name = "")
        {
            //get distance
            var distance = LocationUtils.CalculateDistanceInMeters(source, destination);
            if (distance > 0)
            {
                //write travel plan


                //go to location
                var response = await _navigation.HumanLikeWalking(destination,  distance > _settings.MaxDistance / 2 ? _settings.LongDistanceSpeed : _settings.MinSpeed,  distance > _settings.MaxDistance / 2 ? GetLongTask() : GetShortTask(), distance > _settings.MaxDistance / 2 ? false : true);


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

            var items = await _inventory.GetItems();
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
            var balance = _settings.PokeBallBalancing;

            var items = await _inventory.GetItems();
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
            if (ultraBalls != null && (pokemonCp >= 1000 || (iV >= _settings.KeepAboveIV && proba < 0.40)))
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
            if (greatBalls != null && (pokemonCp >= 300 || (iV >= _settings.KeepAboveIV && proba < 0.50)))
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
            if (_settings.EnableHumanizedThrows)
            {
                var pokemonIv = pokemon.GetPerfection();
                var pokemonV = pokemon.CalculatePokemonValue();

                //_settings.MissThrowChance
                

                if ((_settings.ForceExcellentThrowOverCp > 0 && pokemon.Cp > _settings.ForceExcellentThrowOverCp) ||
                    (_settings.ForceExcellentThrowOverIV > 0 && pokemonIv > _settings.ForceExcellentThrowOverIV) ||
                    (_settings.ForceExcellentThrowOverV > 0 && pokemonV > _settings.ForceExcellentThrowOverV))
                {
                    throwData.NormalizedRecticleSize = Random.NextDouble() * (1.95 - 1.7) + 1.7;
                }
                else if ((_settings.ForceGreatThrowOverCp > 0 && pokemon.Cp >= _settings.ForceGreatThrowOverCp) ||
                         (_settings.ForceGreatThrowOverIV > 0 &&  pokemonIv >= _settings.ForceGreatThrowOverIV) ||
                         (_settings.ForceGreatThrowOverV > 0 && pokemonV >= _settings.ForceGreatThrowOverV))
                {
                    throwData.NormalizedRecticleSize = Random.NextDouble() * (1.95 - 1.3) + 1.3;
                    throwData.HitText = "Great";
                }
                else
                {
                    var regularThrow = 100 - (_settings.ExcellentThrowChance +
                                              _settings.GreatThrowChance +
                                              _settings.NiceThrowChance);
                    var rnd = Random.Next(1, 101);

                    if (rnd <= regularThrow)
                    {
                        throwData.NormalizedRecticleSize = Random.NextDouble() * (1 - 0.1) + 0.1;
                        throwData.HitText = "Ordinary";
                    }
                    else if (rnd <= regularThrow + _settings.NiceThrowChance)
                    {
                        throwData.NormalizedRecticleSize = Random.NextDouble() * (1.3 - 1) + 1;
                        throwData.HitText = "Nice";
                    }
                    else if (rnd <=
                             regularThrow + _settings.NiceThrowChance +
                             _settings.GreatThrowChance)
                    {
                        throwData.NormalizedRecticleSize = Random.NextDouble() * (1.7 - 1.3) + 1.3;
                        throwData.HitText = "Great";
                    }

                    if (Random.NextDouble() * 100 > _settings.CurveThrowChance)
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

            var items = await _inventory.GetItems();
            var berries = items.Where(i => (i.ItemId == ItemId.ItemRazzBerry
                                        || i.ItemId == ItemId.ItemBlukBerry
                                        || i.ItemId == ItemId.ItemNanabBerry
                                        || i.ItemId == ItemId.ItemWeparBerry
                                        || i.ItemId == ItemId.ItemPinapBerry) && i.Count > 0).GroupBy(i => (i.ItemId)).ToList();
            if (berries.Count == 0 || pokemonCp <= 350) return ItemId.ItemUnknown;

            var razzBerryCount = await _inventory.GetItemAmountByType(ItemId.ItemRazzBerry);
            var blukBerryCount = await _inventory.GetItemAmountByType(ItemId.ItemBlukBerry);
            var nanabBerryCount = await _inventory.GetItemAmountByType(ItemId.ItemNanabBerry);
            var weparBerryCount = await _inventory.GetItemAmountByType(ItemId.ItemWeparBerry);
            var pinapBerryCount = await _inventory.GetItemAmountByType(ItemId.ItemPinapBerry);

            if (pinapBerryCount > 0 && pokemonCp >= 2000)
                return ItemId.ItemPinapBerry;

            if (weparBerryCount > 0 && pokemonCp >= 1500)
                return ItemId.ItemWeparBerry;

            if (nanabBerryCount > 0 && (pokemonCp >= 1000 || (iV >= _settings.KeepAboveIV && proba < 0.40)))
                return ItemId.ItemNanabBerry;

            if (blukBerryCount > 0 && (pokemonCp >= 500 || (iV >= _settings.KeepAboveIV && proba < 0.50)))
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

            var items = await _inventory.GetItems();
            var berries = items.Where(i => (i.ItemId == ItemId.ItemRazzBerry
                                        || i.ItemId == ItemId.ItemBlukBerry
                                        || i.ItemId == ItemId.ItemNanabBerry
                                        || i.ItemId == ItemId.ItemWeparBerry
                                        || i.ItemId == ItemId.ItemPinapBerry) && i.Count > 0).GroupBy(i => (i.ItemId)).ToList();
            if (berries.Count == 0 || pokemonCp <= 350) return ItemId.ItemUnknown;

            var razzBerryCount = await _inventory.GetItemAmountByType(ItemId.ItemRazzBerry);
            var blukBerryCount = await _inventory.GetItemAmountByType(ItemId.ItemBlukBerry);
            var nanabBerryCount = await _inventory.GetItemAmountByType(ItemId.ItemNanabBerry);
            var weparBerryCount = await _inventory.GetItemAmountByType(ItemId.ItemWeparBerry);
            var pinapBerryCount = await _inventory.GetItemAmountByType(ItemId.ItemPinapBerry);

            if (pinapBerryCount > 0 && pokemonCp >= 2000)
                return ItemId.ItemPinapBerry;

            if (weparBerryCount > 0 && pokemonCp >= 1500)
                return ItemId.ItemWeparBerry;

            if (nanabBerryCount > 0 && (pokemonCp >= 1000 || (iV >= _settings.KeepAboveIV && proba < 0.40)))
                return ItemId.ItemNanabBerry;

            if (blukBerryCount > 0 && (pokemonCp >= 500 || (iV >= _settings.KeepAboveIV && proba < 0.50)))
                return ItemId.ItemBlukBerry;

            if (razzBerryCount > 0 && pokemonCp >= 150)
                return ItemId.ItemRazzBerry;

            return ItemId.ItemUnknown;
            //return berries.OrderBy(g => g.Key).First().Key;
        }

        #endregion
        #region " Travel Task Methods "

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
            var mapObjects = await GetMapObjects();
            await ProcessNearby(mapObjects);
        }
        private async Task CatchNearbyStops()
        {
            var mapObjects = await GetMapObjects();
            await CatchNearbyStops(mapObjects, false);
        }
        private async Task GpxCatchNearbyStops()
        {
            var mapObjects = await GetMapObjects();
            await CatchNearbyStops(mapObjects, true);
        }

        private async Task GpxCatchNearbyPokemonsAndStops()
        {
            var mapObjects = await GetMapObjects();
            await ProcessNearby(mapObjects);
            await CatchNearbyStops(mapObjects, true);
        }
        private async Task CatchNearbyPokemonsAndStops(bool path)
        {
            var mapObjects = await GetMapObjects();
            await ProcessNearby(mapObjects);
            await CatchNearbyStops(mapObjects, path);
        }

        private async Task CatchNearbyStops(GetMapObjectsResponse mapObjects, bool path)
        {

            var totalActivecount = 0;
            var pokeStopList = GetPokestops(GetCurrentLocation(), path ? _settings.MaxDistanceForLongTravel : _settings.MaxDistance, mapObjects);
            var gymsList = pokeStopList.Where(x => x.Type == FortType.Gym).ToList();
            var stopList = pokeStopList.Where(x => x.Type != FortType.Gym).ToList();
            var unvisitedGymList = gymsList.Where(x => !gymTries.Contains(x.Id)).ToList();
            if (_settings.VisitGyms) totalActivecount += unvisitedGymList.Count;
            if (_settings.VisitPokestops) totalActivecount += stopList.Count;

            if (totalActivecount > 0)
            {
                if (inTravel) Logger.Write($"Slight course change...", LogLevel.Navigation);
                await ProcessFortList(pokeStopList, mapObjects);
                if (inTravel)
                {
                    var speedInMetersPerSecond = _settings.LongDistanceSpeed / 3.6;
                    var sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude);
                    var distanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, new GeoCoordinate(_settings.WaypointLatitude, _settings.WaypointLongitude));
                    var seconds = distanceToTarget / speedInMetersPerSecond;
                    Logger.Write($"Returning to long distance travel: {(_settings.DestinationsEnabled ? _settings.Destinations[_settings.DestinationIndex].Name + " " : String.Empty )}{distanceToTarget:0.##} meters. Will take {StringUtils.GetSecondsDisplay(seconds)} {StringUtils.GetTravelActionString(_settings.LongDistanceSpeed)} at {_settings.LongDistanceSpeed}kmh", LogLevel.Navigation);
                }
            }
        }

        #endregion
        #region " Evolve Methods "
     
        private async Task EvolvePokemon()
        {
            await PokeRoadieInventory.GetCachedInventory(_client);
            var pokemonToEvolve = await _inventory.GetPokemonToEvolve();
            if (pokemonToEvolve == null || !pokemonToEvolve.Any()) return;
            await EvolvePokemon(pokemonToEvolve.ToList());
        }

        private async Task EvolvePokemon(List<PokemonData> pokemonToEvolve)
        {
                Logger.Write($"Found {pokemonToEvolve.Count()} Pokemon for Evolve:", LogLevel.Info);
                if (_settings.UseLuckyEggs)
                    await UseLuckyEgg();

                foreach (var pokemon in pokemonToEvolve)
                {
                    if (!isRunning) break;
                    await EvolvePokemon(pokemon);
                }
            }

        private async Task EvolvePokemon(PokemonData pokemon)
        {
            var evolvePokemonOutProto = await _client.Inventory.EvolvePokemon((ulong)pokemon.Id);

            if (evolvePokemonOutProto.Result == EvolvePokemonResponse.Types.Result.Success)
            {
                PokeRoadieInventory.IsDirty = true;
                Logger.Write($"{pokemon.GetMinStats()} for {evolvePokemonOutProto.ExperienceAwarded} xp", LogLevel.Evolve);
                _stats.AddExperience(evolvePokemonOutProto.ExperienceAwarded);
                //raise event
                if (OnEvolve != null)
                {
                    if (!RaiseSyncEvent(OnEvolve, pokemon))
                        OnEvolve(pokemon);
                }

                //evolution specific delay
                await RandomDelay(_settings.EvolutionMinDelay, _settings.EvolutionMaxDelay);
            }
            else
            {
                Logger.Write($"(EVOLVE ERROR) {pokemon.GetMinStats()} - {evolvePokemonOutProto.Result}", LogLevel.None, ConsoleColor.Red);
                await RandomDelay();
            }
        }

        #endregion
        #region " Transfer Methods "

        private async Task TransferPokemon()
        {
            await PokeRoadieInventory.GetCachedInventory(_client);
            var pokemons = await _inventory.GetPokemonToTransfer();
            if (pokemons == null || !pokemons.Any()) return;
            await TransferPokemon(pokemons);

        }
        private async Task TransferPokemon(PokemonData pokemon)
        {
            var response = await _client.Inventory.TransferPokemon(pokemon.Id);
            if (response.Result == ReleasePokemonResponse.Types.Result.Success)
            {
                PokeRoadieInventory.IsDirty = true;
                var myPokemonSettings = await _inventory.GetPokemonSettings();
                var pokemonSettings = myPokemonSettings.ToList();
                var myPokemonFamilies = await _inventory.GetPokemonFamilies();
                var pokemonFamilies = myPokemonFamilies.ToArray();
                var settings = pokemonSettings.Single(x => x.PokemonId == pokemon.PokemonId);
                var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);
                var FamilyCandies = $"{familyCandy.Candy_ + 1}";

                _stats.IncreasePokemonsTransfered();
                _stats.UpdateConsoleTitle(_client, _inventory);

                PokemonData bestPokemonOfType = null;
                switch (_settings.TransferPriorityType)
                {
                    case PriorityTypes.CP:
                        bestPokemonOfType = await _inventory.GetHighestPokemonOfTypeByCP(pokemon);
                        break;
                    case PriorityTypes.IV:
                        bestPokemonOfType = await _inventory.GetHighestPokemonOfTypeByIV(pokemon);
                        break;
                    default:
                        bestPokemonOfType = await _inventory.GetHighestPokemonOfTypeByV(pokemon);
                        break;
                }

                string bestPokemonInfo = "NONE";
                if (bestPokemonOfType != null)
                    bestPokemonInfo = bestPokemonOfType.GetMinStats();
                Logger.Write($"{(pokemon.GetMinStats().ToString()).PadRight(33) + " Best: " + (bestPokemonInfo.ToString()).PadRight(33) + " Candy: " + FamilyCandies}", LogLevel.Transfer);

                //raise event
                if (OnTransfer != null)
                {
                    if (!RaiseSyncEvent(OnTransfer, pokemon))
                        OnTransfer(pokemon);
                }
                //transfer specific delay
                await RandomDelay(_settings.TransferMinDelay, _settings.TransferMaxDelay);
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
            if (!_settings.TransferPokemon || _settings.TransferTrimFatCount == 0)
            {
                await PokeRoadieInventory.GetCachedInventory(_client);
                Logger.Write($"Pokemon inventory full, trimming the fat by {_settings.TransferTrimFatCount}:", LogLevel.Info);
                var query = (await _inventory.GetPokemons()).Where(x => string.IsNullOrWhiteSpace(x.DeployedFortId) && x.Favorite == 0 && !_settings.PokemonsNotToTransfer.Contains(x.PokemonId));

                //ordering
                Func<PokemonData, double> orderBy = null;
                switch (_settings.TransferPriorityType)
                {
                    case PriorityTypes.CP:
                        orderBy = new Func<PokemonData, double>(x => x.Cp);
                        break;
                    case PriorityTypes.IV:
                        orderBy = new Func<PokemonData, double>(x => x.GetPerfection());
                        break;
                    case PriorityTypes.V:
                        orderBy = new Func<PokemonData, double>(x => x.CalculatePokemonValue());
                        break;
                    default:
                        break;
                }

                Func<PokemonData, double> thenBy = null;
                switch (_settings.TransferPriorityType2)
                {
                    case PriorityTypes.CP:
                        thenBy = new Func<PokemonData, double>(x => x.Cp);
                        break;
                    case PriorityTypes.IV:
                        thenBy = new Func<PokemonData, double>(x => x.GetPerfection());
                        break;
                    case PriorityTypes.V:
                        thenBy = new Func<PokemonData, double>(x => x.CalculatePokemonValue());
                        break;
                    default:
                        break;
                }

                query = orderBy == null ? query : thenBy == null ? query.OrderBy(orderBy) : query.OrderBy(orderBy).ThenBy(thenBy);

                await TransferPokemon(query.Take(_settings.TransferTrimFatCount).ToList());

            }
        }

        #endregion
        #region " Power Up Methods "

        public async Task PowerUpPokemon()
        {
            if (!_settings.PowerUpPokemon) return;
            await PokeRoadieInventory.GetCachedInventory(_client);
            if (await _inventory.GetStarDust() <= _settings.MinStarDustForPowerUps) return;
            var pokemons = await _inventory.GetPokemonToPowerUp();
            if (pokemons == null || pokemons.Count == 0) return;
            await PowerUpPokemon(pokemons);
        }

        public async Task PowerUpPokemon(List<PokemonData> pokemons)
        {
         
            var myPokemonSettings = await _inventory.GetPokemonSettings();
            var pokemonSettings = myPokemonSettings.ToList();

            var myPokemonFamilies = await _inventory.GetPokemonFamilies();
            var pokemonFamilies = myPokemonFamilies.ToArray();

            var upgradedNumber = 0;
            var finalList = new List<PokemonData>();


            //fixed by woshikie! Thanks!
            foreach (var pokemon in pokemons)
            {
                //if (pokemon.GetMaxCP() == pokemon.Cp) continue;

                var settings = pokemonSettings.Single(x => x.PokemonId == pokemon.PokemonId);
                var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);

                if (familyCandy.Candy_ < (pokemon.GetLevel() / 10)) continue;
                if (_settings.MinCandyForPowerUps != 0 && familyCandy.Candy_ < _settings.MinCandyForPowerUps)
                {
                    continue;
                }

                if (pokemon.GetLevel() - _stats.Currentlevel >= 2) continue;
                finalList.Add(pokemon);
            }

            //foreach (var pokemon in pokemons)
            //{
            //    if (pokemon.GetMaxCP() == pokemon.Cp) continue;

            //    var settings = pokemonSettings.Single(x => x.PokemonId == pokemon.PokemonId);
            //    var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);

            //    if (familyCandy.Candy_ <= 0) continue;
            //    if (_settings.MinCandyForPowerUps != 0 && familyCandy.Candy_ < _settings.MinCandyForPowerUps)
            //    {
            //        continue;
            //    }
            //    finalList.Add(pokemon);
            //}

            if (finalList.Count == 0) return;

            Logger.Write($"Found {finalList.Count()} pokemon to power up:", LogLevel.Info);

            foreach (var pokemon in finalList)
            {
                var upgradeResult = await _client.Inventory.UpgradePokemon(pokemon.Id);
                if (upgradeResult.Result == UpgradePokemonResponse.Types.Result.Success)
                {
                    PokeRoadieInventory.IsDirty = true;
                    Logger.Write($"(POWER) Pokemon was powered up! {upgradeResult.UpgradedPokemon.GetMinStats()}", LogLevel.None, ConsoleColor.White);
                    upgradedNumber++;
                    //raise event
                    if (OnPowerUp != null)
                    {
                        if (!RaiseSyncEvent(OnPowerUp, pokemon))
                            OnPowerUp(pokemon);
                    }

                    //power up specific delay
                    await RandomDelay(_settings.PowerUpMinDelay, _settings.PowerUpMaxDelay);

                }
                else
                {
                    Logger.Write($"(POWER ERROR) Unable to powerup {pokemon.GetMinStats()}! Not enough Candies/Stardust or Max Level reached, we should not be hitting this code - {upgradeResult.Result.ToString()}", LogLevel.None, ConsoleColor.Red);
                    await RandomDelay();
                }
                //fixed by woshikie! Thanks!
                if (_settings.MaxPowerUpsPerRound > 0 && upgradedNumber >= _settings.MaxPowerUpsPerRound)
                    break;
            }
        }

        #endregion
        #region " Favorite Methods "

        public async Task FavoritePokemon()
        {
            if (!_settings.FavoritePokemon) return;
            await PokeRoadieInventory.GetCachedInventory(_client);
            var pokemons = await _inventory.GetPokemonToFavorite();
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
                var response = await _client.Inventory.SetFavoritePokemon(pokemon.Id, true);
                if (response.Result == SetFavoritePokemonResponse.Types.Result.Success)
                {
                    PokeRoadieInventory.IsDirty = true;
                    Logger.Write($"(FAVORITE) {pokemon.GetMinStats()}", LogLevel.None, ConsoleColor.White);

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
            await PokeRoadieInventory.GetCachedInventory(_client);
            var pokemons = await _inventory.GetPokemonToHeal();
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
                    var response = await _client.Inventory.UseItemPotion(potion, pokemon.Id);
                    if (response.Result == UseItemPotionResponse.Types.Result.Success)
                    {
                        PokeRoadieInventory.IsDirty = true;
                        Logger.Write($"Healed {pokemon.GetMinStats()} with {potion} - {response.Stamina}/{pokemon.StaminaMax}", LogLevel.Pokemon);
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
                        Logger.Write($"Failed to heal {pokemon.GetMinStats()} with {potion} - {response.Result}", LogLevel.Error);
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
            //if (_settings.PickupDailyBonuses)
            //{
            //    if (_playerProfile.PlayerData.DailyBonus.NextCollectedTimestampMs < DateTime.UtcNow.ToUnixTime())
            //    {
            //        var response = await _inventory.CollectDailyBonus();
            //        if (response.Result == CollectDailyBonusResponse.Types.Result.Success)
            //        {
            //            Logger.Write($"(BONUS) Daily Bonus Collected!", LogLevel.None, ConsoleColor.Green);
            //        }
            //        else if (response.Result == CollectDailyBonusResponse.Types.Result.TooSoon)
            //        {
            //            Logger.Write($"Attempted to collect Daily Bonus too soon! Timestamp is {_playerProfile.PlayerData.DailyBonus.NextCollectedTimestampMs}", LogLevel.Error);
            //        }
            //        else if (response.Result == CollectDailyBonusResponse.Types.Result.Failure || response.Result == CollectDailyBonusResponse.Types.Result.Unset)
            //        {
            //            Logger.Write($"Failure to collect Daily Bonus! Timestamp is {_playerProfile.PlayerData.DailyBonus.NextCollectedTimestampMs}", LogLevel.Error);
            //        }
            //    }
            //}

            if (_settings.PickupDailyDefenderBonuses)
            { 
                var pokemonDefendingCount = (await _inventory.GetPokemons()).Where(x => !string.IsNullOrEmpty(x.DeployedFortId)).Count();
                if (pokemonDefendingCount == 0 || pokemonDefendingCount < _settings.MinGymsBeforeBonusPickup) return;

                if (_playerProfile.PlayerData.DailyBonus.NextDefenderBonusCollectTimestampMs < DateTime.UtcNow.ToUnixTime())
                {
                    var response = await _inventory.CollectDailyDefenderBonus();
                    if (response.Result == CollectDailyDefenderBonusResponse.Types.Result.Success)
                    {
                        //update cached date to prevent error
                        _playerProfile.PlayerData.DailyBonus.NextDefenderBonusCollectTimestampMs = DateTime.UtcNow.AddDays(1).ToUnixTime();

                        Logger.Write($"(BONUS) Daily Defender Bonus Collected!", LogLevel.None, ConsoleColor.Green);
                        if (response.CurrencyType.Count() > 0)
                        {
                            for (int i = 0;i< response.CurrencyType.Count();i++)
                            {
                                //add gained xp
                                if (response.CurrencyType[i] == "XP")
                                    _stats.AddExperience(response.CurrencyAwarded[i]);
                                Logger.Write($"{response.CurrencyAwarded[i]} {response.CurrencyType[i]}", LogLevel.None, ConsoleColor.Green);
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
            await PokeRoadieInventory.GetCachedInventory(_client);
            var items = await _inventory.GetItemsToRecycle(_settings);
            if (items != null && items.Any())
                Logger.Write($"Found {items.Count()} Recyclable {(items.Count() == 1 ? "Item" : "Items")}:", LogLevel.Info);

            foreach (var item in items)
            {
                if (!isRunning) break;
                var response = await _client.Inventory.RecycleItem(item.ItemId, item.Count);
                if (response.Result == RecycleInventoryItemResponse.Types.Result.Success)
                {
                    PokeRoadieInventory.IsDirty = true;
                    Logger.Write($"{(item.ItemId).ToString().Replace("Item", "")} x {item.Count}", LogLevel.Recycling);

                    _stats.AddItemsRemoved(item.Count);
                    _stats.UpdateConsoleTitle(_client, _inventory);

                    //raise event
                    if (OnRecycleItems != null)
                    {
                        if (!RaiseSyncEvent(OnRecycleItems, item.ItemId, response.NewCount))
                            OnRecycleItems(item.ItemId, response.NewCount);
                    }
                 }

                //recycle specific delay
                await RandomDelay(_settings.RecycleMinDelay, _settings.RecycleMaxDelay);
            }
            recycleCounter = 0;
        }

        public async Task UseLuckyEgg()
        {
            if (_settings.UseLuckyEggs && (!_nextLuckyEggTime.HasValue || _nextLuckyEggTime.Value < DateTime.Now))
            {
                var inventory = await _inventory.GetItems();
                var LuckyEgg = inventory.Where(p => p.ItemId == ItemId.ItemLuckyEgg).FirstOrDefault();
                if (LuckyEgg == null || LuckyEgg.Count <= 0) return;

                var response = await _client.Inventory.UseItemXpBoost();
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
            
            var playerStats = await _inventory.GetPlayerStats();
            if (playerStats == null)
                return;

            var rememberedIncubators = GetIncubators();
            var pokemons = (await _inventory.GetPokemons()).ToList();

            // Check if eggs in remembered incubator usages have since hatched
            // (instead of calling session.Client.Inventory.GetHatchedEgg(), which doesn't seem to work properly)
            foreach (var incubator in rememberedIncubators)
            {
                var hatched = pokemons.FirstOrDefault(x => !x.IsEgg && x.Id == incubator.PokemonId);
                if (hatched == null) continue;
                Logger.Write($"Hatched egg! {hatched.GetStats()}", LogLevel.Egg);

                //raise event
                if (OnEggHatched != null)
                {
                    if (!RaiseSyncEvent(OnEggHatched, incubator, hatched))
                        OnEggHatched(incubator, hatched);
                }
               
                //egg hatch specific delay
                await RandomDelay(_settings.EggHatchMinDelay, _settings.EggHatchMaxDelay);
            }

            if (checkOnly) return;

            //var kmWalked = playerStats.
            await PokeRoadieInventory.GetCachedInventory(_client);

            var incubators = (await _inventory.GetEggIncubators())
                .Where(x => x.UsesRemaining > 0 || x.ItemId == ItemId.ItemIncubatorBasicUnlimited)
                .OrderByDescending(x => x.ItemId == ItemId.ItemIncubatorBasicUnlimited)
                .ToList();

            var unusedEggs = (await _inventory.GetEggs())
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

                    var response = await _client.Inventory.UseItemEggIncubator(incubator.Id, egg.Id);
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
            Xml.Serializer.SerializeToFile(incubators, Path.Combine(eggDir, "Incubators.xml"));
        }

        private List<IncubatorData> GetIncubators()
        {
            var path = Path.Combine(eggDir, "Incubators.xml");
            if (!File.Exists(path)) return new List<IncubatorData>();
            return (List<IncubatorData>)Xml.Serializer.DeserializeFromFile(path, typeof(List<IncubatorData>));
        }

        private async Task UseRevives()
        {
            await PokeRoadieInventory.GetCachedInventory(_client);
            var pokemonList = await _inventory.GetPokemonToRevive();
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
                    var response = await _client.Inventory.UseItemRevive(potion, pokemon.Id);
                    if (response.Result == UseItemReviveResponse.Types.Result.Success)
                    {
                        PokeRoadieInventory.IsDirty = true;
                        Logger.Write($"Revived {pokemon.GetMinStats()} with {potion} ", LogLevel.Pokemon);
                        //raise event
                        if (OnUseRevive != null)
                        {
                            if (!RaiseSyncEvent(OnUseRevive, potion, pokemon))
                                OnUseRevive(potion, pokemon);
                        }
                        
                    }
                    else
                    {
                        Logger.Write($"Failed to revive {pokemon.GetMinStats()} with {potion} - {response.Result}", LogLevel.Error);
                    }
                    await RandomDelay();
                }
            }
        }

        public async Task UseIncense()
        {
            if (CanCatch && _settings.UseIncense && (!_nextIncenseTime.HasValue || _nextIncenseTime.Value < DateTime.Now))
            {
                var inventory = await _inventory.GetItems();
                var WorstIncense = inventory.FirstOrDefault(p => p.ItemId == ItemId.ItemIncenseOrdinary);
                if (WorstIncense == null || WorstIncense.Count <= 0) return;

                var response = await _client.Inventory.UseIncense(ItemId.ItemIncenseOrdinary);
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
            if (state.Any())
            {

                //legal screen
                if (state.Contains(TutorialState.LegalScreen))
                    await TutorialLegalScreen();

                //avatar
                if (state.Contains(TutorialState.AvatarSelection))
                    await TutorialSetAvatar();

                if (state.Contains(TutorialState.AccountCreation))
                    await TutorialAccountCreation();

                //first time
                if (state.Contains(TutorialState.FirstTimeExperienceComplete))
                    await TutorialFirstTimeExperience();

                //capture
                if (state.Contains(TutorialState.PokemonCapture))
                    await TutorialCapture();

                //name
                if (state.Contains(TutorialState.NameSelection))
                    await TutorialSetCodename();

                //pokestop
                if (state.Contains(TutorialState.PokestopTutorial))
                    await TutorialPokestop();

            }
        }

        public async Task TutorialFirstTimeExperience()
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(TutorialState.FirstTimeExperienceComplete)) return;
            tutorialAttempts.Add(TutorialState.FirstTimeExperienceComplete);

            //hummanize
            Logger.Write("We haven't done the \"First-Time\" Tutorial... Pausing to pretend we are listening to PW.");
            await RandomDelay(10000, 30000);

            var result = await _inventory.TutorialMarkComplete(TutorialState.FirstTimeExperienceComplete,_playerProfile.PlayerData.ContactSettings.SendMarketingEmails, _playerProfile.PlayerData.ContactSettings.SendPushNotifications);
            if (result.Success)
            {
                //remove cached tutorial entry, so we do not try again before player data is updated.
                _playerProfile.PlayerData = result.PlayerData;

                Logger.Write($"Completed first-time experience tutorial.", LogLevel.Tutorial);
            }
            else
            {
                Logger.Write($"Could not complete the first-time experience tutorial.", LogLevel.Error);
            }
            await RandomDelay(10000, 20000);
        }
        public async Task TutorialSetAvatar()
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(TutorialState.AvatarSelection)) return;
            tutorialAttempts.Add(TutorialState.AvatarSelection);

            //hummanize
            Logger.Write("Found we don't have an avatar... Pausing to pretend we are looking at the menus.");
            await RandomDelay(10000, 30000);

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

            var response = await _inventory.TutorialSetAvatar(avatar);
            if (response.Status == SetAvatarResponse.Types.Status.Success)
            {

                await RandomDelay(10000, 30000);

                var result = await _inventory.TutorialMarkComplete(TutorialState.AvatarSelection, _playerProfile.PlayerData.ContactSettings.SendMarketingEmails, _playerProfile.PlayerData.ContactSettings.SendPushNotifications);
                if (result.Success)
                {
                    //remove cached tutorial entry, so we do not try again before player data is updated.
                    _playerProfile.PlayerData = result.PlayerData;

                    Logger.Write($"Player avatar generated!", LogLevel.Tutorial);
                }
                else
                {
                    Logger.Write($"TutorialMarkComplete Failed to complete the player avatar: {response.Status}", LogLevel.Error);
                }

            }
            else
            {
                Logger.Write($"Failed to generate player avatar: {response.Status}", LogLevel.Error);
            }

            if (_settings.ShowDebugMessages)
                Logger.Write($"Backpack:{avatar.Backpack}|Eyes:{avatar.Eyes}|Gender:{avatar.Gender}|Hair:{avatar.Hair}|Hat:{avatar.Hat}|Pants:{avatar.Pants}|Shirt:{avatar.Shirt}|Shoes:{avatar.Shoes}|Skin:{avatar.Skin}", LogLevel.Debug);

            await RandomDelay(5000, 10000);

        }
        public async Task TutorialLegalScreen()
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(TutorialState.LegalScreen)) return;
            tutorialAttempts.Add(TutorialState.LegalScreen);

            //hummanize
            await RandomDelay(2500, 5000);

            var result = await _inventory.TutorialMarkComplete(TutorialState.LegalScreen, _playerProfile.PlayerData.ContactSettings.SendMarketingEmails, _playerProfile.PlayerData.ContactSettings.SendPushNotifications);
            //remove cached tutorial entry, so we do not try again before player data is updated.
            _playerProfile.PlayerData.TutorialState.Remove(TutorialState.LegalScreen);
            await RandomDelay(10000, 20000);
        }

        public async Task TutorialPokestop()
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(TutorialState.PokestopTutorial)) return;
            tutorialAttempts.Add(TutorialState.PokestopTutorial);

            //hummanize
            Logger.Write("We have not finished the pokestop tutorial... Pausing to pretend we are listening.");
            await RandomDelay(10000, 30000);

            var result = await _inventory.TutorialMarkComplete(TutorialState.PokestopTutorial, _playerProfile.PlayerData.ContactSettings.SendMarketingEmails, _playerProfile.PlayerData.ContactSettings.SendPushNotifications);
            if (result.Success)
            {
                //remove cached tutorial entry, so we do not try again before player data is updated.
                _playerProfile.PlayerData = result.PlayerData;
                Logger.Write($"Completed the pokestop tutorial.", LogLevel.Tutorial);
            }
            else
            {
                Logger.Write($"Could not complete the pokestop tutorial.", LogLevel.Error);
            }
            await RandomDelay(10000, 20000);
        }

        public async Task TutorialAccountCreation()
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(TutorialState.AccountCreation)) return;
            tutorialAttempts.Add(TutorialState.AccountCreation);

            //hummanize
            Logger.Write("We have not finished account creation...");
            await RandomDelay(5000, 10000);

            var result = await _inventory.TutorialMarkComplete(TutorialState.AccountCreation, _playerProfile.PlayerData.ContactSettings.SendMarketingEmails, _playerProfile.PlayerData.ContactSettings.SendPushNotifications);
            if (result.Success)
            {
                //remove cached tutorial entry, so we do not try again before player data is updated.
                _playerProfile.PlayerData = result.PlayerData;

                Logger.Write($"Completed the account creation.", LogLevel.Tutorial);
            }
            else
            {
                Logger.Write($"Could not complete the account creation.", LogLevel.Error);
            }
            await RandomDelay(10000, 20000);
        }


        private void ProcessCaptureAward(CaptureAward awards)
        {
            if (awards == null) return;
            if (awards.Xp.Count > 0)
                foreach (var i in awards.Xp)
                    if (i > 0) Logger.Write($"Received {i} Xp!", LogLevel.Info);
            if (awards.Candy.Count > 0)
                foreach (var i in awards.Candy)
                    if (i > 0) Logger.Write($"Received {i} Candy!", LogLevel.Info);
            if (awards.Stardust.Count > 0)
                foreach (var i in awards.Stardust)
                    if (i > 0) Logger.Write($"Received {i} Stardust!", LogLevel.Info);

        }
        public async Task TutorialCapture()
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(TutorialState.PokemonCapture)) return;
            tutorialAttempts.Add(TutorialState.PokemonCapture);

            //hummanize
            Logger.Write("We have not finished the pokemon capture tutorial... Pausing to pretend we are thinking hard about it.");
            await RandomDelay(10000, 30000);

            var result = await _inventory.TutorialPokemonCapture(_settings.TutorialPokmonId);
            if (result.Result == EncounterTutorialCompleteResponse.Types.Result.Success)
            {
                //remove cached tutorial entry, so we do not try again before player data is updated.
                _playerProfile.PlayerData.TutorialState.Remove(TutorialState.PokemonCapture);

                Logger.Write($"Completed the pokemon capture tutorial", LogLevel.Tutorial);
                Logger.Write($"Received {result.PokemonData.GetMinStats()}", LogLevel.Pokemon);
                ProcessCaptureAward(result.CaptureAward);

                //hummanize
                Logger.Write("We are now waiting for the pokedex entry...");
                await RandomDelay(10000, 30000);

            }
            else
            {
                Logger.Write($"Could not complete the pokemon capture tutorial - {result.Result}.", LogLevel.Error);
            }
            await RandomDelay(10000, 20000);
        }

        public async Task TutorialSetCodename()
        {
            //1 attempt per session
            if (tutorialAttempts.Contains(TutorialState.NameSelection)) return;
            tutorialAttempts.Add(TutorialState.NameSelection);

            var name = _settings.TutorialCodename;
            if (_settings.TutorialGenerateCodename)
            {
                var suggestedNames = await _inventory.TutorialGetSuggestedCodenames();
                if (suggestedNames.Success && suggestedNames.Codenames != null && suggestedNames.Codenames.Count > 0)
                {
                    var randomIndex = Random.Next(0, suggestedNames.Codenames.Count - 1);
                    name = suggestedNames.Codenames[randomIndex];
                }
                else
                {
                    Logger.Write($"Failed to generate a name, no suggested names returned.", LogLevel.Error);
                    return;
                }
            }

             if (!string.IsNullOrWhiteSpace(name))
            {
                var response = await _client.Misc.ClaimCodename(name);
                if (response.Status == ClaimCodenameResponse.Types.Status.Success)
                {

                    //remove cached tutorial entry, so we do not try again before player data is updated.
                    _playerProfile.PlayerData.TutorialState.Remove(TutorialState.NameSelection);

                    Logger.Write($"Name claimed : {name}", LogLevel.Tutorial);
                    await RandomDelay();
                }
                else
                {
                    Logger.Write($"Failed to claim name {name}. {response.Status} - {response.UserMessage}", LogLevel.Error);
                }
            }
        }

        #endregion
    }
}
 
