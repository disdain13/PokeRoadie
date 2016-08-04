#region " Imports "

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using System.Threading.Tasks;

using PokemonGo.RocketAPI;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Logging;

using POGOProtos.Inventory.Item;
using POGOProtos.Enums;

using PokeRoadie.Forms;

#endregion

namespace PokeRoadie
{
    public class PokeRoadieSettings : PokemonGo.RocketAPI.ISettings
    {

        #region " Singleton "

        private static PokeRoadieSettings _current = null;
        public static PokeRoadieSettings Current { get{ _current = _current ?? (new PokeRoadieSettings()).Load(); return _current; }}
        
        #endregion
        #region " Members "

        private static object syncRoot => new object();
        private static string configs_path = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
        private ICollection<PokemonId> _pokemonsNotToTransfer;
        private ICollection<PokemonId> _pokemonsToEvolve;
        private ICollection<PokemonId> _pokemonsNotToCatch;
        private ICollection<PokemonId> _pokemonsToPowerUp;
        private IList<LocationData> _destinations;
        private ICollection<KeyValuePair<ItemId, int>> _itemRecycleFilter;
        private ICollection<MoveData> _pokemonMoveDetails;
        private static string destinationcoords_file = Path.Combine(configs_path, "DestinationCoords.ini");

        #endregion
        #region " General Properties "

        //auth related
        public virtual AuthType AuthType { get; set; }
        public virtual string Username { get; set; }
        public virtual string Password { get; set; }
        public virtual string GoogleRefreshToken { get; set; }

        //destinations/travel
        public virtual bool DestinationsEnabled { get; set; }
        public virtual int MinutesPerDestination { get; set; }
        public virtual int DestinationIndex { get; set; }

        //location related
        public virtual double CurrentLatitude { get; set; }
        public virtual double CurrentLongitude { get; set; }
        public virtual double CurrentAltitude { get; set; }

        //movement related
        public virtual double MinSpeed { get; set; }
        public virtual int MaxSpeed { get; set; }
        public virtual int MaxDistance { get; set; }
        public virtual bool EnableSpeedAdjustment { get; set; }
        public virtual bool EnableSpeedRandomizer { get; set; }
        public virtual int MaxSecondsBetweenStops { get; set; }
        public virtual bool FlyingEnabled { get; set; }
        public virtual bool FlyLikeCaptKirk { get; set; }
        public virtual int FlyingSpeed { get; set; }
        public virtual bool PingStopsWhileFlying { get; set; }
        public virtual bool UseGPXPathing { get; set; }
        public virtual string GPXFile { get; set; }

        //evolution
        public virtual bool EvolvePokemon { get; set; }
        public PriorityTypes EvolvePriorityType { get; set; }
        public virtual double EvolveAboveIV { get; set; }
        public virtual double EvolveAboveV { get; set; }
        public virtual int EvolveAboveCp { get; set; }
        public virtual bool UsePokemonsToEvolveList { get; set; }

        //transfers
        public virtual bool TransferPokemon { get; set; }
        public virtual PriorityTypes TransferPriorityType { get; set; }
        public virtual int KeepDuplicateAmount { get; set; }
        public virtual double KeepAboveIV { get; set; }
        public virtual double KeepAboveV { get; set; }
        public virtual int KeepAboveCp { get; set; }
        public virtual double TransferBelowIV { get; set; }
        public virtual double TransferBelowV { get; set; }
        public virtual int TransferBelowCp { get; set; }
        public virtual int TransferTrimFatCount { get; set; }
        public virtual bool NotTransferPokemonsThatCanEvolve { get; set; }

        //power-ups
        public virtual bool PowerUpPokemon { get; set; }
        public virtual PriorityTypes PowerUpPriorityType { get; set; }
        public virtual double PowerUpAboveIV { get; set; }
        public virtual double PowerUpAboveV { get; set; }
        public virtual int PowerUpAboveCp { get; set; }
        public virtual int MinStarDustForPowerUps { get; set; }
        public virtual bool UsePokemonsToPowerUpList { get; set; }
        public virtual int MinCandyForPowerUps { get; set; }
        public virtual int MaxPowerUpsPerRound { get; set; }

