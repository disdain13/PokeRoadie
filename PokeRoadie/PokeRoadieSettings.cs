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

        private static object syncRoot = new object();
        private static object sessionRoot = new object();
        private static string configs_path = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
        private static string temp_path = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
        private ICollection<PokemonId> _pokemonsNotToTransfer;
        private ICollection<PokemonId> _pokemonsToEvolve;
        private ICollection<PokemonId> _pokemonsNotToCatch;
        private ICollection<PokemonId> _pokemonsToPowerUp;
        private IList<LocationData> _destinations;
        private ICollection<KeyValuePair<ItemId, int>> _itemRecycleFilter;
        private ICollection<MoveData> _pokemonMoveDetails;
        private static string destinationcoords_file = Path.Combine(configs_path, "DestinationCoords.ini");
        private SessionData _session = null;

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
        public virtual int MaxDistanceForLongTravel { get; set; }
        public virtual bool EnableSpeedAdjustment { get; set; }
        public virtual bool EnableSpeedRandomizer { get; set; }
        public virtual int MaxSecondsBetweenStops { get; set; }
        public virtual int MaxLocationAttempts { get; set; }
        public virtual int LongDistanceSpeed { get; set; }
        public virtual int SpeedCurveDistance { get; set; }
        public virtual bool UseGPXPathing { get; set; }
        public virtual string GPXFile { get; set; }
        public virtual bool EnableWandering { get; set; }

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


        //favorites
        public virtual bool FavoritePokemon { get; set; }
        public virtual int FavoriteAboveCp { get; set; }
        public virtual double FavoriteAboveIV { get; set; }
        public virtual double FavoriteAboveV { get; set; }

        //player behavior
        public virtual bool CatchPokemon { get; set; }
        public virtual double MaxCatchSpeed { get; set; }
        public virtual bool VisitPokestops { get; set; }
        public virtual bool MoveWhenNoStops { get; set; }
        public virtual bool PrioritizeStopsWithLures { get; set; }
        public virtual bool LoiteringActive { get; set; }


        public virtual bool VisitGyms { get; set; }
        public virtual bool AutoDeployAtTeamGyms { get; set; }
        public virtual bool PickupDailyDefenderBonuses { get; set; }
        public virtual int MinGymsBeforeBonusPickup { get; set; }
        public virtual bool PokeBallBalancing { get; set; }

        //humanized throws
        public virtual bool EnableHumanizedThrows { get; set; }
        public virtual int ForceExcellentThrowOverCp { get; set; }
        public virtual double ForceExcellentThrowOverIV { get; set; }
        public virtual double ForceExcellentThrowOverV { get; set; }
        public virtual int ForceGreatThrowOverCp { get; set; }
        public virtual double ForceGreatThrowOverIV { get; set; }
        public virtual double ForceGreatThrowOverV { get; set; }
        public virtual int ExcellentThrowChance { get; set; }
        public virtual int GreatThrowChance { get; set; }
        public virtual int NiceThrowChance { get; set; }
        public virtual double CurveThrowChance { get; set; }

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

        //proxy
        public virtual bool UseProxy { get; set; }
        public virtual string UseProxyHost { get; set; }
        public virtual int UseProxyPort { get; set; }
        public virtual bool UseProxyAuthentication { get; set; }
        public virtual string UseProxyUsername { get; set; }
        public virtual string UseProxyPassword { get; set; }
        
        //rename
        public virtual bool RenamePokemon { get; set; }
        public virtual string RenameFormat { get; set; }

        //emulation & safety
        public virtual string DevicePackageName { get; set; }
        public virtual string DeviceId { get; set; }

        //session
        public virtual int DisplayPokemonCount { get; set; }
        public virtual int DisplayTopCandy { get; set; }
        public virtual string MaxRunTimespan { get; set; }
        public virtual string MinBreakTimespan { get; set; }
        public virtual int MaxPokemonCatches { get; set; }
        public virtual int MaxPokestopVisits { get; set; }

        //configurable delays
        public virtual int MinDelay { get; set; }
        public virtual int MaxDelay { get; set; }
        public virtual int EvolutionMinDelay { get; set; }
        public virtual int EvolutionMaxDelay { get; set; }
        public virtual int EggHatchMinDelay { get; set; }
        public virtual int EggHatchMaxDelay { get; set; }
        public virtual int TransferMinDelay { get; set; }
        public virtual int TransferMaxDelay { get; set; }
        public virtual int CatchMinDelay { get; set; }
        public virtual int CatchMaxDelay { get; set; }
        public virtual int RecycleMinDelay { get; set; }
        public virtual int RecycleMaxDelay { get; set; }
        public virtual int PowerUpMinDelay { get; set; }
        public virtual int PowerUpMaxDelay { get; set; }
        public virtual int PokedexEntryMinDelay { get; set; }
        public virtual int PokedexEntryMaxDelay { get; set; }
        public virtual int LocationsMinDelay { get; set; }
        public virtual int LocationsMaxDelay { get; set; }

        public virtual bool ShowDebugMessages { get; set; }
       
        //tutorials
        public virtual PokemonId TutorialPokmonId { get; set; }
        public virtual TeamColor TeamColor { get; set; }
        public virtual string TutorialCodename { get; set; }
        public virtual bool TutorialGenerateCodename { get; set; }
        public virtual bool IncludeHotPokestops { get; set; }

        [XmlIgnore()]
        public DateTime? DestinationEndDate { get; set; }

        [XmlIgnore()]
        public SessionData Session
        {
            get
            {
                if (_session != null) return _session;
                _session = LoadSession(Path.Combine(temp_path, "Session.xml"));
                return _session;
            }
            set
            {
                _session = value;
            }
        }

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
                    new KeyValuePair<ItemId, int>(ItemId.ItemPokeBall, 50),
                    new KeyValuePair<ItemId, int>(ItemId.ItemGreatBall, 75),
                    new KeyValuePair<ItemId, int>(ItemId.ItemUltraBall, 75),
                    new KeyValuePair<ItemId, int>(ItemId.ItemMasterBall, 100),

                    new KeyValuePair<ItemId, int>(ItemId.ItemPotion, 30),
                    new KeyValuePair<ItemId, int>(ItemId.ItemSuperPotion, 50),
                    new KeyValuePair<ItemId, int>(ItemId.ItemHyperPotion, 75),
                    new KeyValuePair<ItemId, int>(ItemId.ItemMaxPotion, 100),

                    new KeyValuePair<ItemId, int>(ItemId.ItemRevive, 25),
                    new KeyValuePair<ItemId, int>(ItemId.ItemMaxRevive, 50),

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
                    PokemonId.Farfetchd, PokemonId.Kangaskhan, PokemonId.Tauros, PokemonId.MrMime , PokemonId.Dragonite, PokemonId.Charizard, PokemonId.Zapdos, PokemonId.Snorlax, PokemonId.Alakazam, PokemonId.Mew, PokemonId.Mewtwo
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
                    PokemonId.Farfetchd, PokemonId.Kangaskhan, PokemonId.Tauros, PokemonId.MrMime , PokemonId.Dragonite, PokemonId.Charizard, PokemonId.Zapdos, PokemonId.Snorlax, PokemonId.Alakazam, PokemonId.Mew, PokemonId.Mewtwo
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
            //this.FlyingEnabled = UserSettings.Default.FlyingEnabled;
            this.LongDistanceSpeed = UserSettings.Default.LongDistanceSpeed;
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
            //this.FlyLikeCaptKirk = UserSettings.Default.FlyLikeCaptKirk;
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

            this.UseProxy = UserSettings.Default.UseProxy;
            this.UseProxyAuthentication = UserSettings.Default.UseProxyAuthentication;
            this.UseProxyHost = UserSettings.Default.UseProxyHost;
            this.UseProxyPassword = UserSettings.Default.UseProxyPassword;
            this.UseProxyPort = UserSettings.Default.UseProxyPort;
            this.UseProxyUsername = UserSettings.Default.UseProxyUsername;

            this.MaxDistanceForLongTravel = UserSettings.Default.MaxDistanceForLongTravel;

            this.MinDelay = UserSettings.Default.MinDelay;
            this.MaxDelay = UserSettings.Default.MaxDelay;
            this.FavoritePokemon = UserSettings.Default.FavoritePokemon;
            this.FavoriteAboveCp = UserSettings.Default.FavoriteAboveCp;
            this.FavoriteAboveIV = UserSettings.Default.FavoriteAboveIV;
            this.FavoriteAboveV = UserSettings.Default.FavoriteAboveV;
            this.RenamePokemon = UserSettings.Default.RenamePokemon;
            this.RenameFormat = UserSettings.Default.RenameFormat;

            this.EnableHumanizedThrows = UserSettings.Default.EnableHumanizedThrows;
            this.ForceExcellentThrowOverCp = UserSettings.Default.ForceExcellentThrowOverCp;
            this.ForceExcellentThrowOverIV = UserSettings.Default.ForceExcellentThrowOverIV;
            this.ForceExcellentThrowOverV = UserSettings.Default.ForceExcellentThrowOverV;
            this.ForceGreatThrowOverV = UserSettings.Default.ForceGreatThrowOverV;
            this.ForceGreatThrowOverCp = UserSettings.Default.ForceGreatThrowOverCp;
            this.ForceGreatThrowOverIV = UserSettings.Default.ForceGreatThrowOverIV;
            this.ExcellentThrowChance = UserSettings.Default.ExcellentThrowChance;
            this.GreatThrowChance = UserSettings.Default.GreatThrowChance;
            this.NiceThrowChance = UserSettings.Default.NiceThrowChance;
            this.CurveThrowChance = UserSettings.Default.CurveThrowChance;
            this.DevicePackageName = UserSettings.Default.DevicePackageName;

            this.DisplayPokemonCount = UserSettings.Default.DisplayPokemonCount;
            this.DisplayTopCandy = UserSettings.Default.DisplayTopCandy;
            this.MaxRunTimespan = UserSettings.Default.MaxRunTimespan.ToString();
            this.MinBreakTimespan = UserSettings.Default.MinBreakTimespan.ToString();
            this.MaxPokemonCatches = UserSettings.Default.MaxPokemonCatches;
            this.MaxPokestopVisits = UserSettings.Default.MaxPokestopVisits;

            this.EvolutionMinDelay = UserSettings.Default.EvolutionMinDelay;
            this.EvolutionMaxDelay = UserSettings.Default.EvolutionMaxDelay;
            this.EggHatchMaxDelay = UserSettings.Default.EggHatchMaxDelay;
            this.EggHatchMinDelay = UserSettings.Default.EggHatchMinDelay;
            this.TransferMinDelay = UserSettings.Default.TransferMinDelay;
            this.TransferMaxDelay = UserSettings.Default.TransferMaxDelay;
            this.CatchMinDelay = UserSettings.Default.CatchMinDelay;
            this.CatchMaxDelay = UserSettings.Default.CatchMaxDelay;
            this.RecycleMinDelay = UserSettings.Default.RecycleMinDelay;
            this.RecycleMaxDelay = UserSettings.Default.RecycleMaxDelay;
            this.PowerUpMinDelay = UserSettings.Default.PowerUpMinDelay;
            this.PowerUpMaxDelay = UserSettings.Default.PowerUpMaxDelay;

            this.MaxCatchSpeed = UserSettings.Default.MaxCatchSpeed;

            this.MaxLocationAttempts = UserSettings.Default.MaxLocationAttempts;
            this.SpeedCurveDistance = UserSettings.Default.SpeedCurveDistance;
            this.EnableWandering = UserSettings.Default.EnableWandering;
            this.ShowDebugMessages = UserSettings.Default.ShowDebugMessages;

            this.DeviceId = UserSettings.Default.DeviceId;

            PokemonId outValue4 = PokemonId.Squirtle;
            if (Enum.TryParse<PokemonId>(UserSettings.Default.TutorialPokemonId, true, out outValue4))
                this.TutorialPokmonId = outValue4;

            TeamColor outValue5 = TeamColor.Neutral;
            if (Enum.TryParse<TeamColor>(UserSettings.Default.TeamColor, true, out outValue5))
                this.TeamColor = outValue5;

            //this.PickupDailyBonuses = UserSettings.Default.PickupDailyBonuses;
            this.PickupDailyDefenderBonuses = UserSettings.Default.PickupDailyDefenderBonuses;
            this.IncludeHotPokestops = UserSettings.Default.IncludeHotPokestops;

            this.TutorialCodename = UserSettings.Default.TutorialCodename;
            this.TutorialGenerateCodename = UserSettings.Default.TutorialGenerateCodename;

            this.PokedexEntryMinDelay = UserSettings.Default.PokedexEntryMinDelay;
            this.PokedexEntryMaxDelay = UserSettings.Default.PokedexEntryMaxDelay;

            this.LocationsMinDelay = UserSettings.Default.LocationsMinDelay;
            this.LocationsMaxDelay = UserSettings.Default.LocationsMaxDelay;

            this.MinGymsBeforeBonusPickup = UserSettings.Default.MinGymsBeforeBonusPickup;

        }

        #endregion
        #region " Methods "

        public void Save()
        {
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
            SaveSession(Session);
        }

        public bool SaveSession(SessionData session)
        {
            if (!Directory.Exists(temp_path)) Directory.CreateDirectory(temp_path);
            string fileName = "Session.xml";
            string filePath = Path.Combine(temp_path, fileName);
            try
            {
                lock (sessionRoot)
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                    using (FileStream s = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        var x = new System.Xml.Serialization.XmlSerializer(typeof(SessionData));
                        x.Serialize(s, session);
                        s.Close();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Write($"Could not save {filePath}. Error: " + e.ToString(), LogLevel.Error);
            }
            return false;
        }

        public SessionData LoadSession(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    lock (sessionRoot)
                    {
                        if (!Directory.Exists(temp_path)) Directory.CreateDirectory(temp_path);
                        using (FileStream s = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            var x = new System.Xml.Serialization.XmlSerializer(typeof(SessionData));
                            var session = (SessionData)x.Deserialize(s);
                            s.Close();
                            return session;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write($"The {filePath} file could not be loaded, a new session will be created. {ex.Message} {ex.ToString()}", LogLevel.Warning);
                }
            }
            return NewSession();
        }

        public SessionData NewSession()
        {
            var session = new SessionData();
            session.StartDate = DateTime.Now;
            return session;
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
                    //this.FlyingEnabled = obj.FlyingEnabled;
                    this.LongDistanceSpeed = obj.LongDistanceSpeed;
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
                    //this.FlyLikeCaptKirk = obj.FlyLikeCaptKirk;
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

                    this.UseProxy = obj.UseProxy;
                    this.UseProxyAuthentication = obj.UseProxyAuthentication;
                    this.UseProxyHost = obj.UseProxyHost;
                    this.UseProxyPassword = obj.UseProxyPassword;
                    this.UseProxyPort = obj.UseProxyPort;
                    this.UseProxyUsername = obj.UseProxyUsername;

                    this.MaxDistanceForLongTravel = obj.MaxDistanceForLongTravel;

                    this.MinDelay = obj.MinDelay;
                    this.MaxDelay = obj.MaxDelay;
                    this.FavoritePokemon = obj.FavoritePokemon;
                    this.FavoriteAboveCp = obj.FavoriteAboveCp;
                    this.FavoriteAboveIV = obj.FavoriteAboveIV;
                    this.FavoriteAboveV = obj.FavoriteAboveV;
                    this.RenamePokemon = obj.RenamePokemon;
                    this.RenameFormat = obj.RenameFormat;

                    this.EnableHumanizedThrows = obj.EnableHumanizedThrows;
                    this.ForceExcellentThrowOverCp = obj.ForceExcellentThrowOverCp;
                    this.ForceExcellentThrowOverIV = obj.ForceExcellentThrowOverIV;
                    this.ForceExcellentThrowOverV = obj.ForceExcellentThrowOverV;
                    this.ForceGreatThrowOverV = obj.ForceGreatThrowOverV;
                    this.ForceGreatThrowOverCp = obj.ForceGreatThrowOverCp;
                    this.ForceGreatThrowOverIV = obj.ForceGreatThrowOverIV;
                    this.ExcellentThrowChance = obj.ExcellentThrowChance;
                    this.GreatThrowChance = obj.GreatThrowChance;
                    this.NiceThrowChance = obj.NiceThrowChance;
                    this.CurveThrowChance = obj.CurveThrowChance;
                    this.DevicePackageName = obj.DevicePackageName;

                    this.DisplayPokemonCount = obj.DisplayPokemonCount;
                    this.DisplayTopCandy = obj.DisplayTopCandy;
                    this.MaxRunTimespan = obj.MaxRunTimespan;
                    this.MinBreakTimespan = obj.MinBreakTimespan;
                    this.MaxPokemonCatches = obj.MaxPokemonCatches;
                    this.MaxPokestopVisits = obj.MaxPokestopVisits;

                    this.EvolutionMinDelay = obj.EvolutionMinDelay;
                    this.EvolutionMaxDelay = obj.EvolutionMaxDelay;
                    this.EggHatchMaxDelay = obj.EggHatchMaxDelay;
                    this.EggHatchMinDelay = obj.EggHatchMinDelay;
                    this.TransferMinDelay = obj.TransferMinDelay;
                    this.TransferMaxDelay = obj.TransferMaxDelay;
                    this.CatchMinDelay = obj.CatchMinDelay;
                    this.CatchMaxDelay = obj.CatchMaxDelay;
                    this.RecycleMinDelay = obj.RecycleMinDelay;
                    this.RecycleMaxDelay = obj.RecycleMaxDelay;
                    this.PowerUpMinDelay = obj.PowerUpMinDelay;
                    this.PowerUpMaxDelay = obj.PowerUpMaxDelay;

                    this.MaxCatchSpeed = obj.MaxCatchSpeed;

                    this.MaxLocationAttempts = obj.MaxLocationAttempts;
                    this.SpeedCurveDistance = obj.SpeedCurveDistance;
                    this.EnableWandering = obj.EnableWandering;
                    this.ShowDebugMessages = obj.ShowDebugMessages;

                    this.DeviceId = obj.DeviceId;

                    this.TeamColor = obj.TeamColor;
                    this.TutorialPokmonId = obj.TutorialPokmonId;

                    this.PickupDailyDefenderBonuses = obj.PickupDailyDefenderBonuses;
                    this.IncludeHotPokestops = obj.IncludeHotPokestops;

                    this.TutorialCodename = obj.TutorialCodename;
                    this.TutorialGenerateCodename = obj.TutorialGenerateCodename;

                    this.PokedexEntryMinDelay = obj.PokedexEntryMinDelay;
                    this.PokedexEntryMaxDelay = obj.PokedexEntryMaxDelay;

                    this.LocationsMinDelay = obj.LocationsMinDelay;
                    this.LocationsMaxDelay = obj.LocationsMaxDelay;

                    this.MinGymsBeforeBonusPickup = obj.MinGymsBeforeBonusPickup;

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
                if (WaypointLatitude != 0 && WaypointLongitude != 0 && WaypointAltitude != 0)
                {
                    CurrentLatitude = WaypointLatitude;
                    CurrentLongitude = WaypointLongitude;
                    CurrentAltitude = WaypointAltitude;
                }
                else if (DestinationsEnabled && Destinations.Any())
                {
                    var index = DestinationIndex < Destinations.Count ? DestinationIndex : 0;
                    var destination = Destinations[index];
                    CurrentLatitude = destination.Latitude;
                    CurrentLongitude = destination.Longitude;
                    CurrentAltitude = destination.Altitude;
                }
            }

            //resolve unknown waypoint
            if (WaypointLatitude == 0 && WaypointLongitude == 0)
            {
                if (CurrentLatitude != 0 && CurrentLongitude != 0)
                {
                    WaypointLatitude = CurrentLatitude;
                    WaypointLongitude = CurrentLongitude;
                    WaypointAltitude = CurrentAltitude;
                }
                else if (DestinationsEnabled && Destinations.Any())
                {
                    var index = DestinationIndex < Destinations.Count ? DestinationIndex : 0;
                    var destination = Destinations[index];
                    WaypointLatitude = destination.Latitude;
                    WaypointLongitude = destination.Longitude;
                    WaypointAltitude = destination.Altitude;
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
                                catch (FormatException e)
                                {
                                    Logger.Write($"Destinations in \"\\Configs\\DestinationCoords.ini\" file is invalid. Destinations will not be used. {e.ToString()}", LogLevel.Error);
                                    return null;
                                }
                            }
                            else
                            {
                                Logger.Write($"Destinations in \"\\Configs\\DestinationCoords.ini\" file is invalid. 1 line per destination, formatted like - LAT:LONG:ALT:NAME", LogLevel.Error);
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
