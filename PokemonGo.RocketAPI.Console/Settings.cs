#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Logging;
using PokemonGo.RocketAPI.Extensions;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Responses;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Data;
using POGOProtos.Data.Player;
using POGOProtos.Enums;

#endregion


namespace PokemonGo.RocketAPI.Console
{
    [Serializable]
    public class Settings : ISettings
    {

        #region " Members "

        private static object syncRoot => new object();
        private string configs_path = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
        private ICollection<PokemonId> _pokemonsNotToTransfer;
        private ICollection<PokemonId> _pokemonsToEvolve;
        private ICollection<PokemonId> _pokemonsNotToCatch;
        private ICollection<KeyValuePair<ItemId, int>> _itemRecycleFilter;
        private ICollection<PokemonMoveDetail> _pokemonMoveDetails;

        #endregion
        #region " Properties "

        public AuthType AuthType { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public double CurrentLatitude { get; set; }
        public double CurrentLongitude { get; set; }
        public double CurrentAltitude { get; set; }
        public int DestinationIndex { get; set; }
        public bool CatchPokemon { get; set; }
        public bool UseGPXPathing { get; set; }
        public string GPXFile { get; set; }
        public double MinSpeed { get; set; }
        public int MaxDistance { get; set; }
        public bool UsePokemonToNotCatchList { get; set; }
        public bool EvolvePokemon { get; set; }
        public bool EvolveOnlyPokemonAboveIV { get; set; }
        public double EvolveOnlyPokemonAboveIVValue { get; set; }
        public bool TransferPokemon { get; set; }
        public int KeepDuplicateAmount { get; set; }
        public bool NotTransferPokemonsThatCanEvolve { get; set; }
        public double KeepAboveIV { get; set; }
        public double KeepAboveV { get; set; }
        public int KeepAboveCP { get; set; }
        public double TransferBelowIV { get; set; }
        public double TransferBelowV { get; set; }
        public int TransferBelowCP { get; set; }
        public bool UseLuckyEggs { get; set; }
        public bool LoiteringActive { get; set; }
        public int MinutesPerDestination { get; set; }
        public int FlyingSpeed { get; set; }
        public bool CatchWhileFlying { get; set; }
        public bool FlyingEnabled { get; set; }
        public bool MoveWhenNoStops { get; set; }
        public bool PrioritizeStopsWithLures { get; set; }
        public bool DestinationsEnabled { get; set; }
        public int DisplayRefreshMinutes { get; set; }
        public bool DisplayAggregateLog { get; set; }
        public bool DisplayAllPokemonInLog { get; set; }
        public bool EnableSpeedAdjustment { get; set; }
        public bool EnableSpeedRandomizer { get; set; }
        public int MaxSpeed { get; set; }
        public int MaxSecondsBetweenStops { get; set; }
        public PriorityType PriorityType { get; set; }

        public ICollection<KeyValuePair<ItemId, int>> ItemRecycleFilter
        {
            get
            {
                //Type of pokemons to evolve
                _itemRecycleFilter = _itemRecycleFilter ?? LoadItemList("Configs\\ConfigItemList.ini", new List<KeyValuePair<ItemId, int>>
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
                });
                return _itemRecycleFilter;
            }

        }


        public ICollection<PokemonId> PokemonsToEvolve
        {
            get
            {
                //Type of pokemons to evolve
                _pokemonsToEvolve = _pokemonsToEvolve ?? LoadPokemonList("PokemonsToEvolve.ini", new List<PokemonId> {
                    PokemonId.Zubat, PokemonId.Pidgey, PokemonId.Rattata
                });
                return _pokemonsToEvolve;
            }
        }

        public ICollection<PokemonId> PokemonsNotToTransfer
        {
            get
            {
                //Type of pokemons not to transfer
                _pokemonsNotToTransfer = _pokemonsNotToTransfer ?? LoadPokemonList("PokemonsNotToTransfer.ini", new List<PokemonId> {
                    PokemonId.Dragonite, PokemonId.Charizard, PokemonId.Zapdos, PokemonId.Snorlax, PokemonId.Alakazam, PokemonId.Mew, PokemonId.Mewtwo
                });
                return _pokemonsNotToTransfer;
            }
        }

        public ICollection<PokemonId> PokemonsNotToCatch
        {
            get
            {
                //Type of pokemons not to catch
                _pokemonsNotToCatch = _pokemonsNotToCatch ?? LoadPokemonList("PokemonsNotToCatch.ini", new List<PokemonId> {
                    PokemonId.Zubat, PokemonId.Pidgey, PokemonId.Rattata
                });
                return _pokemonsNotToCatch;
            }
        }

        public ICollection<PokemonMoveDetail> PokemonMoveDetails
        {
            get
            {
                _pokemonMoveDetails = _pokemonMoveDetails ?? LoadPokemonMoveDetails();
                return _pokemonMoveDetails;
            }
        }

        #endregion
        #region " Constructors "

