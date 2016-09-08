#region " Imports "

using System;
using System.Linq;
using System.IO;
using System.Reflection;
using PokeRoadie.Api.Extensions;
using PokeRoadie.Api;
using POGOProtos.Networking.Envelopes;
using System.Threading.Tasks;
using PokeRoadie.Utils;

#endregion

namespace PokeRoadie
{
    public class PokeRoadieClient : Client
    {

        #region " Properties "

        public Context Context { get; private set; }

        //shadowed settings property
        new public PokeRoadieSettings Settings { get { return Context.Settings; } }

        #endregion
        #region " Constructors "

        public PokeRoadieClient(Context context) 
            : base(context.Settings, context.ApiFailureStrategy)
        {
            Context = context;
        }

        #endregion

    }
}
