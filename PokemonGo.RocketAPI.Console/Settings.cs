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
    [Serializable]
    public class Settings : ISettings
    {
        private static object syncRoot => new object();

        private string configs_path = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
        private ICollection<PokemonId> _pokemonsNotToTransfer;
        private ICollection<PokemonId> _pokemonsToEvolve;
        private ICollection<PokemonId> _pokemonsNotToCatch;
        private ICollection<KeyValuePair<ItemId, int>> _itemRecycleFilter;

        public AuthType AuthType { get; set; }
        public string PtcUsername { get; set; }
        public string PtcPassword { get; set; }
        public double DefaultLatitude { get; set; }
        public double DefaultLongitude { get; set; }
        public double DefaultAltitude { get; set; }
        public int DestinationIndex { get; set; }
        public bool CatchPokemon { get; set; }
        public bool UseGPXPathing { get; set; }
        public string GPXFile { get; set; }
        public double WalkingSpeedInKilometerPerHour { get; set; }
        public int MaxTravelDistanceInMeters { get; set; }
        public bool UsePokemonToNotCatchList { get; set; }
        public bool EvolvePokemon { get; set; }
        public bool EvolveOnlyPokemonAboveIV { get; set; }
        public float EvolveOnlyPokemonAboveIVValue { get; set; }
        public bool TransferPokemon { get; set; }
        public int TransferPokemonKeepDuplicateAmount { get; set; }
        public bool NotTransferPokemonsThatCanEvolve { get; set; }
        public float KeepMinIVPercentage { get; set; }
        public int KeepMinCP { get; set; }
        public bool useLuckyEggsWhileEvolving { get; set; }
        public bool PrioritizeIVOverCP { get; set; }
        public bool LoiteringActive { get; set; }
        public int MinutesPerDestination { get; set; }
        public int FlyingSpeedInKilometerPerHour { get; set; }
        public bool CatchWhileFlying { get; set; }
        public bool FlyingEnabled { get; set; }
        public bool MoveWhenNoStops { get; set; }
        public bool PrioritizeStopsWithLures { get; set; }
        public bool DestinationsEnabled { get; set; }
        public int DisplayRefreshMinutes { get; set; }
        public bool DisplayAllPokemonInLog { get; set; }
        public bool EnableSpeedAdjustment { get; set; }
        public bool EnableSpeedRandomizer { get; set; }
        public int WalkingSpeedInKilometerPerHourMax { get; set; }
        public int MaxSecondsBetweenStops { get; set; }

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

        public Settings()
        {
            this.AuthType = (AuthType)Enum.Parse(typeof(AuthType), UserSettings.Default.AuthType, true);
            this.CatchPokemon = UserSettings.Default.CatchPokemon;
            this.CatchWhileFlying = UserSettings.Default.CatchWhileFlying;
            this.DefaultAltitude = UserSettings.Default.DefaultAltitude;
            this.DefaultLatitude = UserSettings.Default.DefaultLatitude;
            this.DefaultLongitude = UserSettings.Default.DefaultLongitude;
            this.DestinationIndex = UserSettings.Default.DestinationIndex;
            this.DestinationsEnabled = UserSettings.Default.DestinationsEnabled;
            this.DisplayAllPokemonInLog = UserSettings.Default.DisplayAllPokemonInLog;
            this.DisplayRefreshMinutes = UserSettings.Default.DisplayRefreshMinutes;
            this.EnableSpeedAdjustment = UserSettings.Default.EnableSpeedAdjustment;
            this.EnableSpeedRandomizer = UserSettings.Default.EnableSpeedRandomizer;
            this.EvolveOnlyPokemonAboveIV = UserSettings.Default.EvolveOnlyPokemonAboveIV;
            this.EvolveOnlyPokemonAboveIVValue = UserSettings.Default.EvolveOnlyPokemonAboveIVValue;
            this.EvolvePokemon = UserSettings.Default.EvolvePokemon;
            this.FlyingEnabled = UserSettings.Default.FlyingEnabled;
            this.FlyingSpeedInKilometerPerHour = UserSettings.Default.FlyingSpeedInKilometerPerHour;
            this.GPXFile = UserSettings.Default.GPXFile;
            this.KeepMinCP = UserSettings.Default.KeepMinCP;
            this.KeepMinIVPercentage = UserSettings.Default.KeepMinIVPercentage;
            this.LoiteringActive = UserSettings.Default.LoiteringActive;
            this.MaxSecondsBetweenStops = UserSettings.Default.MaxSecondsBetweenStops;
            this.MaxTravelDistanceInMeters = UserSettings.Default.MaxTravelDistanceInMeters;
            this.MinutesPerDestination = UserSettings.Default.MinutesPerDestination;
            this.MoveWhenNoStops = UserSettings.Default.MoveWhenNoStops;
            this.NotTransferPokemonsThatCanEvolve = UserSettings.Default.NotTransferPokemonsThatCanEvolve;
            this.PrioritizeIVOverCP = UserSettings.Default.PrioritizeIVOverCP;
            this.PrioritizeStopsWithLures = UserSettings.Default.PrioritizeStopsWithLures;
            this.PtcPassword = UserSettings.Default.PtcPassword;
            this.PtcUsername = UserSettings.Default.PtcUsername;
            this.TransferPokemon = UserSettings.Default.TransferPokemon;
            this.TransferPokemonKeepDuplicateAmount = UserSettings.Default.TransferPokemonKeepDuplicateAmount;
            this.UseGPXPathing = UserSettings.Default.UseGPXPathing;
            this.useLuckyEggsWhileEvolving = UserSettings.Default.useLuckyEggsWhileEvolving;
            this.UsePokemonToNotCatchList = UserSettings.Default.UsePokemonToNotCatchList;
            this.WalkingSpeedInKilometerPerHour = UserSettings.Default.WalkingSpeedInKilometerPerHour;
            this.WalkingSpeedInKilometerPerHourMax = UserSettings.Default.WalkingSpeedInKilometerPerHourMax;
        }

        public void Save()
        {
            //UserSettings.Default.Save();
            try
            {
                string fileName = "Settings.xml";
                string filePath = Path.Combine(configs_path, fileName);
                lock (syncRoot)
                {
                    using (FileStream s = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        var x = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
                        x.Serialize(s, this);
                        s.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Write("Could not save settings to file. Error: " + e.ToString(), LogLevel.Error);
            }
        }
        public void Load()
        {
            if (!Directory.Exists(configs_path))
                Directory.CreateDirectory(configs_path);
            string fileName = "Settings.xml";
            string filePath = Path.Combine(configs_path, fileName);
            if (!File.Exists(filePath))
            {
                Logger.Write($"File: \"\\Configs\\{fileName}\" not found, creating new...", LogLevel.Warning);
                Save();
            }
            else
            {
                Logger.Write($"Loading File: \"\\Configs\\{fileName}\"", LogLevel.Info);

                var content = string.Empty;
                Settings obj = null;
                lock (syncRoot)
                {
                    using (FileStream s = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        var x = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
                        obj = (Settings)x.Deserialize(s);
                        s.Close();
                    }
                }

                if (obj != null)
                {
                    this.AuthType = obj.AuthType;
                    this.CatchPokemon = obj.CatchPokemon;
                    this.CatchWhileFlying = obj.CatchWhileFlying;
                    this.DefaultAltitude = obj.DefaultAltitude;
                    this.DefaultLatitude = obj.DefaultLatitude;
                    this.DefaultLongitude = obj.DefaultLongitude;
                    this.DestinationIndex = obj.DestinationIndex;
                    this.DestinationsEnabled = obj.DestinationsEnabled;
                    this.DisplayAllPokemonInLog = obj.DisplayAllPokemonInLog;
                    this.DisplayRefreshMinutes = obj.DisplayRefreshMinutes;
                    this.EnableSpeedAdjustment = obj.EnableSpeedAdjustment;
                    this.EnableSpeedRandomizer = obj.EnableSpeedRandomizer;
                    this.EvolveOnlyPokemonAboveIV = obj.EvolveOnlyPokemonAboveIV;
                    this.EvolveOnlyPokemonAboveIVValue = obj.EvolveOnlyPokemonAboveIVValue;
                    this.EvolvePokemon = obj.EvolvePokemon;
                    this.FlyingEnabled = obj.FlyingEnabled;
                    this.FlyingSpeedInKilometerPerHour = obj.FlyingSpeedInKilometerPerHour;
                    this.GPXFile = obj.GPXFile;
                    this.KeepMinCP = obj.KeepMinCP;
                    this.KeepMinIVPercentage = obj.KeepMinIVPercentage;
                    this.LoiteringActive = obj.LoiteringActive;
                    this.MaxSecondsBetweenStops = obj.MaxSecondsBetweenStops;
                    this.MaxTravelDistanceInMeters = obj.MaxTravelDistanceInMeters;
                    this.MinutesPerDestination = obj.MinutesPerDestination;
                    this.MoveWhenNoStops = obj.MoveWhenNoStops;
                    this.NotTransferPokemonsThatCanEvolve = obj.NotTransferPokemonsThatCanEvolve;
                    this.PrioritizeIVOverCP = obj.PrioritizeIVOverCP;
                    this.PrioritizeStopsWithLures = obj.PrioritizeStopsWithLures;
                    this.PtcPassword = obj.PtcPassword;
                    this.PtcUsername = obj.PtcUsername;
                    this.TransferPokemon = obj.TransferPokemon;
                    this.TransferPokemonKeepDuplicateAmount = obj.TransferPokemonKeepDuplicateAmount;
                    this.UseGPXPathing = obj.UseGPXPathing;
                    this.useLuckyEggsWhileEvolving = obj.useLuckyEggsWhileEvolving;
                    this.UsePokemonToNotCatchList = obj.UsePokemonToNotCatchList;
                    this.WalkingSpeedInKilometerPerHour = obj.WalkingSpeedInKilometerPerHour;
                    this.WalkingSpeedInKilometerPerHourMax = obj.WalkingSpeedInKilometerPerHourMax;
                }
            }
        }
    }
}
