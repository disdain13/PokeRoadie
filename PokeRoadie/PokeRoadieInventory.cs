#region " Imports "

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using PokeRoadie.Api.Logging;
using PokeRoadie.Api;
using PokeRoadie.Api.Exceptions;

using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Responses;
using POGOProtos.Data;
using POGOProtos.Data.Player;
using POGOProtos.Enums;
using POGOProtos.Settings.Master;
using PokeRoadie.Api.Extensions;
using POGOProtos.Map.Fort;
using PokeRoadie.Extensions;

#endregion


namespace PokeRoadie
{
    public class PokeRoadieInventory
    {

        #region " Members "

        public static DateTime _lastRefresh;
        public static GetInventoryResponse _cachedInventory;
        private string export_path = Path.Combine(Directory.GetCurrentDirectory(), "Export");
        private Random Random = new Random(DateTime.Now.Millisecond);

        #endregion
        #region " Properties "

        public Context Context { get; private set; }

        public static bool IsDirty { get; set; }

        #endregion
        #region " Constructors "

        public PokeRoadieInventory(Context context)
        {
            Context = context;
        }

        #endregion
        #region " Pokemon Methods "

        public async Task<IEnumerable<PokemonData>> GetPokemons()
        {
            var inventory = await GetCachedInventory(Context.Client);
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
                !Context.Settings.PokemonsNotToTransfer.Contains(x.PokemonId) &&
                ((Context.Settings.AlwaysTransferBelowCp > 0 && x.Cp < Context.Settings.AlwaysTransferBelowCp) ||
                (Context.Settings.AlwaysTransferBelowIV > 0 && x.GetPerfection() < Context.Settings.AlwaysTransferBelowIV) ||
                (Context.Settings.AlwaysTransferBelowLV > 0 && x.GetLevel() < Context.Settings.AlwaysTransferBelowLV) ||
                (Context.Settings.AlwaysTransferBelowV > 0 && Context.Utility.CalculatePokemonValue(x) < Context.Settings.AlwaysTransferBelowV))
            );


            //Keep By CP filter
            if (Context.Settings.KeepAboveCP > 0)
                query = query.Where(p => p.Cp < Context.Settings.KeepAboveCP);

            //Keep By IV filter
            if (Context.Settings.KeepAboveIV > 0)
                query = query.Where(p => p.GetPerfection() < Context.Settings.KeepAboveIV);

            //Keep By V filter
            if (Context.Settings.KeepAboveV > 0)
                query = query.Where(p => Context.Utility.CalculatePokemonValue(p) < Context.Settings.KeepAboveV);

            //Keep By LV filter
            if (Context.Settings.KeepAboveLV > 0)
                query = query.Where(p => p.GetLevel() < Context.Settings.KeepAboveLV);

            //Not to transfer list filter
            if (Context.Settings.PokemonsNotToTransfer != null)
                query = query.Where(p => !Context.Settings.PokemonsNotToTransfer.Contains(p.PokemonId));


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
                case PriorityTypes.V:
                    thenBy = new Func<PokemonData, double>(x => Context.Utility.CalculatePokemonValue(x));
                    break;
                case PriorityTypes.LV:
                    thenBy = new Func<PokemonData, double>(x => x.GetLevel());
                    break;
                default:
                    break;
            }

            query = orderBy == null ? query : thenBy == null ? query.OrderByDescending(orderBy) : query.OrderByDescending(orderBy).ThenByDescending(thenBy);


            //Not transfer if they can evolve
            if (Context.Settings.NotTransferPokemonsThatCanEvolve)
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
                    var amountToSkip = Context.Settings.KeepDuplicateAmount;

                    if (Context.Settings.NotTransferPokemonsThatCanEvolve && (settings.CandyToEvolve > 0 && familyCandy.Candy_ / settings.CandyToEvolve > amountToSkip))
                        amountToSkip = familyCandy.Candy_ / settings.CandyToEvolve;

