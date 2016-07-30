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

#endregion


namespace PokeRoadie
{
    public class PokeRoadieLogic
    {
        public event Func<bool> ShowEditCredentials;

        private readonly PokeRoadieClient _client;
        private readonly PokeRoadieInventory _inventory;
        private readonly Statistics _stats;
        private readonly PokeRoadieNavigation _navigation;
        private GetPlayerResponse _playerProfile;
        private string configs_path = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
        private int recycleCounter = 0;
        private bool IsInitialized = false;

        private int fleeCounter = 0;
        private DateTime? fleeLast;
        private bool softBan = false;
        private bool hasDisplayedTransferSettings;

        public PokeRoadieLogic()
        {
            _client = new PokeRoadieClient();
            _inventory = new PokeRoadieInventory(_client);
            _stats = new Statistics();
            _navigation = new PokeRoadieNavigation(_client);
        }

        public async Task CloseApplication(int exitCode)
        {
            for (int i = 3; i > 0; i--)
            {
                Logger.Write($"PokeRoadie will be closed in {i * 5} seconds!", LogLevel.Warning);
                await Task.Delay(5000);
            }
            await Task.Delay(15000);
            System.Environment.Exit(exitCode);
        }

        public async Task Execute()
        {
            Git.CheckVersion();

            //check pokestop dir
            var pokestopsDir = Path.Combine(Directory.GetCurrentDirectory(), "Pokestops");
            if (!Directory.Exists(pokestopsDir)) Directory.CreateDirectory(pokestopsDir);

            //check pokestop dir
            var gymDir = Path.Combine(Directory.GetCurrentDirectory(), "Gyms");
            if (!Directory.Exists(pokestopsDir)) Directory.CreateDirectory(pokestopsDir);


            if (PokeRoadieSettings.Current.CurrentLatitude == 0 || PokeRoadieSettings.Current.CurrentLongitude == 0)
            {
                Logger.Write($"Please change first Latitude and/or Longitude because currently your using default values!", LogLevel.Error);
            }
            else
            {
                Logger.Write($"Make sure Lat & Lng is right. Exit Program if not! Lat: {_client.CurrentLatitude} Lng: {_client.CurrentLongitude}", LogLevel.Warning);
                for (int i = 3; i > 0; i--)
                {
                    Logger.Write($"Script will continue in {i * 5} seconds!", LogLevel.Warning);
                    await Task.Delay(5000);
                }
            }

            Logger.Write($"Logging in via: {PokeRoadieSettings.Current.AuthType}", LogLevel.Info);
            while (true)
            {
                try
                {
                    switch (PokeRoadieSettings.Current.AuthType)
                    {
                        case AuthType.Ptc:
                            await _client.Login.DoPtcLogin(PokeRoadieSettings.Current.Username, PokeRoadieSettings.Current.Password);
                            break;
                        case AuthType.Google:
                            await _client.Login.DoGoogleLogin(PokeRoadieSettings.Current.Username, PokeRoadieSettings.Current.Password);
                            break;
                        default:
                            Logger.Write("wrong AuthType");
                            Environment.Exit(0);
                            break;
                    }

                    await PostLoginExecute();
                }
                catch(PtcOfflineException e)
                {
                    Logger.Write("(LOGIN ERROR) The Ptc servers are currently offline. Please try again later. " + e.Message, LogLevel.Info, ConsoleColor.Red);
                    await Task.Delay(15000);
                    Environment.Exit(2);
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("NeedsBrowser"))
                    {
                        Logger.Write("(LOGIN ERROR) Please login to your google account and turn off 'Two-Step Authentication' under security settings. If you do NOT want to disable your two-factor auth, please visit the following link and setup an app password. This is the only way of using the bot without disabling two-factor authentication: https://security.google.com/settings/security/apppasswords. Trying automatic restart in 15 seconds...", LogLevel.Info, ConsoleColor.Red);
                        await Task.Delay(15000);
                    }
                    else if (e.Message.Contains("BadAuthentication"))
                    {
                        Logger.Write("(LOGIN ERROR) The username and password provided failed. " + e.Message, LogLevel.Info, ConsoleColor.Red);
                        if (ShowEditCredentials != null)
                        {
                            var result = ShowEditCredentials.Invoke();
                            if (!result)
                            {
                                Logger.Write("Username and password for login not provided. Login screen closed.");
                                await CloseApplication(0);
                            }
                        }
                    }
                    else
                    {
                        Logger.Write($"(FATAL ERROR) Unhandled exception encountered: {e.Message.ToString()}.");
                        Logger.Write("Restarting the application due to error...", LogLevel.Warning);
                    }
                    await Execute();
                }
                
            }
        }

        private async Task WriteStats()
        {
            if (!_client.RefreshEndDate.HasValue || _client.RefreshEndDate.Value <= DateTime.Now)
            {
                await PokeRoadieInventory.getCachedInventory(_client);
                _playerProfile = await _client.Player.GetPlayer();
                var playerName = Statistics.GetUsername(_client, _playerProfile);
                _stats.UpdateConsoleTitle(_client, _inventory);
                var currentLevelInfos = await Statistics._getcurrentLevelInfos(_inventory);

                Logger.Write("====== User Info ======", LogLevel.None, ConsoleColor.Yellow);
                if (PokeRoadieSettings.Current.AuthType == AuthType.Ptc)
                    Logger.Write($"PTC Account: {playerName}\n", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Name: {playerName}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Team: {_playerProfile.PlayerData.Team}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Level: {currentLevelInfos}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"Stardust: {_playerProfile.PlayerData.Currencies.ToArray()[1].Amount}", LogLevel.None, ConsoleColor.White);
                var items = await _inventory.GetItems();
                Logger.Write($"====== Items ({items.Select(x=>x.Count).Sum()}) ======", LogLevel.None, ConsoleColor.Yellow);
                foreach (var item in items)
                {
                    Logger.Write($"{(item.ItemId).ToString().Replace("Item","")} x {item.Count}", LogLevel.None, ConsoleColor.White);
                }
                await DisplayHighests();
                _client.RefreshEndDate = DateTime.Now.AddMinutes(PokeRoadieSettings.Current.DisplayRefreshMinutes);
            }

        } 

