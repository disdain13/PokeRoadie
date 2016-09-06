#region " Imports "

using PokeRoadie.Utils;
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
        public Utility Utility { get; set; }

        public Context(PokeRoadieSettings settings)
        {
            Settings = settings;
            Directories = new Directories();
            Utility = new Utility(this);
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
