#region " Imports "

using PokemonGo.RocketAPI.Helpers;

#endregion

namespace PokeRoadie
{
    public class DestinationData
    {
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }

        public GeoCoordinate GetGeo()
        {
            return new GeoCoordinate(Latitude, Longitude, Altitude);
        }
    }
}