        public async Task PostLoginExecute()
        {
            Logger.Write($"Client logged in", LogLevel.Info);

            while (true)
            {
                if (!IsInitialized)
                {

                    //write stats
                    await WriteStats();

                    //get ignore lists
                    var PokemonsNotToTransfer = PokeRoadieSettings.Current.PokemonsNotToTransfer;
                    var PokemonsNotToCatch = PokeRoadieSettings.Current.PokemonsNotToCatch;
                    var PokemonsToEvolve = PokeRoadieSettings.Current.PokemonsToEvolve;

                    //evolve
                    if (PokeRoadieSettings.Current.EvolvePokemon || PokeRoadieSettings.Current.EvolveOnlyPokemonAboveIV) await EvolvePokemon(PokeRoadieSettings.Current.PokemonsToEvolve);

                    //transfer
                    if (PokeRoadieSettings.Current.TransferPokemon) await TransferPokemon();

                    //export
                    await _inventory.ExportPokemonToCSV(_playerProfile.PlayerData);

                    //recycle
                    await RecycleItems();
                }
                IsInitialized = true;
                await ExecuteFarmingPokestopsAndPokemons(PokeRoadieSettings.Current.UseGPXPathing);

                /*
                * Example calls below
                *
                var profile = await _client.GetProfile();
                var settings = await _client.GetSettings();
                var mapObjects = await _client.GetMapObjects();
                var inventory = await _client.GetInventory();
                var pokemons = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.Pokemon).Where(p => p != null && p?.PokemonId > 0);
                */

                await Task.Delay(100);
            }
        }

