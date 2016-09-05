#region " Imports "

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;

using PokemonGo.RocketAPI;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.Helpers;
using PokemonGo.RocketAPI.Logging;
using PokemonGo.RocketAPI.Exceptions;
using PokemonGo.RocketAPI.Rpc;

using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Responses;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Map.Fort;
using POGOProtos.Data.Player;
using POGOProtos.Map.Pokemon;
using POGOProtos.Data.Capture;

using PokeRoadie.Extensions;
using PokeRoadie.Utils;
//using PokeRoadie.Logging;
using System.ComponentModel;

#endregion

namespace PokeRoadie
{
    public class Context
    {
        public PokeRoadieLogic Logic { get; set; }
        public ApiFailureStrategy ApiFailureStrategy { get; set;}
        public PokeRoadieClient Client { get; set; }
        public PokeRoadieInventory Inventory { get; set; }
        public PokeRoadieSettings Settings { get; set; }
        public Statistics Statistics { get; set; }
        public Navigation Navigation { get; set; }
        public SessionData Session { get; set; }
        public Player PlayerState { get; set; }
        public Directories Directories { get; set; }
        public ISynchronizeInvoke Invoker { get; set; }

        public Context(PokeRoadieSettings settings)
        {
            Settings = settings;
            Directories = new Directories();
            ApiFailureStrategy = new ApiFailureStrategy(this);
            Client = new PokeRoadieClient(this);
            ApiFailureStrategy.Client = Client;
            Inventory = new PokeRoadieInventory(this);
            Statistics = new Statistics(this);
            Navigation = new Navigation(this);
            Logic = new PokeRoadieLogic(this);
            
        }
        public Context(PokeRoadieSettings settings, ISynchronizeInvoke form) : this(settings)
        {
            Invoker = form;
        }
    }
}
