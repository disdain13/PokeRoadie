#region " Imports "

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using PokemonGo.RocketAPI.Logging;
using PokemonGo.RocketAPI;
using PokemonGo.RocketAPI.Exceptions;

using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Responses;
using POGOProtos.Data;
using POGOProtos.Data.Player;
using POGOProtos.Enums;
using POGOProtos.Settings.Master;
using PokemonGo.RocketAPI.Extensions;
using POGOProtos.Map.Fort;
using PokeRoadie.Extensions;

#endregion


namespace PokeRoadie
{
    public class PokeRoadieInventory
    {

        #region " Members "

        private readonly PokeRoadieClient _client;
        private readonly PokeRoadieSettings _settings;
        public static DateTime _lastRefresh;
        public static GetInventoryResponse _cachedInventory;
        private string export_path = Path.Combine(Directory.GetCurrentDirectory(), "Export");
        private Random Random = new Random(DateTime.Now.Millisecond);

        #endregion
        #region " Properties "

        public static bool IsDirty { get; set; }

        #endregion
        #region " Constructors "

        public PokeRoadieInventory(PokeRoadieClient client, PokeRoadieSettings settings)
        {
            _client = client;
            _settings = settings;
        }

        #endregion
        #region " Pokemon Methods "

        public async Task<IEnumerable<PokemonData>> GetPokemons()
        {
            var inventory = await GetCachedInventory(_client);
            if (inventory == null || inventory.InventoryDelta == null) return new List<PokemonData>();
            return inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.PokemonData).Where(p => p != null && p.PokemonId > 0);
        }

        public async Task<IEnumerable<PokemonData>> GetPokemonToTransfer()
        {
            //Not deployed or favorited filter
            var getTask = await GetPokemons();
            var query = getTask.Where(p =>
                 string.IsNullOrWhiteSpace(p.DeployedFortId) &&
                 p.Favorite == 0
            );

            //Build Transfer Below List. These will always transfer, and overrides
            //the Keep list.
            var results1 = query.Where(x =>
                (PokeRoadieSettings.Current.TransferBelowCp > 0 && x.Cp < PokeRoadieSettings.Current.TransferBelowCp) ||
                (PokeRoadieSettings.Current.TransferBelowIV > 0 && x.GetPerfection() < PokeRoadieSettings.Current.TransferBelowIV) ||
                (PokeRoadieSettings.Current.TransferBelowV > 0 && x.CalculatePokemonValue() < PokeRoadieSettings.Current.TransferBelowV)
            );


            //Keep By CP filter
            if (PokeRoadieSettings.Current.KeepAboveCp > 0)
                query = query.Where(p => p.Cp < PokeRoadieSettings.Current.KeepAboveCp);

            //Keep By IV filter
            if (PokeRoadieSettings.Current.KeepAboveIV > 0)
                query = query.Where(p => p.GetPerfection() < PokeRoadieSettings.Current.KeepAboveIV);

            //Keep By V filter
            if (PokeRoadieSettings.Current.KeepAboveV > 0)
                query = query.Where(p => p.CalculatePokemonValue() < PokeRoadieSettings.Current.KeepAboveV);

            //Not to transfer list filter
            if (PokeRoadieSettings.Current.PokemonsNotToTransfer != null)
                query = query.Where(p => !PokeRoadieSettings.Current.PokemonsNotToTransfer.Contains(p.PokemonId));

            //Not transfer if they can evolve
            if (PokeRoadieSettings.Current.NotTransferPokemonsThatCanEvolve)
            {
                var results = new List<PokemonData>();
                var pokemonsThatCanBeTransfered = query.GroupBy(p => p.PokemonId)
                    .Where(x => x.Count() > 2).ToList();

                var myPokemonSettings = await GetPokemonSettings();
                var pokemonSettings = myPokemonSettings.ToList();

                var myPokemonFamilies = await GetPokemonFamilies();
                var pokemonFamilies = myPokemonFamilies.ToArray();

                foreach (var pokemon in pokemonsThatCanBeTransfered)
                {
                    var settings = pokemonSettings.Single(x => x.PokemonId == pokemon.Key);
                    var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);
                    var amountToSkip = PokeRoadieSettings.Current.KeepDuplicateAmount;

                    if (settings.CandyToEvolve > 0 && familyCandy.Candy_ / settings.CandyToEvolve > amountToSkip)
                        amountToSkip = familyCandy.Candy_ / settings.CandyToEvolve;

                    switch (PokeRoadieSettings.Current.TransferPriorityType)
                    {
                        case PriorityTypes.CP:
                            results.AddRange(query.Where(x => x.PokemonId == pokemon.Key)
                                .OrderByDescending(x => x.Cp)
                                .ThenBy(n => n.StaminaMax)
                                .Skip(amountToSkip)
                                .ToList());
                            break;
                        case PriorityTypes.IV:
                            results.AddRange(query.Where(x => x.PokemonId == pokemon.Key)
                                .OrderByDescending(PokemonInfo.CalculatePokemonPerfection)
                                .ThenBy(n => n.StaminaMax)
                                .Skip(amountToSkip)
                                .ToList());
                            break;
                        case PriorityTypes.V:
                            results.AddRange(query.Where(x => x.PokemonId == pokemon.Key)
                                .OrderByDescending(x => x.CalculatePokemonValue())
                                .ThenBy(n => n.StaminaMax)
                                .Skip(amountToSkip)
                                .ToList());
                            break;
                    }
                }
                return results;
            }

