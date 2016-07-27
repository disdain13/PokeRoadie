#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.GeneratedCode;
using PokemonGo.RocketAPI.Logging;


#endregion


namespace PokemonGo.RocketAPI.Console
{
    public class Settings : ISettings
    {
        private string configs_path = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
        private ICollection<PokemonId> _pokemonsNotToTransfer;
        private ICollection<PokemonId> _pokemonsToEvolve;
        private ICollection<PokemonId> _pokemonsNotToCatch;
        private ICollection<KeyValuePair<ItemId, int>> _itemRecycleFilter;

        public AuthType AuthType => (AuthType)Enum.Parse(typeof(AuthType), UserSettings.Default.AuthType, true);
        public string PtcUsername => UserSettings.Default.PtcUsername;
        public string PtcPassword => UserSettings.Default.PtcPassword;

        public double DefaultLatitude { get { return UserSettings.Default.DefaultLatitude; } set { UserSettings.Default.DefaultLatitude = value; } }
        public double DefaultLongitude { get { return UserSettings.Default.DefaultLongitude; } set { UserSettings.Default.DefaultLongitude = value; } }
        public double DefaultAltitude { get { return UserSettings.Default.DefaultAltitude; } set { UserSettings.Default.DefaultAltitude = value; } }
        public int DestinationIndex { get { return UserSettings.Default.DestinationIndex; } set { UserSettings.Default.DestinationIndex = value; } }
        public bool CatchPokemon { get { return UserSettings.Default.CatchPokemon; } set { UserSettings.Default.CatchPokemon = value; } }

        public bool UseGPXPathing => UserSettings.Default.UseGPXPathing;
        public string GPXFile => UserSettings.Default.GPXFile;
        public double WalkingSpeedInKilometerPerHour => UserSettings.Default.WalkingSpeedInKilometerPerHour;
        public int MaxTravelDistanceInMeters => UserSettings.Default.MaxTravelDistanceInMeters;

        public bool UsePokemonToNotCatchList => UserSettings.Default.UsePokemonToNotCatchList;
        public bool EvolvePokemon => UserSettings.Default.EvolvePokemon;
        public bool EvolveOnlyPokemonAboveIV => UserSettings.Default.EvolveOnlyPokemonAboveIV;
        public float EvolveOnlyPokemonAboveIVValue => UserSettings.Default.EvolveOnlyPokemonAboveIVValue;
        public bool TransferPokemon => UserSettings.Default.TransferPokemon;
        public int TransferPokemonKeepDuplicateAmount => UserSettings.Default.TransferPokemonKeepDuplicateAmount;
        public bool NotTransferPokemonsThatCanEvolve => UserSettings.Default.NotTransferPokemonsThatCanEvolve;

        public float KeepMinIVPercentage => UserSettings.Default.KeepMinIVPercentage;
        public int KeepMinCP => UserSettings.Default.KeepMinCP;
        public bool useLuckyEggsWhileEvolving => UserSettings.Default.useLuckyEggsWhileEvolving;
        public bool PrioritizeIVOverCP => UserSettings.Default.PrioritizeIVOverCP;
        public bool LoiteringActive => UserSettings.Default.LoiteringActive;
        public int MinutesPerDestination => UserSettings.Default.MinutesPerDestination;
        public int FlyingSpeedInKilometerPerHour => UserSettings.Default.FlyingSpeedInKilometerPerHour;
        public bool CatchWhileFlying => UserSettings.Default.CatchWhileFlying;
        public bool FlyingEnabled => UserSettings.Default.FlyingEnabled;
        public bool MoveWhenNoStops => UserSettings.Default.MoveWhenNoStops;
        public bool PrioritizeStopsWithLures => UserSettings.Default.PrioritizeStopsWithLures;
        public bool DestinationsEnabled => UserSettings.Default.DestinationsEnabled;
        public int DisplayRefreshMinutes => UserSettings.Default.DisplayRefreshMinutes;
        public bool DisplayAllPokemonInLog => UserSettings.Default.DisplayAllPokemonInLog;
        public bool EnableSpeedAdjustment => UserSettings.Default.EnableSpeedAdjustment;
        public bool EnableSpeedRandomizer => UserSettings.Default.EnableSpeedRandomizer;
        public int WalkingSpeedInKilometerPerHourMax => UserSettings.Default.WalkingSpeedInKilometerPerHourMax;
        public int MaxSecondsBetweenStops => UserSettings.Default.MaxSecondsBetweenStops;

