using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Protobuf;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Exceptions;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.Helpers;
using PokemonGo.RocketAPI.HttpClient;
using PokemonGo.RocketAPI.Login;
using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;
using PokemonGo.RocketAPI.Logging;

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

        public IApiFailureStrategy ApiFailure { get; set; }
        public ISettings Settings { get; }
        public string AuthToken { get; set; }

        public double CurrentLatitude { get; internal set; }
        public double CurrentLongitude { get; internal set; }
        public double CurrentAltitude { get; internal set; }

        public AuthType AuthType => Settings.AuthType;

        internal readonly PokemonHttpClient PokemonHttpClient;
        internal string ApiUrl { get; set; }
        internal AuthTicket AuthTicket { get; set; }
        private Random Random { get; set; }
        public static WebProxy Proxy { get; set; }
        public byte[] SessionHash { get; set; }


        public Client(ISettings settings, IApiFailureStrategy apiFailureStrategy)
        {
            //handle initial session hash
            Random = new Random(DateTime.Now.Millisecond);
            GenerateNewSessionHash();

            //setup
            Settings = settings;
            ApiFailure = apiFailureStrategy;
            if (settings.UseProxy) InitProxy(settings);
            PokemonHttpClient = new PokemonHttpClient(settings);
            Login = new Rpc.Login(this);
            Player = new Rpc.Player(this);
            Download = new Rpc.Download(this);
            Inventory = new Rpc.Inventory(this);
            Map = new Rpc.Map(this);
            Fort = new Rpc.Fort(this);
            Encounter = new Rpc.Encounter(this);
            Misc = new Rpc.Misc(this);

            //player coords
            Player.SetCoordinates(Settings.DefaultLatitude, Settings.DefaultLongitude, Settings.DefaultAltitude);
        }

        private void InitProxy(ISettings settings)
        {
            Proxy = new WebProxy(settings.UseProxyHost, settings.UseProxyPort);

            if (settings.UseProxyAuthentication && !string.IsNullOrWhiteSpace(settings.UseProxyUsername) && !string.IsNullOrWhiteSpace(settings.UseProxyPassword))
                Proxy.Credentials = new NetworkCredential(settings.UseProxyUsername, settings.UseProxyPassword);

        }

        public void GenerateNewSessionHash()
        {
            SessionHash = new byte[16] { GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte(), GenRandomByte() };
        }

        private byte GenRandomByte()
        {
            return System.Convert.ToByte(Random.Next(0, 255));
        }

        public bool CheckForInternetConnection()
        {
            if(!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                Logger.Write("Lost internet connection, waiting to re-establish...", LogLevel.Error);
                while (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    System.Threading.Thread.Sleep(1000);
                    Logger.Append(".");
                }
                
                
                Logger.Write("Internet connection re-established!", LogLevel.Warning);
                return false;
            }
            return true;
        }

 

    }
}