            List<PokemonData> results2 = null;
            switch (PokeRoadieSettings.Current.TransferPriorityType)
            {
                case PriorityTypes.CP:

                    results2 = query
                    .GroupBy(p => p.PokemonId)
                    .Where(x => x.Count() > 1)
                    .SelectMany(p =>

                                p.OrderByDescending(x => x.Cp)
                                .ThenBy(n => n.StaminaMax)
                                .Skip(PokeRoadieSettings.Current.KeepDuplicateAmount)
                                .ToList()

                                ).ToList();
                    break;
                case PriorityTypes.IV:

                    results2 = query
                    .GroupBy(p => p.PokemonId)
                    .Where(x => x.Count() > 1)
                    .SelectMany(p =>

                                p.OrderByDescending(PokemonInfo.CalculatePokemonPerfection)
                                .ThenBy(n => n.StaminaMax)
                                .Skip(PokeRoadieSettings.Current.KeepDuplicateAmount)
                                .ToList()

                                ).ToList();
                    break;
                default:

                    results2 = query
                    .GroupBy(p => p.PokemonId)
                    .Where(x => x.Count() > 1)
                    .SelectMany(p =>

                                p.OrderByDescending(x => x.CalculatePokemonValue())
                                .ThenBy(n => n.StaminaMax)
                                .Skip(PokeRoadieSettings.Current.KeepDuplicateAmount)
                                .ToList()

                                ).ToList();
                    break;
            }

            //merge together the two lists
            foreach (var result in results1)
            {
                if (!results2.Contains(result))
                {
                    results2.Add(result);
                }
            }