        //player behavior
        public virtual bool CatchPokemon { get; set; }
        public virtual bool VisitPokestops { get; set; }
        public virtual bool MoveWhenNoStops { get; set; }
        public virtual bool PrioritizeStopsWithLures { get; set; }
        public virtual bool LoiteringActive { get; set; }
        public virtual bool VisitGyms { get; set; }
        public virtual bool AutoDeployAtTeamGyms { get; set; }
        public virtual bool PokeBallBalancing { get; set; }

        //inventory
        public virtual bool UseLuckyEggs { get; set; }
        public virtual bool UseIncense { get; set; }
        public virtual bool UseRevives { get; set; }
        public virtual bool UsePotions { get; set; }
        public virtual bool UseEggIncubators { get; set; }

        //config options
        public virtual bool UsePokemonToNotCatchList { get; set; }

        //logging
        public virtual int DisplayRefreshMinutes { get; set; }
        public virtual bool DisplayAggregateLog { get; set; }
        public virtual bool DisplayAllPokemonInLog { get; set; }

        //system
        public virtual bool WaitOnStart { get; set; }
        public virtual double WaypointLatitude { get; set; }
        public virtual double WaypointLongitude { get; set; }
        public virtual double WaypointAltitude { get; set; }

        [XmlIgnore()]
        public DateTime? DestinationEndDate { get; set; }

        #endregion
        #region " Collection Properties "

        [XmlIgnore()]
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

        [XmlIgnore()]
        public IList<LocationData> Destinations
        {
            get
            {
                //Type of pokemons to evolve
                _destinations = _destinations ?? LoadDestinations();
                return _destinations;
            }
        }

        [XmlIgnore()]
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

        [XmlIgnore()]
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

        [XmlIgnore()]
        public ICollection<PokemonId> PokemonsToPowerUp
        {
            get
            {
                //Type of pokemons not to transfer
                _pokemonsToPowerUp = _pokemonsToPowerUp ?? LoadPokemonList("PokemonsToPowerUp.ini", new List<PokemonId> {
                    PokemonId.Dragonite, PokemonId.Charizard, PokemonId.Zapdos, PokemonId.Snorlax, PokemonId.Alakazam, PokemonId.Mew, PokemonId.Mewtwo
                });
                return _pokemonsToPowerUp;
            }
        }

        [XmlIgnore()]
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

        [XmlIgnore()]
        public ICollection<MoveData> PokemonMoves
        {
            get
            {
                _pokemonMoveDetails = _pokemonMoveDetails ?? LoadPokemonMoveDetails();
                return _pokemonMoveDetails;
            }
        }

        #endregion
        #region " ISettings Implementations "

        double ISettings.DefaultAltitude
        {
            get
            {
                return CurrentAltitude;
            }

            set
            {
                CurrentAltitude = value;
            }
        }

        double ISettings.DefaultLatitude
        {
            get
            {
                return CurrentLatitude;
            }

            set
            {
                CurrentLatitude = value;
            }
        }

        double ISettings.DefaultLongitude
        {
            get
            {
                return CurrentLongitude; 
            }

            set
            {
                CurrentLongitude = value;
            }
        }

        string ISettings.GooglePassword
        {
            get
            {
                return Password;
            }

            set
            {
                Password = value;
            }
        }

        string ISettings.GoogleUsername
        {
            get
            {
            return Username;
            }

            set
            {
                Username = value;
            }
        }

        string ISettings.PtcPassword
        {
            get
            {
                return Password;
            }

            set
            {
                Password = value;
            }
        }

        string ISettings.PtcUsername
        {
            get
            {
                return Username;
            }

            set
            {
                Username = value;
            }
        }

        #endregion
        #region " Constructors "

