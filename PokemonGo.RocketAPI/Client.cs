#region

using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Protobuf;

using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Exceptions;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.Helpers;

using PokemonGo.RocketAPI.Logging;
using PokemonGo.RocketAPI.HttpClient;
using PokemonGo.RocketAPI.Rpc;

using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;

#endregion


namespace PokemonGo.RocketAPI
{
    public class Client
    {

        public Rpc.Login Login;
        public Rpc.Player Player;
        public Rpc.Download Download;
        public Rpc.Inventory Inventory;
        public Rpc.Map Map;
        public Rpc.Fort Fort;
        public Rpc.Encounter Encounter;
        public Rpc.Misc Misc;

        internal readonly PokemonHttpClient PokemonHttpClient = new PokemonHttpClient();

        public AuthType AuthType { get; set; } = AuthType.Google;
        public string AuthToken { get; set; }
        public AuthTicket AuthTicket { get; set; }
        public string ApiUrl { get; set; }
        public ISettings Settings { get; }
        public string AccessToken { get; set; }
        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public double StartAltitude { get; set; }
        public double CurrentLatitude { get; set; }
        public double CurrentLongitude { get; set; }
        public double CurrentAltitude { get; set; }
        public List<Destination> Destinations { get; private set; }
        public DateTime? DestinationEndDate { get; set; }
        public DateTime? RefreshEndDate { get; set; }

        private static string configs_path = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
        private static string lastcoords_file = Path.Combine(configs_path, "LastCoords.ini");
        private static string destinationcoords_file = Path.Combine(configs_path, "DestinationCoords.ini");

        public Client(ISettings settings)
        {
            //set settings object
            Settings = settings;

            //load API
            Login = new Rpc.Login(this);
            Player = new Rpc.Player(this);
            Download = new Rpc.Download(this);
            Inventory = new Rpc.Inventory(this);
            Map = new Rpc.Map(this);
            Fort = new Rpc.Fort(this);
            Encounter = new Rpc.Encounter(this);
            Misc = new Rpc.Misc(this);

            //load Destinations
            Destinations = GetDestinationListFromFile(settings);

            //set player coordinates
            Player.SetCoordinates(Settings.CurrentLatitude, Settings.CurrentLongitude, Settings.CurrentAltitude);

        }

        /// <summary>
        /// Gets a list of target destinations.
        /// </summary>
        /// <returns>list of target destinations</returns>
        public static List<Destination> GetDestinationListFromFile(ISettings settings)
        {
            var list = new List<Destination>();
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
                                        var newDestination = new Destination();
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
                if (settings.CurrentLatitude != 0 && settings.CurrentLongitude != 0)
                {

                    using (StreamWriter w = File.CreateText(destinationcoords_file))
                    {
                        w.Write($"{settings.CurrentLatitude}:{settings.CurrentLongitude}:{settings.CurrentAltitude}:Default Location");
                        w.Close();
                    }

                    var d = new Destination();
                    d.Latitude = settings.CurrentLatitude;
                    d.Longitude = settings.CurrentLongitude;
                    d.Altitude = settings.CurrentAltitude;
                    d.Name = "Default Location";
                    list.Add(d);
                }
            }
            return list;
        }
        
        /// <summary>
        /// For GUI clients only. GUI clients don't use the DoGoogleLogin, but call the GoogleLogin class directly
        /// </summary>
        /// <param name="type"></param>
        public void SetAuthType(AuthType type)
        {
            AuthType = type;
        }


        /// <summary>
        /// Gets the lat LNG from file.
        /// </summary>
        /// <returns>Tuple&lt;System.Double, System.Double&gt;.</returns>
        public static Tuple<double, double> GetLatLngFromFile()
        {
            if (!Directory.Exists(configs_path))
                Directory.CreateDirectory(configs_path);
            if (File.Exists(lastcoords_file) && File.ReadAllText(lastcoords_file).Contains(":"))
            {
                var latlngFromFile = File.ReadAllText(lastcoords_file);
                var latlng = latlngFromFile.Split(':');
                if (latlng[0].Length != 0 && latlng[1].Length != 0)
                {
                    try
                    {
                        double temp_lat = Convert.ToDouble(latlng[0]);
                        double temp_long = Convert.ToDouble(latlng[1]);

                        if (temp_lat >= -90 && temp_lat <= 90 && temp_long >= -180 && temp_long <= 180)
                        {
                            //SetCoordinates(Convert.ToDouble(latlng[0]), Convert.ToDouble(latlng[1]), Settings.DefaultAltitude);
                            return new Tuple<double, double>(temp_lat, temp_long);
                        }
                        else
                        {
                            Logger.Write("Coordinates in \"\\Configs\\Coords.ini\" file is invalid, using the default coordinates", LogLevel.Error);
                            return null;
                        }
                    }
                    catch (FormatException)
                    {
                        Logger.Write("Coordinates in \"\\Configs\\Coords.ini\" file is invalid, using the default coordinates", LogLevel.Error);
                        return null;
                    }
                }
            }
            return null;
        }

    }
}