        public ICollection<KeyValuePair<ItemId, int>> ItemRecycleFilter 
        {
            get
            {
                //Type of pokemons to evolve
                var defaultItems = new List<KeyValuePair<ItemId, int>>
                {
                    new KeyValuePair<ItemId, int>(ItemId.ItemUnknown, 0),
                    new KeyValuePair<ItemId, int>(ItemId.ItemPokeBall, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemGreatBall, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemUltraBall, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemMasterBall, 100),

                    new KeyValuePair<ItemId, int>(ItemId.ItemPotion, 10),
                    new KeyValuePair<ItemId, int>(ItemId.ItemSuperPotion, 10),
                    new KeyValuePair<ItemId, int>(ItemId.ItemHyperPotion, 25),
                    new KeyValuePair<ItemId, int>(ItemId.ItemMaxPotion, 25),

                    new KeyValuePair<ItemId, int>(ItemId.ItemRevive, 10),
                    new KeyValuePair<ItemId, int>(ItemId.ItemMaxRevive, 25),

                    new KeyValuePair<ItemId, int>(ItemId.ItemLuckyEgg, 200),

                    new KeyValuePair<ItemId, int>(ItemId.ItemIncenseOrdinary, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemIncenseSpicy, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemIncenseCool, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemIncenseFloral, 100),

                    new KeyValuePair<ItemId, int>(ItemId.ItemTroyDisk, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemXAttack, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemXDefense, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemXMiracle, 100),

                    new KeyValuePair<ItemId, int>(ItemId.ItemRazzBerry, 50),
                    new KeyValuePair<ItemId, int>(ItemId.ItemBlukBerry, 10),
                    new KeyValuePair<ItemId, int>(ItemId.ItemNanabBerry, 10),
                    new KeyValuePair<ItemId, int>(ItemId.ItemWeparBerry, 30),
                    new KeyValuePair<ItemId, int>(ItemId.ItemPinapBerry, 30),

                    new KeyValuePair<ItemId, int>(ItemId.ItemSpecialCamera, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemIncubatorBasicUnlimited, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemIncubatorBasic, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemPokemonStorageUpgrade, 100),
                    new KeyValuePair<ItemId, int>(ItemId.ItemItemStorageUpgrade, 100)
                };
                _itemRecycleFilter = _itemRecycleFilter ?? LoadItemList("Configs\\ConfigItemList.ini", defaultItems);
                return _itemRecycleFilter;
            }

        }


        public ICollection<KeyValuePair<ItemId, int>> LoadItemList(string filename, List<KeyValuePair<ItemId, int>> defaultItems)
        {
            ICollection<KeyValuePair<ItemId, int>> result = new List<KeyValuePair<ItemId, int>>();

            DirectoryInfo di = Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\Configs");

            if (File.Exists(Directory.GetCurrentDirectory() + "\\" + filename))
            {
                Logger.Write($"Loading File: {filename}");

                var content = string.Empty;
                using (StreamReader reader = new StreamReader(filename))
                {
                    content = reader.ReadToEnd();
                    reader.Close();
                }

                content = Regex.Replace(content, @"\\/\*(.|\n)*?\*\/", ""); //todo: supposed to remove comment blocks


                StringReader tr = new StringReader(content);

                var itemInfo = tr.ReadLine();
                while (itemInfo != null)
                {
                    string[] itemInfoArray = itemInfo.Split(' ');
                    string itemName = itemInfoArray.Length > 1 ? itemInfoArray[0] : "";
                    int itemAmount = 0;
                    if (!Int32.TryParse(itemInfoArray.Length > 1 ? itemInfoArray[1] : "100", out itemAmount)) itemAmount = 100;

                    ItemId item;
                    if (Enum.TryParse<ItemId>(itemName, out item))
                    {
                        result.Add(new KeyValuePair<ItemId, int>(item, itemAmount));
                    }
                    itemInfo = tr.ReadLine();
                }
            }
            else
            {
                Logger.Write($"File: {filename} not found, creating new...", LogLevel.Warning);
                using (var w = File.AppendText(Directory.GetCurrentDirectory() + "\\" + filename))
                {
                    defaultItems.ForEach(itemInfo => w.WriteLine($"{itemInfo.Key} {itemInfo.Value}"));
                    defaultItems.ForEach(itemInfo => result.Add(itemInfo));
                    w.Close();
                }
            }
            return result;
        }