        public PokeRoadieSettings()
        {
            
            AuthType parserValue = AuthType.Google;
            if (Enum.TryParse<AuthType>(UserSettings.Default.AuthType, true, out parserValue))
                this.AuthType = parserValue;
            this.CatchPokemon = UserSettings.Default.CatchPokemon;
            this.PingStopsWhileFlying = UserSettings.Default.PingStopsWhileFlying;
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
            //this.EvolveOnlyPokemonAboveIV = UserSettings.Default.EvolveOnlyPokemonAboveIV;
            //this.EvolveOnlyPokemonAboveIVValue = UserSettings.Default.EvolveOnlyPokemonAboveIVValue;
            this.EvolvePokemon = UserSettings.Default.EvolvePokemon;
            this.FlyingEnabled = UserSettings.Default.FlyingEnabled;
            this.FlyingSpeed = UserSettings.Default.FlyingSpeed;
            this.GPXFile = UserSettings.Default.GPXFile;
            this.KeepAboveCp = UserSettings.Default.KeepAboveCP;
            this.KeepAboveIV = UserSettings.Default.KeepAboveIV;
            this.KeepAboveV = UserSettings.Default.KeepAboveV;
            this.LoiteringActive = UserSettings.Default.LoiteringActive;
            this.MaxSecondsBetweenStops = UserSettings.Default.MaxSecondsBetweenStops;
            this.MaxDistance = UserSettings.Default.MaxDistance;
            this.MinutesPerDestination = UserSettings.Default.MinutesPerDestination;
            this.MoveWhenNoStops = UserSettings.Default.MoveWhenNoStops;
            this.NotTransferPokemonsThatCanEvolve = UserSettings.Default.NotTransferPokemonsThatCanEvolve;
            this.PrioritizeStopsWithLures = UserSettings.Default.PrioritizeStopsWithLures;
            PriorityTypes outValue = PriorityTypes.V;
            if (Enum.TryParse<PriorityTypes>(UserSettings.Default.TransferPriorityType, true, out outValue))
                this.TransferPriorityType = outValue;
            this.Password = UserSettings.Default.Password;
            this.Username = UserSettings.Default.Username;
            this.TransferPokemon = UserSettings.Default.TransferPokemon;
            this.KeepDuplicateAmount = UserSettings.Default.KeepDuplicateAmount;
            this.UseGPXPathing = UserSettings.Default.UseGPXPathing;
            this.UseLuckyEggs = UserSettings.Default.UseLuckyEggs;
            this.UsePokemonToNotCatchList = UserSettings.Default.UsePokemonToNotCatchList;
            this.MinSpeed = UserSettings.Default.MinSpeed;
            this.MaxSpeed = UserSettings.Default.MaxSpeed;
            this.TransferBelowCp = UserSettings.Default.TransferBelowCP;
            this.TransferBelowIV = UserSettings.Default.TransferBelowIV;
            this.TransferBelowV = UserSettings.Default.TransferBelowV;
            this.AutoDeployAtTeamGyms = UserSettings.Default.AutoDeployAtTeamGyms;
            this.VisitGyms = UserSettings.Default.VisitGyms;
            this.VisitPokestops = UserSettings.Default.VisitPokestops;


            PriorityTypes outValue2 = PriorityTypes.V;
            if (Enum.TryParse<PriorityTypes>(UserSettings.Default.EvolvePriorityType, true, out outValue2))
                this.EvolvePriorityType = outValue;
            this.EvolveAboveCp = UserSettings.Default.EvolveAboveCp;
            this.EvolveAboveIV = UserSettings.Default.EvolveAboveIV;
            this.EvolveAboveV = UserSettings.Default.EvolveAboveV;
            this.UsePokemonsToEvolveList = UserSettings.Default.UsePokemonsToEvolveList;
            this.UseIncense = UserSettings.Default.UseIncense;
            this.UseRevives = UserSettings.Default.UseRevives;
            this.UsePotions = UserSettings.Default.UsePotions;

            this.WaypointLatitude = UserSettings.Default.WaypointLatitude;
            this.WaypointLongitude = UserSettings.Default.WaypointLongitude;
            this.WaypointAltitude = UserSettings.Default.WaypointAltitude;
            this.FlyLikeCaptKirk = UserSettings.Default.FlyLikeCaptKirk;
            this.TransferTrimFatCount = UserSettings.Default.TransferTrimFatCount;
            this.PokeBallBalancing = UserSettings.Default.PokeBallBalancing;

            this.WaitOnStart = UserSettings.Default.WaitOnStart;
            this.PowerUpPokemon = UserSettings.Default.PowerUpPokemon;
            PriorityTypes outValue3 = PriorityTypes.V;
            if (Enum.TryParse<PriorityTypes>(UserSettings.Default.PowerUpPriorityType, true, out outValue3))
                this.PowerUpPriorityType = outValue;
            this.PowerUpAboveCp = UserSettings.Default.PowerUpAboveCp;
            this.PowerUpAboveIV = UserSettings.Default.PowerUpAboveIV;
            this.PowerUpAboveV = UserSettings.Default.PowerUpAboveV;
            this.MinStarDustForPowerUps = UserSettings.Default.MinStarDustForPowerUps;
            this.UsePokemonsToPowerUpList = UserSettings.Default.UsePokemonsToPowerUpList;
            this.MinCandyForPowerUps = UserSettings.Default.MinCandyForPowerUps;
            this.MaxPowerUpsPerRound = UserSettings.Default.MaxPowerUpsPerRound;
            this.UseEggIncubators = UserSettings.Default.UseEggIncubators;

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
                    if (File.Exists(filePath)) File.Delete(filePath);
                    using (FileStream s = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        var x = new System.Xml.Serialization.XmlSerializer(typeof(PokeRoadieSettings));
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

        private PokeRoadieSettings Load()
        {
            //check for base path
            if (!Directory.Exists(configs_path))
                Directory.CreateDirectory(configs_path);

            string fileName = "Settings.xml";
            string filePath = Path.Combine(configs_path, fileName);
            bool createNew = false;

            if (File.Exists(filePath))
            {
                PokeRoadieSettings obj = null;
                try
                {
                    lock (syncRoot)
                    {
                        using (FileStream s = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            var x = new System.Xml.Serialization.XmlSerializer(typeof(PokeRoadieSettings));
                            obj = (PokeRoadieSettings)x.Deserialize(s);
                            s.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write($"The {fileName} file could not be loaded, and will be recreated. {ex.Message} {ex.ToString()}", LogLevel.Error);
                    createNew = true;
                }

                if (obj != null)
                {
                    this.AuthType = obj.AuthType;
                    this.CatchPokemon = obj.CatchPokemon;
                    this.PingStopsWhileFlying = obj.PingStopsWhileFlying;
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
                    //this.EvolveOnlyPokemonAboveIV = obj.EvolveOnlyPokemonAboveIV;
                    //this.EvolveOnlyPokemonAboveIVValue = obj.EvolveOnlyPokemonAboveIVValue;
                    this.EvolvePokemon = obj.EvolvePokemon;
                    this.FlyingEnabled = obj.FlyingEnabled;
                    this.FlyingSpeed = obj.FlyingSpeed;
                    this.GPXFile = obj.GPXFile;
                    this.KeepAboveCp = obj.KeepAboveCp;
                    this.KeepAboveIV = obj.KeepAboveIV;
                    this.KeepAboveV = obj.KeepAboveV;
                    this.LoiteringActive = obj.LoiteringActive;
                    this.MaxSecondsBetweenStops = obj.MaxSecondsBetweenStops;
                    this.MaxDistance = obj.MaxDistance;
                    this.MinutesPerDestination = obj.MinutesPerDestination;
                    this.MoveWhenNoStops = obj.MoveWhenNoStops;
                    this.NotTransferPokemonsThatCanEvolve = obj.NotTransferPokemonsThatCanEvolve;
                    this.PrioritizeStopsWithLures = obj.PrioritizeStopsWithLures;
                    this.TransferPriorityType = obj.TransferPriorityType;
                    this.Password = obj.Password;
                    this.Username = obj.Username;
                    this.TransferPokemon = obj.TransferPokemon;
                    this.KeepDuplicateAmount = obj.KeepDuplicateAmount;
                    this.UseGPXPathing = obj.UseGPXPathing;
                    this.UseLuckyEggs = obj.UseLuckyEggs;
                    this.UsePokemonToNotCatchList = obj.UsePokemonToNotCatchList;
                    this.MinSpeed = obj.MinSpeed;
                    this.MaxSpeed = obj.MaxSpeed;
                    this.TransferBelowCp = obj.TransferBelowCp;
                    this.TransferBelowIV = obj.TransferBelowIV;
                    this.TransferBelowV = obj.TransferBelowV;
                    this.AutoDeployAtTeamGyms = obj.AutoDeployAtTeamGyms;
                    this.VisitGyms = obj.VisitGyms;
                    this.VisitPokestops = obj.VisitPokestops;

                    this.EvolvePriorityType = obj.EvolvePriorityType;
                    this.EvolveAboveCp = obj.EvolveAboveCp;
                    this.EvolveAboveIV = obj.EvolveAboveIV;
                    this.EvolveAboveV = obj.EvolveAboveV;
                    this.UsePokemonsToEvolveList = obj.UsePokemonsToEvolveList;
                    this.UseIncense = obj.UseIncense;
                    this.UseRevives = obj.UseRevives;
                    this.UsePotions = obj.UsePotions;
                    this.WaypointLatitude = obj.WaypointLatitude;
                    this.WaypointLongitude = obj.WaypointLongitude;
                    this.WaypointAltitude = obj.WaypointAltitude;
                    this.FlyLikeCaptKirk = obj.FlyLikeCaptKirk;
                    this.TransferTrimFatCount = obj.TransferTrimFatCount;
                    this.PokeBallBalancing = obj.PokeBallBalancing;

                    this.WaitOnStart = obj.WaitOnStart;
                    this.WaitOnStart = obj.WaitOnStart;
                    this.PowerUpPokemon = obj.PowerUpPokemon;
                    this.PowerUpPriorityType = obj.PowerUpPriorityType;
                    this.PowerUpAboveCp = obj.PowerUpAboveCp;
                    this.PowerUpAboveIV = obj.PowerUpAboveIV;
                    this.PowerUpAboveV = obj.PowerUpAboveV;
                    this.MinStarDustForPowerUps = obj.MinStarDustForPowerUps;
                    this.UsePokemonsToPowerUpList = obj.UsePokemonsToPowerUpList;
                    this.MinCandyForPowerUps = obj.MinCandyForPowerUps;
                    this.MaxPowerUpsPerRound = obj.MaxPowerUpsPerRound;
                    this.UseEggIncubators = obj.UseEggIncubators;

                }
                if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
                {
                    createNew = true;
                }
            }
            else
            {
                createNew = true;
            }


            //resolve unknown location
            if (CurrentLongitude == 0 && CurrentLatitude == 0 && CurrentAltitude == 0) 
            {
                if (DestinationsEnabled && Destinations.Any())
                {
                    var index = DestinationIndex < Destinations.Count ? DestinationIndex : 0;
                    var destination = Destinations[index];
                    CurrentLatitude = destination.Latitude;
                    CurrentLongitude = destination.Longitude;
                    CurrentAltitude = destination.Altitude;
                }
                else if (WaypointLatitude != 0 && WaypointLongitude != 0 && WaypointAltitude != 0)
                {
                    CurrentLatitude = WaypointLatitude;
                    CurrentLongitude = WaypointLongitude;
                    CurrentAltitude = WaypointAltitude;
                }
                else
                {
                    CurrentLatitude = UserSettings.Default.DefaultLatitude;
                    CurrentLongitude = UserSettings.Default.DefaultLongitude;
                    CurrentAltitude = UserSettings.Default.DefaultAltitude;
                }
            }

            //resolve unknown waypoint
            if (WaypointLatitude == 0 && WaypointLongitude == 0 && WaypointAltitude == 0)
            {
                if (DestinationsEnabled && Destinations.Any())
                {
                    var index = DestinationIndex < Destinations.Count ? DestinationIndex : 0;
                    var destination = Destinations[index];
                    WaypointLatitude = destination.Latitude;
                    WaypointLongitude = destination.Longitude;
                    WaypointAltitude = destination.Altitude;
                }
                else if (CurrentLatitude != 0 && CurrentLongitude != 0 && CurrentAltitude != 0)
                {
                    WaypointLatitude = CurrentLatitude;
                    WaypointLongitude = CurrentLongitude;
                    WaypointAltitude = CurrentAltitude;
                }
                else
                {
                    WaypointLatitude = UserSettings.Default.DefaultLatitude;
                    WaypointLongitude = UserSettings.Default.DefaultLongitude;
                    WaypointAltitude = UserSettings.Default.DefaultAltitude;
                }
            }

            if (createNew)
            {
                Logger.Write($"The {fileName} file could not be found, it will be recreated.", LogLevel.Warning);
                var result = PromptForCredentials();
                if (!result)
                {
                    Logger.Write($"Quit before providing login credentials.", LogLevel.Warning);
                    Program.ExitApplication(1);
                }
            }
            else
            {
                Save();
            }

            return this;
        }

        private ICollection<KeyValuePair<ItemId, int>> LoadItemList(string filename, List<KeyValuePair<ItemId, int>> defaultItems)
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

        private ICollection<MoveData> LoadPokemonMoveDetails()
        {
            if (!Directory.Exists(configs_path))
                Directory.CreateDirectory(configs_path);

            string fileName = "PokemonMoveDetails.xml";
            string filePath = Path.Combine(configs_path, fileName);
            //Logger.Write($"Loading File: \"\\Configs\\{fileName}\"", LogLevel.Info);
            List<MoveData> list = new List<MoveData>();
            var content = string.Empty;
            try
            {
                lock (syncRoot)
                {
                    using (FileStream s = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        var x = new System.Xml.Serialization.XmlSerializer(typeof(List<MoveData>));
                        list = (List<MoveData>)x.Deserialize(s);
                        s.Close();
                        return list;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"The {fileName} file could not be loaded. True Value calculations will not be accurate. Please download a copy from the website https://github.com/disdain13/PokeRoadie : {ex.Message} {ex.ToString()}", LogLevel.Error);
            }

            return list;
        }

        /// <summary>
        /// Gets a list of target destinations.
        /// </summary>
        /// <returns>list of target destinations</returns>
        private IList<LocationData> LoadDestinations()
        {
            var list = new List<LocationData>();
            if (!Directory.Exists(configs_path))
                Directory.CreateDirectory(configs_path);
            if (File.Exists(destinationcoords_file))
            {
                using (StreamReader r = new StreamReader(destinationcoords_file))
                {
                    var line = r.ReadLine();
                    while (line != null)
                    {
                        if (line.Contains(":"))
                        {
                            var latlng = line.Split(':');


                            if (latlng != null && latlng.Length > 2 && latlng[0].Length > 0 && latlng[1].Length > 0 && latlng[2].Length > 0)
                            {
                                try
                                {
                                    double temp_lat = Convert.ToDouble(latlng[0]);
                                    double temp_long = Convert.ToDouble(latlng[1]);
                                    double temp_alt = Convert.ToDouble(latlng[2]);
                                    if (temp_lat >= -90 && temp_lat <= 90 && temp_long >= -180 && temp_long <= 180)
                                    {
                                        //SetCoordinates(Convert.ToDouble(latlng[0]), Convert.ToDouble(latlng[1]), Settings.DefaultAltitude);
                                        var newDestination = new LocationData();
                                        newDestination.Latitude = temp_lat;
                                        newDestination.Longitude = temp_long;
                                        newDestination.Altitude = temp_alt;
                                        if (latlng.Length > 3)
                                        {
                                            newDestination.Name = latlng[3];
                                        }
                                        else
                                        {
                                            newDestination.Name = "Destination " + (list.Count + 1).ToString();
                                        }
                                        list.Add(newDestination);
                                    }
                                    else
                                    {

                                    }
                                }
                                catch (FormatException)
                                {
                                    Logger.Write("Destinations in \"\\Configs\\DestinationCoords.ini\" file is invalid. Destinations will not be used.", LogLevel.Error);
                                    return null;
                                }
                            }
                            else
                            {
                                Logger.Write("Destinations in \"\\Configs\\DestinationCoords.ini\" file is invalid. 1 line per destination, formatted like - LAT:LONG:ALT:NAME", LogLevel.Error);
                                return null;
                            }

                        }
                        line = r.ReadLine();
                    }
                    r.Close();
                }
            }
            else
            {
                if (CurrentLatitude != 0 && CurrentLongitude != 0)
                {

                    using (StreamWriter w = File.CreateText(destinationcoords_file))
                    {
                        w.Write($"{CurrentLatitude}:{CurrentLongitude}:{CurrentAltitude}:Default Location");
                        w.Close();
                    }

                    var d = new LocationData();
                    d.Latitude = CurrentLatitude;
                    d.Longitude = CurrentLongitude;
                    d.Altitude = CurrentAltitude;
                    d.Name = "Default Location";
                    list.Add(d);
                }
            }
            return list;
        }

        public bool PromptForCredentials()
        {
            var d = new UsernamePasswordForm();
            var result = d.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                this.Username = d.Username;
                this.Password = d.Password;
                AuthType parserValue = AuthType.Google;
                if (Enum.TryParse<AuthType>(d.AuthType, true, out parserValue))
                    this.AuthType = parserValue;
                this.Save();
            }
            d.Dispose();
            d = null;

            return result == System.Windows.Forms.DialogResult.OK;
        }

        #endregion

    }
}
