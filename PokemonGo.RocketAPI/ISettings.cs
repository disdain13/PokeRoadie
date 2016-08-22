using PokemonGo.RocketAPI.Enums;

namespace PokemonGo.RocketAPI
{
    public interface ISettings
    {
        AuthType AuthType { get; set; }
        double DefaultLatitude { get; set; }
        double DefaultLongitude { get; set; }
        double DefaultAltitude { get; set; }
        string GoogleRefreshToken { get; set; }
        string PtcPassword { get; set; }
        string PtcUsername { get; set; }
        string GoogleUsername { get; set; }
        string GooglePassword { get; set; }
        string DevicePackageName { get; set; }
        string DeviceId { get; set; }
        bool UseProxy { get; set; }
        bool UseProxyAuthentication { get; set; }
        string UseProxyHost { get; set; }
        int UseProxyPort { get; set; }
        string UseProxyUsername { get; set; }
        string UseProxyPassword { get; set; }
    }
}