        private async Task ExecuteFarmingPokestopsAndPokemons(bool path)
        {

            if (!path)
                await ExecuteFarmingPokestopsAndPokemons();
            else
            {
                var tracks = GetGpxTracks();
                var curTrkPt = 0;
                var curTrk = 0;
                var maxTrk = tracks.Count - 1;
                var curTrkSeg = 0;

                //check pokestop dir
                var pokestopsDir = Path.Combine(Directory.GetCurrentDirectory(), "Pokestops");
                if (!Directory.Exists(pokestopsDir)) Directory.CreateDirectory(pokestopsDir);

                while (curTrk <= maxTrk)
                {
                    var track = tracks.ElementAt(curTrk);
                    var trackSegments = track.Segments;
                    var maxTrkSeg = trackSegments.Count - 1;
                    while (curTrkSeg <= maxTrkSeg)
                    {
                        var trackPoints = track.Segments.ElementAt(0).TrackPoints;
                        var maxTrkPt = trackPoints.Count - 1;
                        while (curTrkPt <= maxTrkPt)
                        {
                            var nextPoint = trackPoints.ElementAt(curTrkPt);
                            if (
                                LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude,
                                    Convert.ToDouble(nextPoint.Lat), Convert.ToDouble(nextPoint.Lon)) > 5000)
                            {
                                Logger.Write(
                                    $"Your desired destination of {nextPoint.Lat}, {nextPoint.Lon} is too far from your current position of {_client.CurrentLatitude}, {_client.CurrentLongitude}",
                                    LogLevel.Error);
                                break;
                            }

                            Logger.Write(
                                $"Your desired destination is {nextPoint.Lat}, {nextPoint.Lon} your location is {_client.CurrentLatitude}, {_client.CurrentLongitude}",
                                LogLevel.Warning);

                            // Wasn't sure how to make this pretty. Edit as needed.
                            var mapObjects = await _client.Map.GetMapObjects();
                            var pokeStops =
                                mapObjects.MapCells.SelectMany(i => i.Forts)
                                    .Where(
                                        i =>
                                            i.Type != FortType.Gym &&
                                            i.Type == FortType.Checkpoint &&
                                            i.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime() &&
                                            ( // Make sure PokeStop is within 40 meters, otherwise we cannot hit them.
                                                LocationUtils.CalculateDistanceInMeters(
                                                    _client.CurrentLatitude, _client.CurrentLongitude,
                                                    i.Latitude, i.Longitude) < 40)
                                    ).ToList();

                            await ProcessPokeStopList(pokeStops, mapObjects);
                           
                            Func<Task> del = null;
                            if (PokeRoadieSettings.Current.CatchPokemon && !softBan)
                                del = ExecuteCatchAllNearbyPokemons;

                            await
                                _navigation.HumanPathWalking(trackPoints.ElementAt(curTrkPt),
                                    PokeRoadieSettings.Current.MinSpeed, del);

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

        private async Task ExecuteFarmingPokestopsAndPokemons()
        {

            if (!PokeRoadieSettings.Current.VisitGyms && !PokeRoadieSettings.Current.VisitPokestops)
            {
                Logger.Write("Both VisitGyms and VisitPokestops are false... This is boring.");
                
                await RandomHelper.RandomDelay(2500);

                if (PokeRoadieSettings.Current.CatchPokemon && !softBan)
                    await ExecuteCatchAllNearbyPokemons();

            }

            var distanceFromStart = LocationUtils.CalculateDistanceInMeters(
            _client.CurrentLatitude, _client.CurrentLongitude,
            _client.CurrentLatitude, _client.CurrentLongitude);

            // Edge case for when the client somehow ends up outside the defined radius
            if (PokeRoadieSettings.Current.MaxDistance != 0 &&
                distanceFromStart > PokeRoadieSettings.Current.MaxDistance)
            {

                if (PokeRoadieSettings.Current.FlyingEnabled)
                    Logger.Write($"Boarded flight #{RandomHelper.RandomNumber(101,501)}", LogLevel.Navigation, ConsoleColor.White);
                else
                {
                    Logger.Write("Hopped in car", LogLevel.Navigation, ConsoleColor.White);
                }

                Func<Task> del = null;
                if (PokeRoadieSettings.Current.CatchPokemon && !softBan && !PokeRoadieSettings.Current.FlyingEnabled || (PokeRoadieSettings.Current.FlyingEnabled && PokeRoadieSettings.Current.CatchWhileFlying)) del = ExecuteCatchAllNearbyPokemons;
                var ToStart = await _navigation.HumanLikeWalking(
                    new GeoCoordinate(_client.CurrentLatitude , _client.CurrentLongitude,_client.CurrentAltitude),
                    PokeRoadieSettings.Current.FlyingEnabled ? PokeRoadieSettings.Current.FlyingSpeed: PokeRoadieSettings.Current.MinSpeed, del);

                Logger.Write($"Arrived at destination", LogLevel.Navigation);
            }

            //if destinations are enabled
            if (PokeRoadieSettings.Current.DestinationsEnabled)
            {
                if (PokeRoadieSettings.Current.DestinationEndDate.HasValue)
                {
                    if (DateTime.Now > PokeRoadieSettings.Current.DestinationEndDate.Value)
                    {

                        if (PokeRoadieSettings.Current.Destinations != null && PokeRoadieSettings.Current.Destinations.Count > 1)
                        {
                            //get new destination index
                            var newIndex = PokeRoadieSettings.Current.DestinationIndex + 1 >= PokeRoadieSettings.Current.Destinations.Count ? 0 : PokeRoadieSettings.Current.DestinationIndex + 1;
                            //get coords
                            var destination = PokeRoadieSettings.Current.Destinations[newIndex];

                            //set new index and default location
                            PokeRoadieSettings.Current.DestinationIndex = newIndex;
                            PokeRoadieSettings.Current.CurrentLatitude = destination.Latitude;
                            PokeRoadieSettings.Current.CurrentLongitude = destination.Longitude;
                            PokeRoadieSettings.Current.CurrentAltitude = destination.Altitude;
                            PokeRoadieSettings.Current.Save();

                            if (PokeRoadieSettings.Current.FlyingEnabled)
                                Logger.Write($"Boarded flight #{RandomHelper.RandomNumber(101, 501)}", LogLevel.Navigation, ConsoleColor.White);
                            else
                            {
                                Logger.Write("Hopped in car", LogLevel.None, ConsoleColor.White);
                            }
                            Logger.Write($"Moving to new destination - {destination.Name} - {destination.Latitude}:{destination.Longitude}", LogLevel.Navigation, ConsoleColor.White);

                            //fly to location
                            Func<Task> del = null;
                            if (PokeRoadieSettings.Current.CatchPokemon && !softBan && !PokeRoadieSettings.Current.FlyingEnabled || (PokeRoadieSettings.Current.FlyingEnabled && PokeRoadieSettings.Current.CatchWhileFlying)) del = ExecuteCatchAllNearbyPokemons;
                            var ToStart = await _navigation.HumanLikeWalking(
                                new GeoCoordinate(PokeRoadieSettings.Current.CurrentLatitude, PokeRoadieSettings.Current.CurrentLongitude , PokeRoadieSettings.Current.CurrentAltitude),
                                PokeRoadieSettings.Current.FlyingEnabled ? PokeRoadieSettings.Current.FlyingSpeed : PokeRoadieSettings.Current.MinSpeed, del);

                            //reset destination timer
                            PokeRoadieSettings.Current.DestinationEndDate = DateTime.Now.AddMinutes(PokeRoadieSettings.Current.MinutesPerDestination);

                            Logger.Write($"Arrived at destination - {destination.Name}!", LogLevel.Navigation, ConsoleColor.White);
                        }
                        else
                        {
                            PokeRoadieSettings.Current.DestinationEndDate = DateTime.Now.AddMinutes(PokeRoadieSettings.Current.MinutesPerDestination);
                        }
                    }
                }
                else
                {
                    PokeRoadieSettings.Current.DestinationEndDate = DateTime.Now.AddMinutes(PokeRoadieSettings.Current.MinutesPerDestination);
                }
            }
            
            var mapObjects = await _client.Map.GetMapObjects();

            var pokeStopList =
                PokeRoadieNavigation.pathByNearestNeighbour(
                mapObjects.MapCells.SelectMany(i => i.Forts)
                    .Where(i =>
                        i.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime() &&
                        (PokeRoadieSettings.Current.MaxDistance == 0 ||
                        LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, i.Latitude, i.Longitude) < PokeRoadieSettings.Current.MaxDistance))
                    .OrderBy(i =>
                        LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, i.Latitude, i.Longitude)).ToArray()).ToList();

            if (!PokeRoadieSettings.Current.VisitGyms)
                pokeStopList = pokeStopList.Where(x => x.Type != FortType.Gym).ToList();

            if (!PokeRoadieSettings.Current.VisitPokestops)
                pokeStopList = pokeStopList.Where(x => x.Type == FortType.Gym).ToList();

            if (pokeStopList.Count <= 0)
            {
                 Logger.Write("No usable PokeStops found in your area. Is your maximum distance too small?",
                                    LogLevel.Warning);

                if (PokeRoadieSettings.Current.MoveWhenNoStops && _client != null && PokeRoadieSettings.Current.DestinationEndDate.HasValue && PokeRoadieSettings.Current.DestinationEndDate.Value > DateTime.Now)
                    PokeRoadieSettings.Current.DestinationEndDate = DateTime.Now;
            }

            await ProcessPokeStopList(pokeStopList, mapObjects);
        }

        private async Task ProcessPokeStopList(List<FortData> pokeStopList, GetMapObjectsResponse mapObjects)
        {

            if (pokeStopList.Count == 0) return;
            var pokestopListWithLures = pokeStopList.Where(x => x.LureInfo != null).ToList();
            if (pokestopListWithLures.Count > 0)
            {
                Logger.Write($"(INFO) Found {pokestopListWithLures.Count()} with lure!", LogLevel.None, ConsoleColor.DarkMagenta);
                
                //if we are prioritizing stops with lures
                if (PokeRoadieSettings.Current.PrioritizeStopsWithLures)
                {
                    int counter = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        for (int x = 0; x < pokestopListWithLures.Count; x++)
                        {
                            pokeStopList.Insert(counter, pokestopListWithLures[x]);
                            counter++;
                        }
                    }
                }
            }

            //get counts
            var pokestopCount = pokeStopList.Where(x => x.Type != FortType.Gym).Count();
            var gymCount = pokeStopList.Where(x => x.Type == FortType.Gym).Count();

            var msg = $"Found {pokestopCount} {(pokestopCount == 1 ? "Pokestop" : "Pokestops")}";

            Logger.Write($"Found {pokestopCount} {(pokestopCount == 1 ? "Pokestop" : "Pokestops")} | {gymCount} {(gymCount == 1 ? "Gym" : "Gyms")}", LogLevel.Info);
            while (pokeStopList.Any())
            {
                if (PokeRoadieSettings.Current.DestinationsEnabled && PokeRoadieSettings.Current.DestinationEndDate.HasValue && DateTime.Now > PokeRoadieSettings.Current.DestinationEndDate.Value)
                {
                    break;
                }

                await WriteStats();

                var pokeStop = pokeStopList[0];
                pokeStopList.RemoveAt(0);
                if (pokeStop.Type != FortType.Gym)
                {
                    await ProcessPokeStop(pokeStop, mapObjects);
                }
                else
                {
                    await ProcessGym(pokeStop, mapObjects);
                }
                if (pokestopCount == 0 && gymCount > 0)
                    await RandomHelper.RandomDelay(1000, 2000);
                else
                    await RandomHelper.RandomDelay(50, 200);
            }

        }

        private async Task ProcessGym(FortData pokeStop, GetMapObjectsResponse mapObjects)
        {
            var fortInfo = await _client.Fort.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
            if (fortInfo != null)
            {
                bool inRange = false;
                int attempts = 0;

                do
                {
                    var distance = LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, pokeStop.Latitude, pokeStop.Longitude);
                    var fortDetails = await _client.Fort.GetGymDetails(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                    if (fortDetails.Result == GetGymDetailsResponse.Types.Result.Success)
                    {
                        inRange = true;
                        var fortString = $"{ fortDetails.Name} | { fortDetails.GymState.FortData.OwnedByTeam } | { pokeStop.GymPoints} | { fortDetails.GymState.Memberships.Count}";
                        if (fortDetails.Result == GetGymDetailsResponse.Types.Result.Success)
                        {
                            if (fortDetails.GymState.FortData.OwnedByTeam == _playerProfile.PlayerData.Team &&
                                ((pokeStop.GymPoints < 3001 && fortDetails.GymState.Memberships.Count < 2) ||
                                (pokeStop.GymPoints > 3000 && pokeStop.GymPoints < 7001 && fortDetails.GymState.Memberships.Count < 2) ||
                                (pokeStop.GymPoints > 6000 && pokeStop.GymPoints < 10001 && fortDetails.GymState.Memberships.Count < 3) ||
                                (pokeStop.GymPoints > 10000 && fortDetails.GymState.Memberships.Count < 4)))
                            {

                                Logger.Write($"(GYM) Casing out {fortDetails.Name} in {distance:0.##} m distance", LogLevel.None, ConsoleColor.Cyan);

                                //var gymDir = Path.Combine(Directory.GetCurrentDirectory(), "Gyms");
                                //try
                                //{
                                //    var fortFile = Path.Combine(gymDir, pokeStop.Id + ".txt");
                                //    if (!File.Exists(fortFile))
                                //    {
                                //        using (StreamWriter w = File.CreateText(fortFile))
                                //        {
                                //            w.WriteLine(fortInfo.FortId.Replace((char)13, ' ').Replace((char)10, ' ').Replace("  ", " "));
                                //            w.WriteLine(fortInfo.Type);
                                //            w.WriteLine(fortInfo.Latitude);
                                //            w.WriteLine(fortInfo.Longitude);
                                //            w.WriteLine(fortInfo.Name.Replace((char)13, ' ').Replace((char)10, ' ').Replace("  ", " "));
                                //            w.WriteLine(fortInfo.Description.Replace((char)13, ' ').Replace((char)10, ' ').Replace("  ", " "));
                                //            w.WriteLine();
                                //            foreach (var img in fortInfo.ImageUrls)
                                //            {
                                //                w.WriteLine(img);
                                //            }
                                //            w.Close();
                                //        }
                                //    }
                                //}
                                //catch (Exception e)
                                //{
                                //    Logger.Write("Could not save the pokestop information file. " + e.ToString(), LogLevel.Error);
                                //}


                                Func<Task> del = null;
                                if (PokeRoadieSettings.Current.CatchPokemon && !softBan) del = ExecuteCatchAllNearbyPokemons;
                                var update = await _navigation.HumanLikeWalking(new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude), PokeRoadieSettings.Current.MinSpeed, del);

                                fortInfo = await _client.Fort.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                                fortDetails = await _client.Fort.GetGymDetails(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                                fortString = $"{ fortDetails.Name} | { fortDetails.GymState.FortData.OwnedByTeam } | { pokeStop.GymPoints} | { fortDetails.GymState.Memberships.Count}";


                                if (fortDetails.GymState.FortData.OwnedByTeam == _playerProfile.PlayerData.Team &&
                                    ((pokeStop.GymPoints < 3001 && fortDetails.GymState.Memberships.Count < 2) ||
                                    (pokeStop.GymPoints > 3000 && pokeStop.GymPoints < 7001 && fortDetails.GymState.Memberships.Count < 2) ||
                                    (pokeStop.GymPoints > 6000 && pokeStop.GymPoints < 10001 && fortDetails.GymState.Memberships.Count < 3) ||
                                    (pokeStop.GymPoints > 10000 && fortDetails.GymState.Memberships.Count < 4)))
                                {

                                    await PokeRoadieInventory.getCachedInventory(_client, true);
                                    var pokemonList = await _inventory.GetHighestsVNotDeployed(1);
                                    var pokemon = pokemonList.FirstOrDefault();
                                    if (pokemon != null)
                                    {

                                        var response = await _client.Fort.FortDeployPokemon(fortInfo.FortId, pokemon.Id);
                                        if (response.Result == FortDeployPokemonResponse.Types.Result.Success)
                                        {
                                            Logger.Write($"(GYM) Deployed {pokemon.GetMinStats()} to {fortDetails.Name}", LogLevel.None, ConsoleColor.Green);
                                        }
                                        else
                                        {
                                            Logger.Write($"(GYM) Deployment Failed at {fortString} - {response.Result}", LogLevel.None, ConsoleColor.Green);
                                        }
                                    }
                                }
                                else
                                {
                                    Logger.Write($"(GYM) Wasted walk on {fortString}", LogLevel.None, ConsoleColor.Cyan);
                                }
                            }
                            else
                            {
                                Logger.Write($"(GYM) Ignoring {fortString}", LogLevel.None, ConsoleColor.Cyan);
                            }
                        }
                    }
                    else if (fortDetails.Result == GetGymDetailsResponse.Types.Result.ErrorNotInRange)
                    {
                        attempts++;
                        Func<Task> del = null;
                        if (PokeRoadieSettings.Current.CatchPokemon && !softBan && !PokeRoadieSettings.Current.FlyingEnabled || (PokeRoadieSettings.Current.FlyingEnabled && PokeRoadieSettings.Current.CatchWhileFlying)) del = ExecuteCatchAllNearbyPokemons;
                        Logger.Write($"(GYM) Moving closer to {fortInfo.Name} - {fortDetails.Result}", LogLevel.None, ConsoleColor.Cyan);
                        var ToStart = await _navigation.HumanLikeWalkingGetCloser(
                            new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude, _client.CurrentAltitude),
                            PokeRoadieSettings.Current.FlyingEnabled ? PokeRoadieSettings.Current.FlyingSpeed : PokeRoadieSettings.Current.MinSpeed, del, 0.20);

                    }
                    else
                    {
                        Logger.Write($"(GYM) Ignoring {fortInfo.Name} - {fortDetails.Result}", LogLevel.None, ConsoleColor.Cyan);
                        inRange = true;
                    }

                } while (!inRange && attempts < 6);
            }
        }
        private async Task ProcessPokeStop(FortData pokeStop, GetMapObjectsResponse mapObjects)
        {

            var pokestopsDir = Path.Combine(Directory.GetCurrentDirectory(), "Pokestops");
            if (PokeRoadieSettings.Current.CatchPokemon && !softBan)
                await ExecuteCatchAllNearbyPokemons();

            var distance = LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, pokeStop.Latitude, pokeStop.Longitude);
            var fortInfo = await _client.Fort.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);

            try
            {
                var fortFile = Path.Combine(pokestopsDir, pokeStop.Id + ".txt");
                if (!File.Exists(fortFile))
                {
                    using (StreamWriter w = File.CreateText(fortFile))
                    {
                        w.WriteLine(fortInfo.FortId.Replace((char)13, ' ').Replace((char)10, ' ').Replace("  ", " "));
                        w.WriteLine(fortInfo.Type);
                        w.WriteLine(fortInfo.Latitude);
                        w.WriteLine(fortInfo.Longitude);
                        w.WriteLine(fortInfo.Name.Replace((char)13, ' ').Replace((char)10, ' ').Replace("  ", " "));
                        w.WriteLine(fortInfo.Description.Replace((char)13, ' ').Replace((char)10, ' ').Replace("  ", " "));
                        w.WriteLine();
                        foreach (var img in fortInfo.ImageUrls)
                        {
                            w.WriteLine(img);
                        }
                        w.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Write("Could not save the pokestop information file. " + e.ToString(), LogLevel.Error);
            }

            var name = $"Name: {fortInfo.Name}{(pokeStop.LureInfo == null ? "" : " WITH LURE")} in {distance:0.##} m distance";
            Logger.Write(name, LogLevel.Pokestop);

            Func<Task> del = null;
            if (PokeRoadieSettings.Current.CatchPokemon && !softBan) del = ExecuteCatchAllNearbyPokemons;
            var update = await _navigation.HumanLikeWalking(new GeoCoordinate(pokeStop.Latitude, pokeStop.Longitude), PokeRoadieSettings.Current.MinSpeed, del);

            var fortSearch = await _client.Fort.SearchFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
            if (fortSearch.ExperienceAwarded > 0)
            {
                _stats.AddExperience(fortSearch.ExperienceAwarded);
                _stats.UpdateConsoleTitle(_client, _inventory);
                string EggReward = fortSearch.PokemonDataEgg != null ? "1" : "0";
                if (!softBan) Logger.Write($"XP: {fortSearch.ExperienceAwarded}, Gems: {fortSearch.GemsAwarded}, Eggs: {EggReward}, Items: {StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch.ItemsAwarded)}", LogLevel.Pokestop);
                recycleCounter++;

                //reset ban
                if (softBan)
                {
                    softBan = false;
                    fleeCounter = 0;
                    fleeLast = null;
                    Logger.Write("(SOFT BAN) The ban was lifted!", LogLevel.None, ConsoleColor.DarkRed);
                }

            }
            else if (fortSearch.Result == FortSearchResponse.Types.Result.Success)
            {
                fleeCounter++;
                if (fleeLast.HasValue && fleeLast.Value.AddMinutes(3) > DateTime.Now && fleeCounter > 3 && !softBan)
                {
                    softBan = true;
                    Logger.Write("(SOFT BAN) Detected a soft ban, let's walk it off!", LogLevel.None, ConsoleColor.DarkRed);
                }

                fleeLast = DateTime.Now;
                fleeLast = DateTime.Now;
            }
            
            if (PokeRoadieSettings.Current.LoiteringActive && pokeStop.LureInfo != null)
            {
                Logger.Write($"Loitering: {fortInfo.Name} has a lure we can milk!", LogLevel.Info);
                while (PokeRoadieSettings.Current.LoiteringActive && pokeStop.LureInfo != null)
                {
                    if (PokeRoadieSettings.Current.CatchPokemon && !softBan)
                        await ExecuteCatchAllNearbyPokemons();

                    var fortSearch2 = await _client.Fort.SearchFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                    if (fortSearch2.ExperienceAwarded > 0)
                    {
                        _stats.AddExperience(fortSearch2.ExperienceAwarded);
                        _stats.UpdateConsoleTitle(_client, _inventory);
                        string EggReward = fortSearch2.PokemonDataEgg != null ? "1" : "0";
                        Logger.Write($"XP: {fortSearch2.ExperienceAwarded}, Gems: {fortSearch2.GemsAwarded}, Eggs: {EggReward}, Items: {StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch2.ItemsAwarded)}", LogLevel.Pokestop);
                        recycleCounter++;
                    }

                    if (recycleCounter >= 5)
                        await RecycleItems();

                    await RandomHelper.RandomDelay(15000, 30000);
                    pokeStop = mapObjects.MapCells.SelectMany(i => i.Forts).Where(x => x.Id == pokeStop.Id).FirstOrDefault();
                    if (pokeStop.LureInfo != null) Logger.Write($"Loitering: {fortInfo.Name} still has a lure, chillin out!", LogLevel.Info);
                }
            }

            await RandomHelper.RandomDelay(50, 200);
            if (recycleCounter >= 5)
                await RecycleItems();
        }

        private async Task CatchEncounter(EncounterResponse encounter, MapPokemon pokemon)
        {
            CatchPokemonResponse caughtPokemonResponse;
            var attemptCounter = 1;
            do
            {

                //if there has not been a consistent flee, reset
                if (fleeCounter > 0 && fleeLast.HasValue && fleeLast.Value.AddMinutes(3) < DateTime.Now && !softBan)
                {
                    fleeCounter = 0;
                    fleeLast = null;
                }

                var probability = encounter?.CaptureProbability?.CaptureProbability_?.FirstOrDefault();
                var bestPokeball = await GetBestBall(encounter);
                if (bestPokeball == ItemId.ItemUnknown)
                {
                    Logger.Write($"You don't own any Pokeballs :( - We missed a {pokemon.PokemonId} with CP {encounter?.WildPokemon?.PokemonData?.Cp}", LogLevel.Warning);
                    return;
                }

                //only use crappy pokeballs when they are fleeing
                if (fleeCounter > 1) bestPokeball = ItemId.ItemPokeBall;

                var bestBerry = await GetBestBerry(encounter);
                //only use berries when they are fleeing
                if (fleeCounter == 0)
                {
                    var inventoryBerries = await _inventory.GetItems();
                    var berries = inventoryBerries.Where(p => p.ItemId == bestBerry).FirstOrDefault();
                    if (bestBerry != ItemId.ItemUnknown && probability.HasValue && probability.Value < 0.35)
                    {
                        await _client.Encounter.UseCaptureItem(pokemon.EncounterId, bestBerry, pokemon.SpawnPointId);
                        berries.Count--;
                        Logger.Write($"{bestBerry} used, remaining: {berries.Count}", LogLevel.Berry);
                        await RandomHelper.RandomDelay(50, 200);
                    }
                }

                var distance = LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, pokemon.Latitude, pokemon.Longitude);
                caughtPokemonResponse = await _client.Encounter.CatchPokemon(pokemon.EncounterId, pokemon.SpawnPointId, bestPokeball);

                if (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
                {
                    //reset soft ban info
                    fleeCounter = 0;
                    fleeLast = null;
                    softBan = false;

                    foreach (var xp in caughtPokemonResponse.CaptureAward.Xp)
                        _stats.AddExperience(xp);
                    _stats.IncreasePokemons();
                    var profile = await _client.Player.GetPlayer();
                    _stats.GetStardust(profile.PlayerData.Currencies.ToArray()[1].Amount);
                }
                _stats.UpdateConsoleTitle(_client, _inventory);

                //calculate if we are in a soft ban
                if (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchFlee)
                {
                    fleeCounter++;
                    if (fleeLast.HasValue && fleeLast.Value.AddMinutes(3) > DateTime.Now && fleeCounter > 3 && !softBan)
                    {
                        softBan = true;
                        Logger.Write("(SOFT BAN) Detected a soft ban, let's walk it off!", LogLevel.None, ConsoleColor.DarkRed);
                        
                    }    
                    fleeLast = DateTime.Now; 
                }

                if (encounter?.CaptureProbability?.CaptureProbability_ != null)
                {
                    Func<ItemId, string> returnRealBallName = a =>
                    {
                        switch (a)
                        {
                            case ItemId.ItemPokeBall:
                                return "Poke";
                            case  ItemId.ItemGreatBall:
                                return "Great";
                            case ItemId.ItemUltraBall:
                                return "Ultra";
                            case ItemId.ItemMasterBall:
                                return "Master";
                            default:
                                return "Unknown";
                        }
                    };
                    var catchStatus = attemptCounter > 1
                        ? $"{caughtPokemonResponse.Status} Attempt #{attemptCounter}"
                        : $"{caughtPokemonResponse.Status}";

                    string receivedXP = catchStatus == "CatchSuccess" 
                        ? $"and received XP {caughtPokemonResponse.CaptureAward.Xp.Sum()}" 
                        : $"";

                    Logger.Write($"({catchStatus}) | {encounter?.WildPokemon?.PokemonData.GetMinStats()} | Chance: {(float)((int)(encounter?.CaptureProbability?.CaptureProbability_.First() * 100)) / 100} | {Math.Round(distance)}m dist | with a {returnRealBallName(bestPokeball)}Ball {receivedXP}", LogLevel.Pokemon);
                }

                attemptCounter++;
                await RandomHelper.RandomDelay(750, 1250);
            }
            while (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchMissed || caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchEscape);
        }

        private async Task ExecuteCatchAllNearbyPokemons()
        {
            var mapObjects = await _client.Map.GetMapObjects();

            var pokemons =
                mapObjects.MapCells.SelectMany(i => i.CatchablePokemons)
                .OrderBy(
                    i =>
                    LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, i.Latitude, i.Longitude));

            if (PokeRoadieSettings.Current.UsePokemonToNotCatchList)
            {
                ICollection<PokemonId> filter = PokeRoadieSettings.Current.PokemonsNotToCatch;
                pokemons = mapObjects.MapCells.SelectMany(i => i.CatchablePokemons).Where(p => !filter.Contains(p.PokemonId)).OrderBy(i => LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, i.Latitude, i.Longitude));
            }

            if (pokemons != null && pokemons.Any())
                Logger.Write($"Found {pokemons.Count()} catchable Pokemon", LogLevel.Info);
            else
                return;

            foreach (var pokemon in pokemons)
            {
                var distance = LocationUtils.CalculateDistanceInMeters(_client.CurrentLatitude, _client.CurrentLongitude, pokemon.Latitude, pokemon.Longitude);

                await RandomHelper.RandomDelay(220, 320);

                var encounter = await _client.Encounter.EncounterPokemon(pokemon.EncounterId, pokemon.SpawnPointId);

                if (encounter.Status == EncounterResponse.Types.Status.EncounterSuccess)
                    await CatchEncounter(encounter, pokemon);
                else
                    Logger.Write($"Encounter problem: {encounter.Status}", LogLevel.Warning);
                if (!Equals(pokemons.ElementAtOrDefault(pokemons.Count() - 1), pokemon))
                    // If pokemon is not last pokemon in list, create delay between catches, else keep moving.
                    await RandomHelper.RandomDelay(220, 320);
            }

            if (PokeRoadieSettings.Current.EvolvePokemon || PokeRoadieSettings.Current.EvolveOnlyPokemonAboveIV) await EvolvePokemon(PokeRoadieSettings.Current.PokemonsToEvolve);
            if (PokeRoadieSettings.Current.TransferPokemon) await TransferPokemon();
        }

        private async Task EvolvePokemon(IEnumerable<PokemonId> filter = null)
        {
            await PokeRoadieInventory.getCachedInventory(_client, true);
            var pokemonToEvolve = await _inventory.GetPokemonToEvolve(filter);
            if (pokemonToEvolve != null && pokemonToEvolve.Any())
            {
                Logger.Write($"Found {pokemonToEvolve.Count()} Pokemon for Evolve:", LogLevel.Info);
                if (PokeRoadieSettings.Current.UseLuckyEggs)
                    await UseLuckyEgg();
            }

            foreach (var pokemon in pokemonToEvolve)
            {
                var evolvePokemonOutProto = await _client.Inventory.EvolvePokemon((ulong)pokemon.Id);

                Logger.Write(
                    evolvePokemonOutProto.Result == EvolvePokemonResponse.Types.Result.Success
                        ? $"{pokemon.PokemonId} successfully for {evolvePokemonOutProto.ExperienceAwarded} xp"
                        : $"Failed: {pokemon.PokemonId}. EvolvePokemonOutProto.Result was {evolvePokemonOutProto.Result}, stopping evolving {pokemon.PokemonId}"
                    , LogLevel.Evolve);

                await RandomHelper.RandomDelay(220, 320);
            }
        }

        private async Task TransferPokemon()
        {
            await PokeRoadieInventory.getCachedInventory(_client, true);
            var duplicatePokemons = await _inventory.GetPokemonToTransfer();
            if (duplicatePokemons != null && duplicatePokemons.Any())
            {
                Logger.Write($"Found {duplicatePokemons.Count()} pokemon to transfer...", LogLevel.Info);
               foreach (var duplicatePokemon in duplicatePokemons)
                {
                    await _client.Inventory.TransferPokemon(duplicatePokemon.Id);

                    await PokeRoadieInventory.getCachedInventory(_client, true);
                    var myPokemonSettings = await _inventory.GetPokemonSettings();
                    var pokemonSettings = myPokemonSettings.ToList();
                    var myPokemonFamilies = await _inventory.GetPokemonFamilies();
                    var pokemonFamilies = myPokemonFamilies.ToArray();
                    var settings = pokemonSettings.Single(x => x.PokemonId == duplicatePokemon.PokemonId);
                    var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);
                    var FamilyCandies = $"{familyCandy.Candy_}";

                    _stats.IncreasePokemonsTransfered();
                    _stats.UpdateConsoleTitle(_client, _inventory);

                    PokemonData bestPokemonOfType = null;
                    switch(PokeRoadieSettings.Current.PriorityType)
                    {
                        case PriorityTypes.CP:
                            bestPokemonOfType = await _inventory.GetHighestPokemonOfTypeByCP(duplicatePokemon);
                            break;
                        case PriorityTypes.IV:
                            bestPokemonOfType = await _inventory.GetHighestPokemonOfTypeByIV(duplicatePokemon);
                            break;
                        default:
                            bestPokemonOfType = await _inventory.GetHighestPokemonOfTypeByV(duplicatePokemon);
                            break;
                    }

                    string bestPokemonInfo = "NONE";
                   if (bestPokemonOfType != null)
                        bestPokemonInfo = $"CP: {bestPokemonOfType.Cp}/{PokemonInfo.CalculateMaxCP(bestPokemonOfType)} | IV: {PokemonInfo.CalculatePokemonPerfection(bestPokemonOfType).ToString("0.00")}% perfect";
                    Logger.Write($"{duplicatePokemon.PokemonId} [CP {duplicatePokemon.Cp}/{PokemonInfo.CalculateMaxCP(duplicatePokemon)} | IV: { PokemonInfo.CalculatePokemonPerfection(duplicatePokemon).ToString("0.00")}% perfect] | Best: [{bestPokemonInfo}] | Family Candies: {FamilyCandies}", LogLevel.Transfer);
                }
            }

        }

        private async Task RecycleItems()
        {
            await PokeRoadieInventory.getCachedInventory(_client, true);
            var items = await _inventory.GetItemsToRecycle(PokeRoadieSettings.Current);
            if (items != null && items.Any())
                Logger.Write($"Found {items.Count()} Recyclable {(items.Count() == 1 ? "Item" : "Items")}:", LogLevel.Info);

            foreach (var item in items)
            {
                await _client.Inventory.RecycleItem(item.ItemId, item.Count);
                Logger.Write($"{(item.ItemId).ToString().Replace("Item", "")} x {item.Count}", LogLevel.Recycling);

                _stats.AddItemsRemoved(item.Count);
                _stats.UpdateConsoleTitle(_client, _inventory);

                //await RandomHelper.RandomDelay(220, 320);
            }
            recycleCounter = 0;
        }

        private async Task<ItemId> GetBestBall(EncounterResponse encounter)
        {
            var pokemonCp = encounter?.WildPokemon?.PokemonData?.Cp;
            var iV = Math.Round(PokemonInfo.CalculatePokemonPerfection(encounter?.WildPokemon?.PokemonData));
            var proba = encounter?.CaptureProbability?.CaptureProbability_.First();

            var items = await _inventory.GetItems();
            var pokeBalls = items.Where(x => x.ItemId == ItemId.ItemPokeBall).FirstOrDefault();
            var greatBalls = items.Where(x => x.ItemId == ItemId.ItemGreatBall).FirstOrDefault();
            var ultraBalls = items.Where(x => x.ItemId == ItemId.ItemUltraBall).FirstOrDefault();
            var masterBalls = items.Where(x => x.ItemId == ItemId.ItemMasterBall).FirstOrDefault();

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
                if (ultraBalls != null && masterBalls.Count * 3 < ultraBalls.Count)
                    return ItemId.ItemUltraBall;
                //return the default
                return ItemId.ItemMasterBall;
            }
            if (ultraBalls != null && (pokemonCp >= 1000 || (iV >= PokeRoadieSettings.Current.KeepAboveIV && proba < 0.40)))
            {
                //substitute when low (Upgrade)
                if (masterBalls != null && ultraBalls.Count * 3 < masterBalls.Count)
                    return ItemId.ItemMasterBall;
                //substitute when low (Downgrade)
                if (greatBalls != null && ultraBalls.Count * 3 < greatBalls.Count)
                    return ItemId.ItemGreatBall;
                //return the default
                return ItemId.ItemUltraBall;
            }
            if (greatBalls != null && (pokemonCp >= 300 || (iV >= PokeRoadieSettings.Current.KeepAboveIV && proba < 0.50)))
            {
                //substitute when low (Upgrade)
                if (ultraBalls != null && greatBalls.Count * 3 < ultraBalls.Count)
                    return ItemId.ItemUltraBall;
                //substitute when low (Downgrade)
                if (pokeBalls != null && greatBalls.Count * 3 < pokeBalls.Count)
                    return ItemId.ItemPokeBall;
                //return the default
                return ItemId.ItemGreatBall;
            }
            if (pokeBalls != null)
            {
                //substitute when low (Upgrade)
                if (greatBalls != null && pokeBalls.Count * 3 < greatBalls.Count)
                    return ItemId.ItemGreatBall;
                //return the default
                return ItemId.ItemPokeBall;
            }
            //default to highest possible
            if (masterBalls != null) return ItemId.ItemMasterBall;
            if (ultraBalls != null) return ItemId.ItemUltraBall;
            if (greatBalls != null) return ItemId.ItemGreatBall;
            if (pokeBalls != null) return ItemId.ItemPokeBall;

            return ItemId.ItemUnknown;
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

            if (nanabBerryCount > 0 && (pokemonCp >= 1000 || (iV >= PokeRoadieSettings.Current.KeepAboveIV && proba < 0.40)))
                return ItemId.ItemNanabBerry;

            if (blukBerryCount > 0 && (pokemonCp >= 500 || (iV >= PokeRoadieSettings.Current.KeepAboveIV && proba < 0.50)))
                return ItemId.ItemBlukBerry;

            if (razzBerryCount > 0 && pokemonCp >= 150)
                return ItemId.ItemRazzBerry;

            return ItemId.ItemUnknown;
            //return berries.OrderBy(g => g.Key).First().Key;
        }

        private async Task DisplayHighests()
        {

            //write transfer settings
            if (!hasDisplayedTransferSettings)
            {
                hasDisplayedTransferSettings = true;
                Logger.Write("====== Transfer Settings ======", LogLevel.None, ConsoleColor.Yellow);
                Logger.Write($"{("Keep Above CP:").PadRight(25)}{PokeRoadieSettings.Current.KeepAboveCP}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"{("Keep Above IV:").PadRight(25)}{PokeRoadieSettings.Current.KeepAboveIV}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"{("Keep Above V:").PadRight(25)}{PokeRoadieSettings.Current.KeepAboveV}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"{("Transfer Below CP:").PadRight(25)}{PokeRoadieSettings.Current.TransferBelowCP}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"{("Transfer Below IV:").PadRight(25)}{PokeRoadieSettings.Current.TransferBelowIV}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"{("Transfer Below V:").PadRight(25)}{PokeRoadieSettings.Current.TransferBelowV}", LogLevel.None, ConsoleColor.White);
                Logger.Write($"{("Transfer Evolvable:").PadRight(25)}{!PokeRoadieSettings.Current.NotTransferPokemonsThatCanEvolve}", LogLevel.None, ConsoleColor.White);
                if (PokeRoadieSettings.Current.PokemonsNotToTransfer.Count > 0)
                {
                    Logger.Write($"{("PokemonsNotToTransfer:").PadRight(25)} {PokeRoadieSettings.Current.PokemonsNotToTransfer.Count}", LogLevel.None, ConsoleColor.White);
                    foreach (PokemonId i in PokeRoadieSettings.Current.PokemonsNotToTransfer)
                    {
                        Logger.Write(i.ToString(), LogLevel.None, ConsoleColor.White);
                    }
                }
            }
 
            //get all ordered by id, then cp
            var allPokemon = (await _inventory.GetPokemons()).OrderBy(x => x.PokemonId).ThenByDescending(x => x.Cp).ToList();

            if (PokeRoadieSettings.Current.DestinationsEnabled && PokeRoadieSettings.Current.Destinations != null && PokeRoadieSettings.Current.Destinations.Count > 0)
            {
                Logger.Write("====== Destinations ======", LogLevel.None, ConsoleColor.Yellow);
                DestinationData lastDestination = null;
                for (int i = 0; i < PokeRoadieSettings.Current.Destinations.Count; i++)
                {
                    var destination = PokeRoadieSettings.Current.Destinations[i];
                    var str = $"{i} - {destination.Name} - {destination.Latitude}:{destination.Longitude}:{destination.Altitude}";
                    if (PokeRoadieSettings.Current.DestinationIndex < i)
                    {
                        if (lastDestination != null)
                        {

                            var sourceLocation = new GeoCoordinate(lastDestination.Latitude, lastDestination.Longitude, lastDestination.Altitude);
                            var targetLocation = new GeoCoordinate(destination.Latitude, destination.Longitude, destination.Altitude);
                            var distanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);
                            var speed = PokeRoadieSettings.Current.FlyingEnabled ? PokeRoadieSettings.Current.FlyingSpeed : PokeRoadieSettings.Current.MaxSpeed;
                            var speedInMetersPerSecond = speed / 3.6;
                            var seconds = distanceToTarget / speedInMetersPerSecond;
                            var action = PokeRoadieSettings.Current.FlyingEnabled ? "flying" : "driving";
                            str += " (";
                            str += StringUtils.GetSecondsDisplay(seconds);
                            str += $" {action} at {speed}kmh)";

                        }
                    }
                    else if (PokeRoadieSettings.Current.DestinationIndex == i)
                    {
                        str += " <-- You Are Here!";
                    }
                    else
                    {
                        str += " (Visited)";
                    }
                    Logger.Write(str, LogLevel.None, PokeRoadieSettings.Current.DestinationIndex == i ? ConsoleColor.Red : PokeRoadieSettings.Current.DestinationIndex < i ? ConsoleColor.White : ConsoleColor.DarkGray);
                    lastDestination = destination;
                }
            }
            Logger.Write("====== Most Valuable ======", LogLevel.None, ConsoleColor.Yellow);
            var highestsPokemonV = await _inventory.GetHighestsV(20);
            foreach (var pokemon in highestsPokemonV)
                Logger.Write(pokemon.GetStats(), LogLevel.None, ConsoleColor.White);
            Logger.Write("====== Highest CP ======", LogLevel.None, ConsoleColor.Yellow);
            var highestsPokemonCp = await _inventory.GetHighestsCP(20);
            foreach (var pokemon in highestsPokemonCp)
                Logger.Write(pokemon.GetStats(), LogLevel.None, ConsoleColor.White);
            Logger.Write("====== Most Perfect Genetics ======", LogLevel.None, ConsoleColor.Yellow);
            var highestsPokemonPerfect = await _inventory.GetHighestsPerfect(20);
            foreach (var pokemon in highestsPokemonPerfect)
            {
                Logger.Write(pokemon.GetStats(), LogLevel.None, ConsoleColor.White);
            }
            if (PokeRoadieSettings.Current.DisplayAllPokemonInLog)
            {
                Logger.Write("====== Full List ======", LogLevel.None, ConsoleColor.Yellow);
                foreach (var pokemon in allPokemon.OrderBy(x => x.PokemonId).ThenByDescending(x => x.Cp))
                {
                    Logger.Write(pokemon.GetStats(), LogLevel.None, ConsoleColor.White);
                }
            }
            if (PokeRoadieSettings.Current.DisplayAggregateLog)
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
            }

        }

        /*
        private async Task LoadAndDisplayGpxFile()
        {
            var xmlString = File.ReadAllText(PokeRoadieSettings.Current.GPXFile);
            var readgpx = new GpxReader(xmlString);
            foreach (var trk in readgpx.Tracks)
            {
                foreach (var trkseg in trk.Segments)
                {
                    foreach (var trpkt in trkseg.TrackPoints)
                    {
                        Console.WriteLine(trpkt.ToString());
                    }
                }
            }
            await Task.Delay(0);
        }
        */

        /*
        private GPXReader.trk GetGPXTrack(string gpxFile)
        {
            string xmlString = File.ReadAllText(PokeRoadieSettings.Current.GPXFile);
            GPXReader Readgpx = new GPXReader(xmlString);
            return Readgpx.Tracks.ElementAt(0);
        }
        */

        private List<GpxReader.Trk> GetGpxTracks()
        {
            var xmlString = File.ReadAllText(PokeRoadieSettings.Current.GPXFile);
            var readgpx = new GpxReader(xmlString);
            return readgpx.Tracks;
        }

        public async Task UseLuckyEgg()
        {
            var inventory = await _inventory.GetItems();
            var LuckyEgg = inventory.Where(p => p.ItemId == ItemId.ItemLuckyEgg).FirstOrDefault();

            if (LuckyEgg == null || LuckyEgg.Count <= 0)
                return;

            await _client.Inventory.UseItemXpBoost();
            Logger.Write($"Used Lucky Egg, remaining: {LuckyEgg.Count - 1}", LogLevel.Egg);
        }

        ///// <summary>
        ///// Resets coords if someone could realistically get back to the default coords points since they were last updated (program was last run)
        ///// </summary>
        //private void ResetCoords(string filename = "LastCoords.ini")
        //{

        //    var lat = PokeRoadieSettings.Current.CurrentLatitude;
        //    var lng = PokeRoadieSettings.Current.CurrentLongitude;
        //    var alt = PokeRoadieSettings.Current.CurrentAltitude;

        //    if (_client != null)
        //    {
        //        if (_client.StartLat > 0 && _client.StartLng > 0)
        //        {
        //            lat = _client.StartLat;
        //            lng = _client.StartLng;
        //            alt = _client.StartAltitude;
        //        }
        //        else
        //        {
        //            _client.StartLat = PokeRoadieSettings.Current.CurrentLatitude;
        //            _client.StartLng = PokeRoadieSettings.Current.CurrentLongitude;
        //            _client.StartAltitude = PokeRoadieSettings.Current.CurrentAltitude;
        //        }
        //    }
        //    double distance = LocationUtils.CalculateDistanceInMeters(latLngFromFile.Item1, latLngFromFile.Item2, lat, lng);
        //    DateTime? lastModified = File.Exists(lastcoords_file) ? (DateTime?)File.GetLastWriteTime(lastcoords_file) : null;
        //    if (lastModified == null) return;
        //    double? hoursSinceModified = (DateTime.Now - lastModified).HasValue ? (double?)((DateTime.Now - lastModified).Value.Minutes / 60.0) : null;
        //    if (hoursSinceModified == null || hoursSinceModified < 1) return; // Shouldn't really be null, but can be 0 and that's bad for division.
        //    var kmph = (distance / 1000) / (hoursSinceModified ?? .1);
        //    if (kmph < 80) // If speed required to get to the default location is < 80km/hr
        //    {
        //        File.Delete(lastcoords_file);
        //        Logger.Write("Detected realistic Traveling , using UserSettings.settings", LogLevel.Warning);
        //    }
        //    else
        //    {
        //        Logger.Write("Not realistic Traveling at " + kmph + ", using last saved Coords.ini", LogLevel.Warning);
        //    }
        //}
    }
}
 