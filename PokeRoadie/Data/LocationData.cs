#region " Imports "

using System;
using PokeRoadie.Api.Helpers;

#endregion

namespace PokeRoadie
{
    [Serializable]
    public class LocationData
    {
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }

        public LocationData()
        {
        }

        public LocationData(double lat, double lng, double alt)
        {
            Latitude = lat;
            Longitude = lng;
            Altitude = alt;
        }
        public GeoCoordinate GetGeo()
        {
            return new GeoCoordinate(Latitude, Longitude, Altitude);
        }
    }
}