                    if (amountToSkip > 0)
                    {
                        results.AddRange(query.Where(x => x.PokemonId == pokemon.Key).Skip(amountToSkip).ToList());
                    }
                    else
                    {
                        results.AddRange(query.Where(x => x.PokemonId == pokemon.Key).ToList());
                    }

                }
                return results;
            }

            List<PokemonData> results2 = 
                
                (Context.Settings.KeepDuplicateAmount < 1) ?
                
                (thenBy == null) ?
                query
                    .GroupBy(p => p.PokemonId)
                    .Where(x => x.Count() > 1)
                    .SelectMany(p => p.OrderByDescending(orderBy)
                    .ToList()).ToList()
                :
                query
                    .GroupBy(p => p.PokemonId)
                    .Where(x => x.Count() > 1)
                    .SelectMany(p => p.OrderByDescending(orderBy)
                    .ThenByDescending(thenBy)
                    .ToList()).ToList()

                :

                (thenBy == null) ?
                query
                    .GroupBy(p => p.PokemonId)
                    .Where(x => x.Count() > 1)
                    .SelectMany(p => p.OrderByDescending(orderBy)
                    .Skip(Context.Settings.KeepDuplicateAmount)
                    .ToList()).ToList() 
                : 
                query
                    .GroupBy(p => p.PokemonId)
                    .Where(x => x.Count() > 1)
                    .SelectMany(p => p.OrderByDescending(orderBy)
                    .ThenByDescending(thenBy)
                    .Skip(Context.Settings.KeepDuplicateAmount)
                    .ToList()).ToList();


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
            return pokemons.OrderByDescending(x => Context.Utility.CalculatePokemonValue(x)).ThenBy(n => n.StaminaMax).Take(limit);
        }

        public async Task<IEnumerable<PokemonData>> GetPokemonToHeal()
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.Where(x => string.IsNullOrWhiteSpace(x.DeployedFortId) && x.Stamina > 0 && x.Stamina < x.StaminaMax).OrderByDescending(n => Context.Utility.CalculatePokemonValue(n)).ThenBy(n => n.Stamina);
        }

        public async Task<IEnumerable<PokemonData>> GetPokemonToRevive()
        {
            return (await GetPokemons()).Where(x => string.IsNullOrWhiteSpace(x.DeployedFortId) && x.Stamina == 0).OrderByDescending(n => Context.Utility.CalculatePokemonValue(n));
        }

        public async Task<IEnumerable<PokemonData>> GetHighestsVNotDeployed(int limit)
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.Where(x => string.IsNullOrWhiteSpace(x.DeployedFortId) && x.Stamina == x.StaminaMax && x.PokemonId != PokemonId.Grimer && x.PokemonId != PokemonId.Jynx).OrderByDescending(x => Context.Utility.CalculatePokemonValue(x)).ThenBy(n => n.StaminaMax).Take(limit);
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

        public async Task<PokemonData> GetHighestPokemonOfTypeByLV(PokemonData pokemon)
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.Where(x => x.PokemonId == pokemon.PokemonId)
                .OrderByDescending(PokemonInfo.GetLevel)
                .FirstOrDefault();
        }

        public async Task<PokemonData> GetHighestPokemonOfTypeByV(PokemonData pokemon)
        {
            var myPokemon = await GetPokemons();
            var pokemons = myPokemon.ToList();
            return pokemons.Where(x => x.PokemonId == pokemon.PokemonId)
                .OrderByDescending(x => Context.Utility.CalculatePokemonValue(x))
                .FirstOrDefault();
        }
        
        //GetHighestsCandies
        public async Task<IEnumerable<Candy>> GetHighestsCandies(int limit)
        {   
            var myPokemonFamilies = await GetPokemonFamilies();
            return myPokemonFamilies.OrderByDescending(x => x.Candy_ ).ThenBy(n => n.FamilyId ).Take(limit).ToArray();
        }

        public async Task<IEnumerable<Candy>> GetPokemonFamilies()
        {
            var inventory = await GetCachedInventory(Context.Client);
            return
                inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.Candy)
                    .Where(p => p != null && p.FamilyId != PokemonFamilyId.FamilyUnset);
        }

        public async Task<IEnumerable<PokemonSettings>> GetPokemonSettings()
        {
            var templates = await Context.Client.Download.GetItemTemplates();
            return
                templates.ItemTemplates.Select(i => i.PokemonSettings)
                    .Where(p => p != null && p.FamilyId != PokemonFamilyId.FamilyUnset);
        }

        public async Task<IEnumerable<PokemonData>> GetPokemonToEvolve()
        {
            var query = (await GetPokemons()).Where(p =>
            String.IsNullOrWhiteSpace(p.DeployedFortId));

            //list filter
            if (Context.Settings.UsePokemonsToEvolveList)
            {
                if (Context.Settings.PokemonsToEvolve.Count() == 0)
                    return new List<PokemonData>();

                query = query.Where(x => Context.Settings.PokemonsToEvolve.Contains(x.PokemonId));
                if (query.Count() == 0) return new List<PokemonData>();
            }

            //Evolve By CP filter
            if (Context.Settings.EvolveAboveCp > 0)
                query = query.Where(p => p.Cp > Context.Settings.EvolveAboveCp);
            if (query.Count() == 0) return new List<PokemonData>();

            //Evolve By IV filter
            if (Context.Settings.EvolveAboveIV > 0)
                query = query.Where(p => p.GetPerfection() > Context.Settings.EvolveAboveIV);
            if (query.Count() == 0) return new List<PokemonData>();

            //Evolve By V filter
            if (Context.Settings.EvolveAboveV > 0)
                query = query.Where(p => Context.Utility.CalculatePokemonValue(p) > Context.Settings.EvolveAboveV);
            if (query.Count() == 0) return new List<PokemonData>();

            //Evolve By LV filter
            if (Context.Settings.EvolveAboveLV > 0)
                query = query.Where(p => p.GetLevel() > Context.Settings.EvolveAboveLV);
            if (query.Count() == 0) return new List<PokemonData>();

            //ordering
            Func<PokemonData, double> orderBy = null;
            switch (Context.Settings.EvolvePriorityType)
            {
                case PriorityTypes.CP:
                    orderBy = new Func<PokemonData, double>(x => x.Cp);
                    break;
                case PriorityTypes.IV:
                    orderBy = new Func<PokemonData, double>(x => x.GetPerfection());
                    break;
                case PriorityTypes.V:
                    orderBy = new Func<PokemonData, double>(x => Context.Utility.CalculatePokemonValue(x));
                    break;
                case PriorityTypes.LV:
                    orderBy = new Func<PokemonData, double>(x => x.GetLevel());
                    break;
                default:
                    break;
            }

            Func<PokemonData, double> thenBy = null;
            switch (Context.Settings.EvolvePriorityType2)
            {
                case PriorityTypes.CP:
                    thenBy = new Func<PokemonData, double>(x => x.Cp);
                    break;
                case PriorityTypes.IV:
                    thenBy = new Func<PokemonData, double>(x => x.GetPerfection());
                    break;
                case PriorityTypes.V:
                    thenBy = new Func<PokemonData, double>(x => Context.Utility.CalculatePokemonValue(x));
                    break;
                case PriorityTypes.LV:
                    thenBy = new Func<PokemonData, double>(x => x.GetLevel());
                    break;
                default:
                    break;
            }

            query = orderBy == null ? query : thenBy == null ? query.OrderByDescending(orderBy) : query.OrderByDescending(orderBy).ThenByDescending(thenBy);

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
                  String.IsNullOrWhiteSpace(p.DeployedFortId) && p.GetMaxCP() > p.Cp);

            //list filter
            if (Context.Settings.UsePokemonsToPowerUpList)
            {
                if (Context.Settings.PokemonsToPowerUp.Count() == 0)
                    return new List<PokemonData>();

                query = query.Where(x => Context.Settings.PokemonsToPowerUp.Contains(x.PokemonId));
                if (query.Count() == 0) return new List<PokemonData>();
            }

            //PowerUp By CP filter
            if (Context.Settings.PowerUpAboveCp > 0)
                query = query.Where(p => p.Cp > Context.Settings.PowerUpAboveCp);
            if (query.Count() == 0) return new List<PokemonData>();

            //PowerUp By IV filter
            if (Context.Settings.PowerUpAboveIV > 0)
                query = query.Where(p => p.GetPerfection() > Context.Settings.PowerUpAboveIV);
            if (query.Count() == 0) return new List<PokemonData>();

            //PowerUp By V filter
            if (Context.Settings.PowerUpAboveV > 0)
                query = query.Where(p => Context.Utility.CalculatePokemonValue(p) > Context.Settings.PowerUpAboveV);
            if (query.Count() == 0) return new List<PokemonData>();

            //PowerUp By LV filter
            if (Context.Settings.PowerUpAboveLV > 0)
                query = query.Where(p => p.GetLevel() > Context.Settings.PowerUpAboveLV);
            if (query.Count() == 0) return new List<PokemonData>();


            //ordering
            Func<PokemonData, double> orderBy = null;
            switch (Context.Settings.PowerUpPriorityType)
            {
                case PriorityTypes.CP:
                    orderBy = new Func<PokemonData, double>(x => x.Cp);
                    break;
                case PriorityTypes.IV:
                    orderBy = new Func<PokemonData, double>(x => x.GetPerfection());
                    break;
                case PriorityTypes.V:
                    orderBy = new Func<PokemonData, double>(x => Context.Utility.CalculatePokemonValue(x));
                    break;
                case PriorityTypes.LV:
                    orderBy = new Func<PokemonData, double>(x => x.GetLevel());
                    break;
                default:
                    break;
            }

            Func<PokemonData, double> thenBy = null;
            switch (Context.Settings.PowerUpPriorityType2)
            {
                case PriorityTypes.CP:
                    thenBy = new Func<PokemonData, double>(x => x.Cp);
                    break;
                case PriorityTypes.IV:
                    thenBy = new Func<PokemonData, double>(x => x.GetPerfection());
                    break;
                case PriorityTypes.V:
                    thenBy = new Func<PokemonData, double>(x => Context.Utility.CalculatePokemonValue(x));
                    break;
                case PriorityTypes.LV:
                    thenBy = new Func<PokemonData, double>(x => x.GetLevel());
                    break;
                default:
                    break;
            }

            query = orderBy == null ? query : thenBy == null ? query.OrderByDescending(orderBy) : query.OrderByDescending(orderBy).ThenByDescending(thenBy);
            
            return query.ToList();

        }

        public async Task<List<PokemonData>> GetPokemonToFavorite()
        {
            var query = (await GetPokemons()).Where(p =>
                  String.IsNullOrWhiteSpace(p.DeployedFortId) && p.Favorite == 0);

            var specific = query;
            
            for (int i = 0; i < Context.Settings.PokemonPicker.Count; i++)
            {
                
                var pokemon = Context.Settings.PokemonPicker[i];
                if (pokemon.Task == "fav") // need to add Task to PokemonData for this to work
                {
                    specific = specific.Where(p => p.Id == pokemon.Id);
                }
            }


            //Favorite By CP filter
            if (Context.Settings.FavoriteAboveCp > 0)
                query = query.Where(p => p.Cp > Context.Settings.FavoriteAboveCp);
            if (query.Count() == 0 && specific.Count() ==0) return new List<PokemonData>();

            //Favorite By IV filter
            if (Context.Settings.FavoriteAboveIV > 0)
                query = query.Where(p => p.GetPerfection() > Context.Settings.FavoriteAboveIV);
            if (query.Count() == 0 && specific.Count() == 0) return new List<PokemonData>();

            //Favorite By V filter
            if (Context.Settings.FavoriteAboveV > 0)
                query = query.Where(p => Context.Utility.CalculatePokemonValue(p) > Context.Settings.FavoriteAboveV);
            if (query.Count() == 0 && specific.Count() == 0) return new List<PokemonData>();

            //Favorite By LV filter
            if (Context.Settings.FavoriteAboveLV > 0)
                query = query.Where(p => p.GetLevel() > Context.Settings.FavoriteAboveLV);
            if (query.Count() == 0 && specific.Count() == 0) return new List<PokemonData>();

            if (specific.Count() > 0)
            {
                return query.Concat(specific).ToList();
            }
            else
            {
                return query.ToList();
            }
            

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
                //_cachedInventory = await Context.Client.GetInventory();

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
            var inventory = await GetCachedInventory(Context.Client);
            return inventory.InventoryDelta.InventoryItems
                .Select(i => i.InventoryItemData?.Item)
                .Where(p => p != null);
        }

        public async Task<IEnumerable<ItemData>> GetItemsToRecycle(ISettings settings)
        {
            var myItems = await GetItems();

            return myItems
                .Where(x => Context.Settings.ItemRecycleFilter.Any(f => f.Key == (ItemId)x.ItemId && x.Count > f.Value))
                .Select(
                    x =>
                        new ItemData
                        {
                            ItemId = x.ItemId,
                            Count = x.Count - Context.Settings.ItemRecycleFilter.Single(f => f.Key == x.ItemId).Value,
                            Unseen = x.Unseen
                        });
        }

        public async Task<IEnumerable<EggIncubator>> GetEggIncubators()
        {
            var inventory = await GetCachedInventory(Context.Client);
            return
                inventory.InventoryDelta.InventoryItems
                    .Where(x => x.InventoryItemData.EggIncubators != null)
                    .SelectMany(i => i.InventoryItemData.EggIncubators.EggIncubator)
                    .Where(i => i != null);
        }

        public async Task<IEnumerable<PokemonData>> GetEggs()
        {
            var inventory = await GetCachedInventory(Context.Client);
            return
                inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.PokemonData)
                    .Where(p => p != null && p.IsEgg);
        }

        #endregion
        #region " Player/Trainer Methods "

        public async Task<IEnumerable<PlayerStats>> GetPlayerStats()
        {
            var inventory = await GetCachedInventory(Context.Client);
            return inventory.InventoryDelta.InventoryItems
                .Select(i => i.InventoryItemData?.PlayerStats)
                .Where(p => p != null);
        }

        public async Task<int> GetStarDust()
        {
            var StarDust = await Context.Client.Player.GetPlayer();
            var gdrfds = StarDust.PlayerData.Currencies;
            var SplitStar = gdrfds[1].Amount;
            return SplitStar;

        }

        public async Task<LevelUpRewardsResponse> GetLevelUpRewards(int level)
        {
            return await Context.Client.Player.GetLevelUpRewards(level);
        }

        public async Task<CollectDailyDefenderBonusResponse> CollectDailyDefenderBonus()
        {
            return await Context.Client.Player.CollectDailyDefenderBonus();
        }

        public async Task<CollectDailyBonusResponse> CollectDailyBonus()
        {
            return await Context.Client.Player.CollectDailyBonus();
        }

        public async Task<SetPlayerTeamResponse> SetPlayerTeam(TeamColor team)
        {
            return await Context.Client.Player.SetPlayerTeam(team);
        }

        public async Task<CheckAwardedBadgesResponse> GetNewlyAwardedBadges()
        {
            return await Context.Client.Player.GetNewlyAwardedBadges();
        }

        #endregion
        #region " Tutorial Methods "

        public async Task<MarkTutorialCompleteResponse> TutorialMarkComplete(TutorialState tutorialState, bool sendMarketing, bool pushNotifications)
        {
            var list = new List<TutorialState>();
            list.Add(tutorialState);
            return await TutorialMarkComplete(new List<TutorialState>(list), sendMarketing, pushNotifications);
        }
        public async Task<MarkTutorialCompleteResponse> TutorialMarkComplete(IEnumerable<TutorialState> tutorialStates,bool sendMarketing, bool pushNotifications)
        {
            return await Context.Client.Misc.MarkTutorialComplete(tutorialStates, true, true);
        }
        public async Task<ClaimCodenameResponse> TutorialClaimCodeName(string codeName)
        {
            return await Context.Client.Misc.ClaimCodename(codeName);
        }
        public async Task<CheckCodenameAvailableResponse> CheckCodenameAvailable(string codeName)
        {
            return await Context.Client.Misc.CheckCodenameAvailable(codeName);
        }
        public async Task<GetSuggestedCodenamesResponse> TutorialGetSuggestedCodenames()
        {
            return await Context.Client.Misc.GetSuggestedCodenames();
        }
        public async Task<SetAvatarResponse> TutorialSetAvatar(PlayerAvatar avatar)
        {
            return await Context.Client.Player.SetAvatar(avatar);
        }
        public async Task<SetContactSettingsResponse> TutorialSetContactSettings(ContactSettings contactSettings)
        {
            return await Context.Client.Player.SetContactSetting(contactSettings);
        }
        public async Task<EncounterTutorialCompleteResponse> TutorialPokemonCapture(PokemonId pokemonId)
        {
            return await Context.Client.Encounter.EncounterTutorialComplete(pokemonId);
        }

        #endregion
        #region " Export Methods "

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
                    string header = "PokemonID,Name,NickName,CP / MaxCP,Perfection,True Value,Attack 1,Attack 2,HP,Attk,Def,Stamina,Familie Candies,ID,previewLink";
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
                            //Generate base64 code to make it viewable here http://poke.isitin.org/#MTUwLDIzLDE3LDE5MDIsMTE4
                            var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(toEncode));
                            var settings = pokemonSettings.Single(x => x.PokemonId == pokemon.PokemonId);
                            var familiecandies = pokemonFamilies.Single(x => settings.FamilyId == x.FamilyId).Candy_;
                            string perfection = pokemon.GetPerfection().ToString("0.00");
                            perfection = perfection.Replace(",", ls == "," ? "." : ",");
                            string truevalue = Context.Utility.CalculatePokemonValue(pokemon).ToString();
                            string content_part1 = $"{(int)pokemon.PokemonId},{pokemon.PokemonId},{pokemon.Nickname},{pokemon.Cp}/{PokemonInfo.CalculateMaxCP(pokemon)},";
                            string content_part2 = $",{truevalue},{pokemon.Move1},{pokemon.Move2},{pokemon.Stamina},{pokemon.IndividualAttack},{pokemon.IndividualDefense},{pokemon.IndividualStamina},{familiecandies},{pokemon.Id},http://poke.isitin.org/#{encoded}";
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