        public Settings()
        {
            this.AuthType = (AuthType)Enum.Parse(typeof(AuthType), UserSettings.Default.AuthType, true);
            this.CatchPokemon = UserSettings.Default.CatchPokemon;
            this.CatchWhileFlying = UserSettings.Default.CatchWhileFlying;
            this.CurrentAltitude = UserSettings.Default.DefaultAltitude;
            this.CurrentLatitude = UserSettings.Default.DefaultLatitude;
            this.CurrentLongitude = UserSettings.Default.DefaultLongitude;
            this.DestinationIndex = UserSettings.Default.DestinationIndex;
            this.DestinationsEnabled = UserSettings.Default.DestinationsEnabled;
            this.DisplayAllPokemonInLog = UserSettings.Default.DisplayAllPokemonInLog;
            this.DisplayAggregateLog = UserSettings.Default.DisplayAggregateLog;
            this.DisplayRefreshMinutes = UserSettings.Default.DisplayRefreshMinutes;
            this.EnableSpeedAdjustment = UserSettings.Default.EnableSpeedAdjustment;
            this.EnableSpeedRandomizer = UserSettings.Default.EnableSpeedRandomizer;
            this.EvolveOnlyPokemonAboveIV = UserSettings.Default.EvolveOnlyPokemonAboveIV;
            this.EvolveOnlyPokemonAboveIVValue = UserSettings.Default.EvolveOnlyPokemonAboveIVValue;
            this.EvolvePokemon = UserSettings.Default.EvolvePokemon;
            this.FlyingEnabled = UserSettings.Default.FlyingEnabled;
            this.FlyingSpeed = UserSettings.Default.FlyingSpeed;
            this.GPXFile = UserSettings.Default.GPXFile;
            this.KeepAboveCP = UserSettings.Default.KeepAboveCP;
            this.KeepAboveIV = UserSettings.Default.KeepAboveIV;
            this.KeepAboveV = UserSettings.Default.KeepAboveV;
            this.LoiteringActive = UserSettings.Default.LoiteringActive;
            this.MaxSecondsBetweenStops = UserSettings.Default.MaxSecondsBetweenStops;
            this.MaxDistance = UserSettings.Default.MaxDistance;
            this.MinutesPerDestination = UserSettings.Default.MinutesPerDestination;
            this.MoveWhenNoStops = UserSettings.Default.MoveWhenNoStops;
            this.NotTransferPokemonsThatCanEvolve = UserSettings.Default.NotTransferPokemonsThatCanEvolve;
            this.PrioritizeStopsWithLures = UserSettings.Default.PrioritizeStopsWithLures;
            this.PriorityType = (PriorityType)Enum.Parse(typeof(PriorityType), UserSettings.Default.PriorityType, true);
            this.Password = UserSettings.Default.Password;
            this.Username = UserSettings.Default.Username;
            this.TransferPokemon = UserSettings.Default.TransferPokemon;
            this.KeepDuplicateAmount = UserSettings.Default.KeepDuplicateAmount;
            this.UseGPXPathing = UserSettings.Default.UseGPXPathing;
            this.UseLuckyEggs = UserSettings.Default.UseLuckyEggs;
            this.UsePokemonToNotCatchList = UserSettings.Default.UsePokemonToNotCatchList;
            this.MinSpeed = UserSettings.Default.MinSpeed;
            this.MaxSpeed = UserSettings.Default.MaxSpeed;
            this.TransferBelowCP = UserSettings.Default.TransferBelowCP;
            this.TransferBelowIV = UserSettings.Default.TransferBelowIV;
            this.TransferBelowV = UserSettings.Default.TransferBelowV;

        }

        #endregion
        #region " Methods "

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
            //check for base path
            if (!Directory.Exists(configs_path))
                Directory.CreateDirectory(configs_path);

