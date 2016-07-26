#region

using System;
using System.Linq;
using System.Threading.Tasks;
using PokemonGo.RocketAPI.GeneratedCode;
using PokemonGo.RocketAPI.Enums;
using System.Globalization;

#endregion


namespace PokemonGo.RocketAPI.Logic.Utils
{
    internal class Statistics
    {
        public static int TotalExperience;
        public static int TotalPokemons;
        public static int TotalItemsRemoved;
        public static int TotalPokemonsTransfered;
        public static int TotalStardust;
        public static string CurrentLevelInfos;
        public static int Currentlevel = -1;
        public static string PlayerName;
        public static int TotalPokesInBag;
        public static int TotalPokesInPokedex;

        public static DateTime InitSessionDateTime = DateTime.Now;
        public static TimeSpan Duration = DateTime.Now - InitSessionDateTime;

        public static async Task<string> _getcurrentLevelInfos(Inventory _inventory)
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
                    time = Convert.ToDouble(TimeSpan.FromHours(time).ToString("h\\.mm"), CultureInfo.InvariantCulture);
                    hours = Math.Truncate(time);
                    minutes = Math.Round((time - hours) * 100);
                }

                output = $"{stat.Level} (LvLUp in {hours}h {minutes}m | {stat.Experience - stat.PrevLevelXp - GetXpDiff(stat.Level)}/{stat.NextLevelXp - stat.PrevLevelXp - GetXpDiff(stat.Level)} XP)";
                //output = $"{stat.Level} (LvLUp in {_hours}hours // EXP required: {_ep})";
            }
            return output;
        }

        public static string GetUsername(Client client, GetPlayerResponse profile)
        {
            return PlayerName = client.Settings.AuthType == AuthType.Ptc ? client.Settings.PtcUsername : profile.Profile.Username;
        }

        public static double _getSessionRuntime()
        {
            return (DateTime.Now - InitSessionDateTime).TotalSeconds/3600;
        }

        public static string _getSessionRuntimeInTimeFormat()
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

        public async void UpdateConsoleTitle(Client _client, Inventory _inventory)
        {
            //appears to give incorrect info?		
            var pokes = await _inventory.GetPokemons();
            TotalPokesInBag = pokes.Count();

            var inventory = await _client.GetInventory();
            TotalPokesInPokedex = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.PokedexEntry).Where(x => x != null && x.TimesCaptured >= 1).OrderBy(k => k.PokedexEntryNumber).ToArray().Length;
            
            CurrentLevelInfos = await _getcurrentLevelInfos(_inventory);
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

        public static int GetXpDiff(int level)
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
    }
}