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

using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Responses;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Map.Fort;
using POGOProtos.Map.Pokemon;

using PokeRoadie.Extensions;
using PokeRoadie.Utils;
//using PokeRoadie.Logging;
using System.ComponentModel;
using Google.Protobuf.Collections;

#endregion


namespace PokeRoadie
{
    internal class Statistics
    {
        public int TotalExperience;
        public int TotalPokemons;
        public int TotalItemsRemoved;
        public int TotalPokemonsTransfered;
        public int TotalStardust;
        public string CurrentLevelInfos;
        public int Currentlevel = -1;
        public string PlayerName;
        public int TotalPokesInBag;
        public int TotalPokesInPokedex;
        public int LevelForRewards = -1;
        public int _level = 0;
        public DateTime InitSessionDateTime;
        public PokeRoadieInventory _inventory = null;
        public TimeSpan Duration;

        public Statistics(PokeRoadieInventory inventory)
        {
            _inventory = inventory;
            InitSessionDateTime = DateTime.Now;
            Duration = DateTime.Now - InitSessionDateTime;
        }
        public async Task<string> _getcurrentLevelInfos(PokeRoadieInventory _inventory)
        {
            var stats = await _inventory.GetPlayerStats();
            var output = string.Empty;
            var stat = stats.FirstOrDefault();
            if (stat != null)
            {
                var ep = (stat.NextLevelXp - stat.PrevLevelXp) - (stat.Experience - stat.PrevLevelXp);
                var time = Math.Round(ep / (TotalExperience / _getSessionRuntime()), 2);
                var hours = 0.00;
                var minutes = 0.00;
                if (Double.IsInfinity(time) == false && time > 0)
                {
                    time = Convert.ToDouble(TimeSpan.FromHours(time).ToString("h\\.mm"), System.Globalization.CultureInfo.InvariantCulture);
                    hours = Math.Truncate(time);
                    minutes = Math.Round((time - hours) * 100);
                }

                bool didLevelUp = false;

                if (LevelForRewards == -1 || stat.Level >= LevelForRewards)
                {
                    LevelUpRewardsResponse Result = await _inventory.GetLevelUpRewards(stat.Level);

                    if (Result.Result == LevelUpRewardsResponse.Types.Result.AwardedAlready)
                        LevelForRewards = stat.Level + 1;

                    if (Result.Result == LevelUpRewardsResponse.Types.Result.Success)
                    {
                        didLevelUp = true;
                        Logger.Write($"(LEVEL) Reached level {stat.Level}!", LogLevel.None, ConsoleColor.Green);

                        RepeatedField<ItemAward> items = Result.ItemsAwarded;

                        if (items.Any<ItemAward>())
                        {
                            Logger.Write("- Received Bonus Items -", LogLevel.Info);
                            foreach (ItemAward item in items)
                            {
                                Logger.Write($"[ITEM] {item.ItemId} x {item.ItemCount} ", LogLevel.Info);
                            }
                        }
                    }
                }

                if (!didLevelUp)
                    output = $"{stat.Level} (Level in {hours}h {minutes}m | {stat.Experience - stat.PrevLevelXp - GetXpDiff(stat.Level)}/{stat.NextLevelXp - stat.PrevLevelXp - GetXpDiff(stat.Level)} XP)";
            }
            return output;
        }

        public string GetUsername(PokeRoadieClient client, GetPlayerResponse profile)
        {
           
            return PlayerName = client.Settings.AuthType == AuthType.Ptc ? client.Settings.PtcUsername : (profile == null || profile.PlayerData  == null ? client.Settings.GoogleUsername : profile.PlayerData.Username);
        }

        public double _getSessionRuntime()
        {
            return (DateTime.Now - InitSessionDateTime).TotalSeconds/3600;
        }

        public string _getSessionRuntimeInTimeFormat()
        {
            return (DateTime.Now - InitSessionDateTime).ToString(@"dd\.hh\:mm\:ss");
        }

        public void AddExperience(int xp)
        {
            TotalExperience += xp;
        }

        public void AddItemsRemoved(int count)
        {
            TotalItemsRemoved += count;
        }

        public void GetStardust(int stardust)
        {
            TotalStardust = stardust;
        }

        public void IncreasePokemons()
        {
            TotalPokemons += 1;
        }

        public void IncreasePokemonsTransfered()
        {
            TotalPokemonsTransfered += 1;
        }

        public async void UpdateConsoleTitle(PokeRoadieClient _client, PokeRoadieInventory _inventory)
        {
            //appears to give incorrect info?		
            var pokes = await _inventory.GetPokemons();
            TotalPokesInBag = pokes.Count();

            var inventory = await PokeRoadieInventory.getCachedInventory(_client);

            if (inventory.InventoryDelta != null)
            {
                TotalPokesInPokedex = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.PokedexEntry).Where(x => x != null && x.TimesCaptured >= 1).OrderBy(k => k.PokemonId).ToArray().Length;
                CurrentLevelInfos = await _getcurrentLevelInfos(_inventory);
            }
            Console.Title = ToString();
        }

        public override string ToString()
        {
            return
                string.Format(
                    "{0} - Runtime {1} - Lvl: {2:0} | EXP/H: {3:0} | P/H: {4:0} | Stardust: {5:0} | Transfered: {6:0} | Items Recycled: {7:0} | Pokemon: {8:0} | Pokedex: {9:0}/147",
                    PlayerName, _getSessionRuntimeInTimeFormat(), CurrentLevelInfos, TotalExperience / _getSessionRuntime(),
                    TotalPokemons / _getSessionRuntime(), TotalStardust, TotalPokemonsTransfered, TotalItemsRemoved, TotalPokesInBag, TotalPokesInPokedex);
        }

        public int GetXpDiff(int level)
        {
            if (level > 0 && level <= 40)
            {
                int[] xpTable = { 0, 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000,
                    10000, 10000, 10000, 10000, 15000, 20000, 20000, 20000, 25000, 25000,
                    50000, 75000, 100000, 125000, 150000, 190000, 200000, 250000, 300000, 350000,
                    500000, 500000, 750000, 1000000, 1250000, 1500000, 2000000, 2500000, 1000000, 1000000};
                return xpTable[level - 1];
            }

            return 0;
        }
        public async Task<LevelUpRewardsResponse> Execute(PokeRoadieClient ctx)
        {
            var Result = await GetLevelUpRewards(LevelForRewards);
            return Result;
        }

        public async Task<LevelUpRewardsResponse> GetLevelUpRewards(int level)
        {
            if (_level == 0 || level > _level)
            {
                _level = level;
                return await _inventory.GetLevelUpRewards(level);
            }

            return new LevelUpRewardsResponse();
        }
    }
}