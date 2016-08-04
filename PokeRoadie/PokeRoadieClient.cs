#region " Imports "

using System;
using System.Linq;
using System.IO;
using System.Reflection;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI;
using POGOProtos.Networking.Envelopes;
using System.Threading.Tasks;
using PokeRoadie.Utils;

#endregion

namespace PokeRoadie
{
    public class PokeRoadieClient : Client
    {

        //#region " Singleton "

        //private static PokeRoadieClient _current;
        //public static PokeRoadieClient Current { get { _current = _current ?? new PokeRoadieClient(); return _current; } }

        //#endregion

        public DateTime? RefreshEndDate { get; set; }

        public PokeRoadieClient(PokeRoadieSettings settings, ApiFailureStrategy apiFailureStrategy) 
            : base(settings, apiFailureStrategy)
        {
        }

        //public void SetLocation(double lat, double lng, double alt)
        //{
        //    var type = typeof(Client);
        //    var latProp = type.GetProperties(BindingFlags.Public & BindingFlags.Instance).Where(x => x.Name == "CurrentLatitude").FirstOrDefault();
        //    if (latProp != null)
        //    {
        //        latProp.SetValue(this, lat);
        //    }
        //    var lngProp = type.GetProperties(BindingFlags.Public & BindingFlags.Instance).Where(x => x.Name == "CurrentLongitude").FirstOrDefault();
        //    if (lngProp != null)
        //    {
        //        lngProp.SetValue(this, lng);
        //    }
        //    var altProp = type.GetProperties(BindingFlags.Public & BindingFlags.Instance).Where(x => x.Name == "CurrentAltitude").FirstOrDefault();
        //    if (altProp != null)
        //    {
        //        altProp.SetValue(this, alt);
        //    }
        //}

        //public Task<ApiOperation> HandleApiFailure(RequestEnvelope request, ResponseEnvelope response)
        //{
        //    throw new NotImplementedException();
        //}

        //public void HandleApiSuccess(RequestEnvelope request, ResponseEnvelope response)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