        public ICollection<PokemonId> PokemonsToEvolve
        {
            get
            {
                //Type of pokemons to evolve
                List<PokemonId> defaultPokemon = new List<PokemonId> {
                    PokemonId.Zubat, PokemonId.Pidgey, PokemonId.Rattata
                };
                _pokemonsToEvolve = _pokemonsToEvolve ?? LoadPokemonList("PokemonsToEvolve.ini", defaultPokemon);
                return _pokemonsToEvolve;
            }
        }

        public ICollection<PokemonId> PokemonsNotToTransfer
        {
            get
            {
                //Type of pokemons not to transfer
                List<PokemonId> defaultPokemon = new List<PokemonId> {
                    PokemonId.Dragonite, PokemonId.Charizard, PokemonId.Zapdos, PokemonId.Snorlax, PokemonId.Alakazam, PokemonId.Mew, PokemonId.Mewtwo
                };
                _pokemonsNotToTransfer = _pokemonsNotToTransfer ?? LoadPokemonList("PokemonsNotToTransfer.ini", defaultPokemon);
                return _pokemonsNotToTransfer;
            }
        }

        public ICollection<PokemonId> PokemonsNotToCatch
        {
            get
            {
                //Type of pokemons not to catch
                List<PokemonId> defaultPokemon = new List<PokemonId> {
                    PokemonId.Zubat, PokemonId.Pidgey, PokemonId.Rattata
                };
                _pokemonsNotToCatch = _pokemonsNotToCatch ?? LoadPokemonList("PokemonsNotToCatch.ini", defaultPokemon);
                return _pokemonsNotToCatch;
            }
        }

        private ICollection<PokemonId> LoadPokemonList(string filename, List<PokemonId> defaultPokemon)
        {
            ICollection<PokemonId> result = new List<PokemonId>();
            if (!Directory.Exists(configs_path))
                Directory.CreateDirectory(configs_path);
            string pokemonlist_file = Path.Combine(configs_path, filename);
            if (!File.Exists(pokemonlist_file))
            {
                Logger.Write($"File: \"\\Configs\\{filename}\" not found, creating new...", LogLevel.Warning);
                using (var w = File.AppendText(pokemonlist_file))
                {
                    defaultPokemon.ForEach(pokemon => w.WriteLine(pokemon.ToString()));
                    defaultPokemon.ForEach(pokemon => result.Add((PokemonId)pokemon));
                    w.Close();
                }
            }
            if (File.Exists(pokemonlist_file))
            {
                Logger.Write($"Loading File: \"\\Configs\\{filename}\"", LogLevel.Info);

                var content = string.Empty;
                using (StreamReader reader = new StreamReader(pokemonlist_file))
                {
                    content = reader.ReadToEnd();
                    reader.Close();
                }
                content = Regex.Replace(content, @"\\/\*(.|\n)*?\*\/", ""); //todo: supposed to remove comment blocks

                StringReader tr = new StringReader(content);

                var pokemonName = tr.ReadLine();
                while (pokemonName != null)
                {
                    PokemonId pokemon;
                    if (Enum.TryParse<PokemonId>(pokemonName, out pokemon))
                    {
                        result.Add((PokemonId)pokemon);
                    }
                    pokemonName = tr.ReadLine();
                }
            }
            return result;
        }

        public void SetDefaultLocation(double lat, double lng, double z)
        {
            DefaultLatitude = lat;
            DefaultLongitude = lng;
            DefaultAltitude = z;
            Save();
        }
        public void Save()
        {
            UserSettings.Default.Save();
        }
    }
}