            return results2;
        }

        public async Task<IEnumerable<PokemonData>> GetHighestsV(int limit)
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.OrderByDescending(x => x.CalculatePokemonValue()).ThenBy(n => n.StaminaMax).Take(limit);
        }

        public async Task<IEnumerable<PokemonData>> GetPokemonToHeal()
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.Where(x => string.IsNullOrWhiteSpace(x.DeployedFortId) && x.Stamina > 0 && x.Stamina < x.StaminaMax).OrderByDescending(n => n.CalculatePokemonValue()).ThenBy(n => n.Stamina);
        }

        public async Task<IEnumerable<PokemonData>> GetPokemonToRevive()
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.Where(x => string.IsNullOrWhiteSpace(x.DeployedFortId) && x.Stamina == 0).OrderByDescending(n => n.CalculatePokemonValue());
        }

        public async Task<IEnumerable<PokemonData>> GetHighestsVNotDeployed(int limit)
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.Where(x => string.IsNullOrWhiteSpace(x.DeployedFortId) && x.Stamina == x.StaminaMax).OrderByDescending(x => x.CalculatePokemonValue()).ThenBy(n => n.StaminaMax).Take(limit);
        }

        public async Task<IEnumerable<PokemonData>> GetHighestsCP(int limit)
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.OrderByDescending(x => x.Cp).ThenBy(n => n.StaminaMax).Take(limit);
        }

        public async Task<IEnumerable<PokemonData>> GetHighestsPerfect(int limit = 1000)
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.OrderByDescending(PokemonInfo.CalculatePokemonPerfection).Take(limit);
        }

        public async Task<PokemonData> GetHighestPokemonOfTypeByCP(PokemonData pokemon)
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.Where(x => x.PokemonId == pokemon.PokemonId)
                .OrderByDescending(x => x.Cp)
                .FirstOrDefault();
        }

        public async Task<PokemonData> GetHighestPokemonOfTypeByIV(PokemonData pokemon)
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.Where(x => x.PokemonId == pokemon.PokemonId)
                .OrderByDescending(PokemonInfo.CalculatePokemonPerfection)
                .FirstOrDefault();
        }

        public async Task<PokemonData> GetHighestPokemonOfTypeByV(PokemonData pokemon)
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.Where(x => x.PokemonId == pokemon.PokemonId)
                .OrderByDescending(x => x.CalculatePokemonValue())
                .FirstOrDefault();
        }
        
        //GetHighestsCandies
        public async Task<IEnumerable<Candy>> GetHighestsCandies(int limit)
        {   
            //var myPokemon = await GetPokemons();
            //var pokemons = myPokemon.ToList();

            //var myPokemonSettings = await GetPokemonSettings();
            //var pokemonSettings = myPokemonSettings.ToList();
            var myPokemonFamilies = await GetPokemonFamilies();
            //var pokemonFamilies = myPokemonFamilies.ToArray();
            //var settings = pokemonSettings.Single(x => x.PokemonId == pokemon.PokemonId);
            //var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);
            //var FamilyCandies = familyCandy.Candy_;

            return myPokemonFamilies.OrderByDescending(x => x.Candy_ ).ThenBy(n => n.FamilyId ).Take(limit).ToArray();
        }

        public async Task<IEnumerable<Candy>> GetPokemonFamilies()
        {
            var inventory = await GetCachedInventory(_client);
            return
                inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.Candy)
                    .Where(p => p != null && p.FamilyId != PokemonFamilyId.FamilyUnset);
        }

        public async Task<IEnumerable<PokemonSettings>> GetPokemonSettings()
        {
            var templates = await _client.Download.GetItemTemplates();
            return
                templates.ItemTemplates.Select(i => i.PokemonSettings)
                    .Where(p => p != null && p.FamilyId != PokemonFamilyId.FamilyUnset);
        }

        public async Task<IEnumerable<PokemonData>> GetPokemonToEvolve()
        {
            var query = (await GetPokemons()).Where(p =>
            String.IsNullOrWhiteSpace(p.DeployedFortId));

            //list filter
            if (PokeRoadieSettings.Current.UsePokemonsToEvolveList)
            {
                if (PokeRoadieSettings.Current.PokemonsToEvolve.Count() == 0)
                    return new List<PokemonData>();

                query = query.Where(x => PokeRoadieSettings.Current.PokemonsToEvolve.Contains(x.PokemonId));
                if (query.Count() == 0) return new List<PokemonData>();
            }

            //Evolve By CP filter
            if (PokeRoadieSettings.Current.EvolveAboveCp > 0)
                query = query.Where(p => p.Cp > PokeRoadieSettings.Current.EvolveAboveCp);
            if (query.Count() == 0) return new List<PokemonData>();

            //Evolve By IV filter
            if (PokeRoadieSettings.Current.EvolveAboveIV > 0)
                query = query.Where(p => p.GetPerfection() > PokeRoadieSettings.Current.EvolveAboveIV);
            if (query.Count() == 0) return new List<PokemonData>();

            //Evolve By V filter
            if (PokeRoadieSettings.Current.EvolveAboveV > 0)
                query = query.Where(p => p.CalculatePokemonValue() > PokeRoadieSettings.Current.EvolveAboveV);
            if (query.Count() == 0) return new List<PokemonData>();

            //ordering
            switch (PokeRoadieSettings.Current.EvolvePriorityType)
            {
                case PriorityTypes.CP:
                    query = query.OrderByDescending(x => x.Cp)
                                 .ThenBy(x => x.Stamina);
                    break;
                case PriorityTypes.IV:
                    query = query.OrderByDescending(PokemonInfo.CalculatePokemonPerfection)
                                 .ThenBy(n => n.StaminaMax);
                    break;
                default:
                    query = query.OrderByDescending(x => x.CalculatePokemonValue())
                                 .ThenBy(n => n.StaminaMax);
                    break;
            }

            var pokemons = query.ToList();

            var myPokemonSettings = await GetPokemonSettings();
            var pokemonSettings = myPokemonSettings.ToList();

            var myPokemonFamilies = await GetPokemonFamilies();
            var pokemonFamilies = myPokemonFamilies.ToArray();

            var pokemonToEvolve = new List<PokemonData>();
            foreach (var pokemon in pokemons)
            {
                var settings = pokemonSettings.Single(x => x.PokemonId == pokemon.PokemonId);
                var familyCandy = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId);

                //Don't evolve if we can't evolve it
                if (settings.EvolutionIds.Count == 0)
                    continue;

                var pokemonCandyNeededAlready =
                    pokemonToEvolve.Count(
                        p => pokemonSettings.Single(x => x.PokemonId == p.PokemonId).FamilyId == settings.FamilyId) *
                    settings.CandyToEvolve;
                if (familyCandy.Candy_ - pokemonCandyNeededAlready > settings.CandyToEvolve)
                    pokemonToEvolve.Add(pokemon);
            }

            return pokemonToEvolve;
        }

        public async Task<List<PokemonData>> GetPokemonToPowerUp()
        {
            var query = (await GetPokemons()).Where(p =>
                  String.IsNullOrWhiteSpace(p.DeployedFortId));

            //list filter
            if (PokeRoadieSettings.Current.UsePokemonsToPowerUpList)
            {
                if (PokeRoadieSettings.Current.PokemonsToPowerUp.Count() == 0)
                    return new List<PokemonData>();

                query = query.Where(x => PokeRoadieSettings.Current.PokemonsToPowerUp.Contains(x.PokemonId));
                if (query.Count() == 0) return new List<PokemonData>();
            }

            //PowerUp By CP filter
            if (PokeRoadieSettings.Current.PowerUpAboveCp > 0)
                query = query.Where(p => p.Cp > PokeRoadieSettings.Current.PowerUpAboveCp);
            if (query.Count() == 0) return new List<PokemonData>();

            //PowerUp By IV filter
            if (PokeRoadieSettings.Current.PowerUpAboveIV > 0)
                query = query.Where(p => p.GetPerfection() > PokeRoadieSettings.Current.PowerUpAboveIV);
            if (query.Count() == 0) return new List<PokemonData>();

            //PowerUp By V filter
            if (PokeRoadieSettings.Current.PowerUpAboveV > 0)
                query = query.Where(p => p.CalculatePokemonValue() > PokeRoadieSettings.Current.PowerUpAboveV);
            if (query.Count() == 0) return new List<PokemonData>();

            //ordering
            switch (PokeRoadieSettings.Current.PowerUpPriorityType)
            {
                case PriorityTypes.CP:
                    query = query.OrderByDescending(x => x.Cp)
                                 .ThenBy(x => x.Stamina);
                    break;
                case PriorityTypes.IV:
                    query = query.OrderByDescending(PokemonInfo.CalculatePokemonPerfection)
                                 .ThenBy(n => n.StaminaMax);
                    break;
                default:
                    query = query.OrderByDescending(x => x.CalculatePokemonValue())
                                 .ThenBy(n => n.StaminaMax);
                    break;
            }

            return query.ToList();

        }

        public async Task<List<PokemonData>> GetPokemonToFavorite()
        {
            var query = (await GetPokemons()).Where(p =>
                  String.IsNullOrWhiteSpace(p.DeployedFortId) && p.Favorite == 0);

            //Favorite By CP filter
            if (PokeRoadieSettings.Current.FavoriteAboveCp > 0)
                query = query.Where(p => p.Cp > PokeRoadieSettings.Current.FavoriteAboveCp);
            if (query.Count() == 0) return new List<PokemonData>();

            //Favorite By IV filter
            if (PokeRoadieSettings.Current.FavoriteAboveIV > 0)
                query = query.Where(p => p.GetPerfection() > PokeRoadieSettings.Current.FavoriteAboveIV);
            if (query.Count() == 0) return new List<PokemonData>();

            //Favorite By V filter
            if (PokeRoadieSettings.Current.FavoriteAboveV > 0)
                query = query.Where(p => p.CalculatePokemonValue() > PokeRoadieSettings.Current.FavoriteAboveV);
            if (query.Count() == 0) return new List<PokemonData>();

            return query.ToList();

        }

        #endregion
        #region " Inventory Methods "

        public static async Task<GetInventoryResponse> GetCachedInventory(PokeRoadieClient _client, bool request = false)
        {
            var now = DateTime.UtcNow;
            var ss = new SemaphoreSlim(10);

            if (!IsDirty && _lastRefresh > now && request == false)
            {
                return _cachedInventory;
            }
            await ss.WaitAsync();
            try
            {
                _lastRefresh = now.AddSeconds(30);
                //_cachedInventory = await _client.GetInventory();

                try
                {
                    _cachedInventory = await _client.Inventory.GetInventory();
                    IsDirty = false;
                }
                catch
                {
                    // ignored
                }

                return _cachedInventory;
            }
            finally
            {
                ss.Release();
            }
        }

        public async Task<int> GetItemAmountByType(ItemId type)
        {
            var pokeballs = await GetItems();
            return pokeballs.FirstOrDefault(i => i.ItemId == type)?.Count ?? 0;
        }

        public async Task<IEnumerable<ItemData>> GetItems()
        {
            var inventory = await GetCachedInventory(_client);
            return inventory.InventoryDelta.InventoryItems
                .Select(i => i.InventoryItemData?.Item)
                .Where(p => p != null);
        }

        public async Task<IEnumerable<ItemData>> GetItemsToRecycle(ISettings settings)
        {
            var myItems = await GetItems();

            return myItems
                .Where(x => PokeRoadieSettings.Current.ItemRecycleFilter.Any(f => f.Key == (ItemId)x.ItemId && x.Count > f.Value))
                .Select(
                    x =>
                        new ItemData
                        {
                            ItemId = x.ItemId,
                            Count = x.Count - PokeRoadieSettings.Current.ItemRecycleFilter.Single(f => f.Key == x.ItemId).Value,
                            Unseen = x.Unseen
                        });
        }

        public async Task<IEnumerable<EggIncubator>> GetEggIncubators()
        {
            var inventory = await GetCachedInventory(_client);
            return
                inventory.InventoryDelta.InventoryItems
                    .Where(x => x.InventoryItemData.EggIncubators != null)
                    .SelectMany(i => i.InventoryItemData.EggIncubators.EggIncubator)
                    .Where(i => i != null);
        }

        public async Task<IEnumerable<PokemonData>> GetEggs()
        {
            var inventory = await GetCachedInventory(_client);
            return
                inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.PokemonData)
                    .Where(p => p != null && p.IsEgg);
        }

        #endregion
        #region " Player/Trainer Methods "

        public async Task<IEnumerable<PlayerStats>> GetPlayerStats()
        {
            var inventory = await GetCachedInventory(_client);
            return inventory.InventoryDelta.InventoryItems
                .Select(i => i.InventoryItemData?.PlayerStats)
                .Where(p => p != null);
        }

        public async Task<int> GetStarDust()
        {
            var StarDust = await _client.Player.GetPlayer();
            var gdrfds = StarDust.PlayerData.Currencies;
            var SplitStar = gdrfds[1].Amount;
            return SplitStar;

        }

        public async Task<LevelUpRewardsResponse> GetLevelUpRewards(int level)
        {
            return await _client.Player.GetLevelUpRewards(level);
        }

        public async Task<EncounterTutorialCompleteResponse> TutorialComplete(PokemonId pokemonId)
        {
            return await _client.Encounter.EncounterTutorialComplete(pokemonId);
        }

        public async Task<CollectDailyDefenderBonusResponse> CollectDailyDefenderBonus()
        {
            return await _client.Player.CollectDailyDefenderBonus();
        }

        public async Task<CollectDailyBonusResponse> CollectDailyBonus()
        {
            return await _client.Player.CollectDailyBonus();
        }

        public async Task<SetPlayerTeamResponse> SetPlayerTeam(TeamColor team)
        {
            return await _client.Player.SetPlayerTeam(team);
        }
        public async Task<EncounterTutorialCompleteResponse> TutorialMarkComplete(IEnumerable<TutorialState> tutorialStates)
        {
            return await _client.Misc.MarkTutorialComplete(tutorialStates, false, false);
        }
        public async Task<ClaimCodenameResponse> ClaimCodeName(string codeName)
        {
            return await _client.Misc.ClaimCodename(codeName);
        }
        public async Task<GetSuggestedCodenamesResponse> GetSuggestedCodenames()
        {
            return await _client.Misc.GetSuggestedCodenames();
        }
        public async Task<SetAvatarResponse> SetAvatar(PlayerAvatar avatar)
        {
            return await _client.Player.SetAvatar(avatar);
        }
        public async Task<SetContactSettingsResponse> SetContactSettings(ContactSettings contactSettings)
        {
            return await _client.Player.SetContactSetting(contactSettings);
        }
        public async Task<CheckAwardedBadgesResponse> GetNewlyAwardedBadges()
        {
            return await _client.Player.GetNewlyAwardedBadges();
        }
        #endregion
        #region " Export "

        public async Task ExportPokemonToCSV(PlayerData player, string filename = "PokeList.csv")
        {
            if (player == null)
                return;
            var stats = await GetPlayerStats();
            var stat = stats.FirstOrDefault();
            if (stat == null)
                return;

            if (!Directory.Exists(export_path))
                Directory.CreateDirectory(export_path);
            if (Directory.Exists(export_path))
            {
                try
                {
                    string pokelist_file = Path.Combine(export_path, $"Profile_{player.Username}_{filename}");
                    if (File.Exists(pokelist_file))
                        File.Delete(pokelist_file);
                    string ls = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;
                    string header = "PokemonID,Name,NickName,CP / MaxCP,Perfection,Attack 1,Attack 2,HP,Attk,Def,Stamina,Familie Candies,previewLink";
                    File.WriteAllText(pokelist_file, $"{header.Replace(",", $"{ls}")}");

                    var AllPokemon = await GetHighestsPerfect();
                    var myPokemonSettings = await GetPokemonSettings();
                    var pokemonSettings = myPokemonSettings.ToList();
                    var myPokemonFamilies = await GetPokemonFamilies();
                    var pokemonFamilies = myPokemonFamilies.ToArray();
                    int trainerLevel = stat.Level;
                    int[] exp_req = new[] { 0, 1000, 3000, 6000, 10000, 15000, 21000, 28000, 36000, 45000, 55000, 65000, 75000, 85000, 100000, 120000, 140000, 160000, 185000, 210000, 260000, 335000, 435000, 560000, 710000, 900000, 1100000, 1350000, 1650000, 2000000, 2500000, 3000000, 3750000, 4750000, 6000000, 7500000, 9500000, 12000000, 15000000, 20000000 };
                    int exp_req_at_level = exp_req[stat.Level - 1];

                    using (var w = File.AppendText(pokelist_file))
                    {
                        w.WriteLine("");
                        foreach (var pokemon in AllPokemon)
                        {
                            string toEncode = $"{(int)pokemon.PokemonId}" + "," + trainerLevel + "," + PokemonInfo.GetLevel(pokemon) + "," + pokemon.Cp + "," + pokemon.Stamina;
                            //Generate base64 code to make it viewable here https://jackhumbert.github.io/poke-rater/#MTUwLDIzLDE3LDE5MDIsMTE4
                            var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(toEncode));
                            var settings = pokemonSettings.Single(x => x.PokemonId == pokemon.PokemonId);
                            var familiecandies = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId).Candy_;
                            string perfection = pokemon.GetPerfection().ToString("0.00");
                            perfection = perfection.Replace(",", ls == "," ? "." : ",");
                            string content_part1 = $"{(int)pokemon.PokemonId},{pokemon.PokemonId},{pokemon.Nickname},{pokemon.Cp}/{PokemonInfo.CalculateMaxCP(pokemon)},";
                            string content_part2 = $",{pokemon.Move1},{pokemon.Move2},{pokemon.Stamina},{pokemon.IndividualAttack},{pokemon.IndividualDefense},{pokemon.IndividualStamina},{familiecandies},https://jackhumbert.github.io/poke-rater/#{encoded}";
                            string content = $"{content_part1.Replace(",", $"{ls}")}{perfection}{content_part2.Replace(",", $"{ls}")}";
                            w.WriteLine($"{content}");

                        }
                        w.Close();
                    }
                    //Logger.Write($"Export Player Infos and all Pokemon to \"\\Export\\{filename}\"", LogLevel.Info);
                }
                catch
                {
                    Logger.Write("Export Player Infos and all Pokemons to CSV not possible. File seems be in use!", LogLevel.Warning);
                }
            }
        }

        #endregion

    }
}