            string fileName = "Settings.xml";
            string filePath = Path.Combine(configs_path, fileName);
            if (!File.Exists(filePath))
            {
                var result = PromptForCredentials();
                Logger.Write($"File: \"\\Configs\\{fileName}\" not found, creating new...", LogLevel.Warning);
                Save();
            }
            else
            {
                //Logger.Write($"Loading File: \"\\Configs\\{fileName}\"", LogLevel.Info);

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
                    this.CurrentAltitude = obj.CurrentAltitude;
                    this.CurrentLatitude = obj.CurrentLatitude;
                    this.CurrentLongitude = obj.CurrentLongitude;
                    this.DestinationIndex = obj.DestinationIndex;
                    this.DestinationsEnabled = obj.DestinationsEnabled;
                    this.DisplayAllPokemonInLog = obj.DisplayAllPokemonInLog;
                    this.DisplayAggregateLog = obj.DisplayAggregateLog;
                    this.DisplayRefreshMinutes = obj.DisplayRefreshMinutes;
                    this.EnableSpeedAdjustment = obj.EnableSpeedAdjustment;
                    this.EnableSpeedRandomizer = obj.EnableSpeedRandomizer;
                    this.EvolveOnlyPokemonAboveIV = obj.EvolveOnlyPokemonAboveIV;
                    this.EvolveOnlyPokemonAboveIVValue = obj.EvolveOnlyPokemonAboveIVValue;
                    this.EvolvePokemon = obj.EvolvePokemon;
                    this.FlyingEnabled = obj.FlyingEnabled;
                    this.FlyingSpeed = obj.FlyingSpeed;
                    this.GPXFile = obj.GPXFile;
                    this.KeepAboveCP = obj.KeepAboveCP;
                    this.KeepAboveIV = obj.KeepAboveIV;
                    this.KeepAboveV = obj.KeepAboveV;
                    this.LoiteringActive = obj.LoiteringActive;
                    this.MaxSecondsBetweenStops = obj.MaxSecondsBetweenStops;
                    this.MaxDistance = obj.MaxDistance;
                    this.MinutesPerDestination = obj.MinutesPerDestination;
                    this.MoveWhenNoStops = obj.MoveWhenNoStops;
                    this.NotTransferPokemonsThatCanEvolve = obj.NotTransferPokemonsThatCanEvolve;
                    this.PrioritizeStopsWithLures = obj.PrioritizeStopsWithLures;
                    this.PriorityType = obj.PriorityType;
                    this.Password = obj.Password;
                    this.Username = obj.Username;
                    this.TransferPokemon = obj.TransferPokemon;
                    this.KeepDuplicateAmount = obj.KeepDuplicateAmount;
                    this.UseGPXPathing = obj.UseGPXPathing;
                    this.UseLuckyEggs = obj.UseLuckyEggs;
                    this.UsePokemonToNotCatchList = obj.UsePokemonToNotCatchList;
                    this.MinSpeed = obj.MinSpeed;
                    this.MaxSpeed = obj.MaxSpeed;
                    this.TransferBelowCP = obj.TransferBelowCP;
                    this.TransferBelowIV = obj.TransferBelowIV;
                    this.TransferBelowV = obj.TransferBelowV;
                }
            }
        }

        public ICollection<KeyValuePair<ItemId, int>> LoadItemList(string filename, List<KeyValuePair<ItemId, int>> defaultItems)
        {
            ICollection<KeyValuePair<ItemId, int>> result = new List<KeyValuePair<ItemId, int>>();

            DirectoryInfo di = Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\Configs");

            if (File.Exists(Directory.GetCurrentDirectory() + "\\" + filename))
            {
                //Logger.Write($"Loading File: {filename}");

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

        private ICollection<PokemonId> LoadPokemonList(string filename, List<PokemonId> defaultPokemon)
        {
            ICollection<PokemonId> result = new List<PokemonId>();
            if (!Directory.Exists(configs_path))
                Directory.CreateDirectory(configs_path);
            string pokemonlist_file = Path.Combine(configs_path, filename);
            if (!File.Exists(pokemonlist_file))
            {
                //Logger.Write($"File: \"\\Configs\\{filename}\" not found, creating new...", LogLevel.Warning);
                using (var w = File.AppendText(pokemonlist_file))
                {
                    defaultPokemon.ForEach(pokemon => w.WriteLine(pokemon.ToString()));
                    defaultPokemon.ForEach(pokemon => result.Add((PokemonId)pokemon));
                    w.Close();
                }
            }
            if (File.Exists(pokemonlist_file))
            {
                //Logger.Write($"Loading File: \"\\Configs\\{filename}\"", LogLevel.Info);

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

        private ICollection<PokemonMoveDetail> LoadPokemonMoveDetails()
        {
            if (!Directory.Exists(configs_path))
                Directory.CreateDirectory(configs_path);

            string fileName = "PokemonMoveDetails.xml";
            string filePath = Path.Combine(configs_path, fileName);
            //Logger.Write($"Loading File: \"\\Configs\\{fileName}\"", LogLevel.Info);

            var content = string.Empty;
            lock (syncRoot)
            {
                using (FileStream s = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var x = new System.Xml.Serialization.XmlSerializer(typeof(List<PokemonMoveDetail>));
                    var list = (List<PokemonMoveDetail>)x.Deserialize(s);
                    s.Close();
                    return list;
                }
            }
        }

        public void SetDefaultLocation(double lat, double lng, double z)
        {
            CurrentLatitude = lat;
            CurrentLongitude = lng;
            CurrentAltitude = z;
            Save();
        }

        public bool PromptForCredentials()
        {
            var d = new UsernamePasswordForm();
            var result = d.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                Logger.Write($"User quit before providing credentials.", LogLevel.Warning);
                Program.ExitApplication(1);
            }

            this.Username = d.Username;
            this.Password = d.Password;
            this.AuthType = (AuthType)Enum.Parse(typeof(AuthType), d.AuthType, true);

            d.Dispose();
            d = null;

            Save();

            return result == System.Windows.Forms.DialogResult.OK;
        }
        #endregion

    }
}
