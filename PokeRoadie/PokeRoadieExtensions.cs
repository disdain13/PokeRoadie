#region " Imports "

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using PokemonGo.RocketAPI;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.Helpers;
using PokemonGo.RocketAPI.Logging;
using PokemonGo.RocketAPI.Exceptions;

using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Responses;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Map.Fort;
using POGOProtos.Map.Pokemon;

using PokeRoadie.Extensions;

#endregion


namespace PokeRoadie.Extensions
{
    public static class PokeRoadieExtensions
    {
        const double twoThousand = 2000d;
        const double twoHundred = 200d;
        const double oneHundred = 100d;

        public static double GetPerfection(this PokemonData pokemon)
        {
            return PokemonInfo.CalculatePokemonPerfection(pokemon);
        }

        public static double GetLevel(this PokemonData pokemon)
        {
            return PokemonInfo.GetLevel(pokemon);
        }

        public static int GetMaxCP(this PokemonData pokemon)
        {
            return PokemonInfo.CalculateMaxCP(pokemon);
        }

        public static int GetMinCP(this PokemonData pokemon)
        {
            return PokemonInfo.CalculateMinCP(pokemon);
        }

        public static double GetMaxCPMultiplier(this PokemonData pokemon)
        {
            return PokemonInfo.CalculateMaxCPMultiplier(pokemon);
        }

        public static double GetMinCPMultiplier(this PokemonData pokemon)
        {
            return PokemonInfo.CalculateMinCPMultiplier(pokemon);
        }


        public static int GetPowerUpLevel(this PokemonData pokemon)
        {
            return PokemonInfo.GetPowerUpLevel(pokemon);
        }

        public static BaseStats GetBaseStats(this PokemonData pokemon)
        {
            return PokemonInfo.GetBaseStats(pokemon.PokemonId);
        }

        public static double GetCP(this PokemonData pokemon)
        {
            return PokemonInfo.CalculateCP(pokemon);
        }

        public static MoveData GetMove(this ICollection<MoveData> list, string name)
        {
            var filteredName = name.ToLower();
            var move = list.Where(x => x.Name.Replace(" ", "").ToLower() == filteredName).FirstOrDefault();
            if (move == null)
            {
                if (filteredName.EndsWith("fast"))
                    filteredName = filteredName.Substring(0, filteredName.Length - 4);
                move = list.Where(x => x.Name.Replace(" ", "").ToLower() == filteredName).FirstOrDefault();
            }
            if (move == null)
            {
                Logger.Write($"Pokemon move '{name}' could not be found in the PokemonMoveDatas.xml file, using default move.", LogLevel.Error);
                move = new MoveData();
                move.Name = name;
                move.Power = 50;
                move.PP = 20;
                move.Type = "Normal";
                move.Category = "Physical";
                move.Effect = "Unknown";
                move.Accuracy = 75;
            }
                
            return move;
        }

        public static string GetStats(this PokemonData pokemon)
        {
            return $"{((String.IsNullOrWhiteSpace(pokemon.DeployedFortId) ? "" : "^") + (pokemon.Favorite == 1 ? "*" : "") + pokemon.PokemonId.ToString()).PadRight(19,' ')} {pokemon.CalculatePokemonValue().ToString().PadRight(3, ' ')} True Value | {pokemon.Cp.ToString().PadLeft(4, ' ')} Cp | {pokemon.IndividualAttack.ToString("00").PadLeft(2)}ATK,{pokemon.IndividualDefense.ToString("00").PadLeft(2)}DEF,{pokemon.IndividualStamina.ToString("00").PadLeft(2)}STA {pokemon.GetPerfection().ToString("0.00").PadLeft(6, ' ')}% Perfect | Lvl {pokemon.GetLevel().ToString("00")} | {pokemon.Stamina.ToString().PadLeft(3, ' ')}/{pokemon.StaminaMax.ToString().PadRight(1, ' ')} Hp | {pokemon.Move1.GetMoveName().PadRight(13, ' ')}({CalculateMoveValue(pokemon.Move1.GetMoveName())}) / {pokemon.Move2.GetMoveName().PadRight(13, ' ')}({CalculateMoveValue(pokemon.Move2.GetMoveName())})";
        }

        public static string GetMinStats(this PokemonData pokemon)
        {
            return $"{pokemon.PokemonId.ToString().PadRight(19, ' ')} ({pokemon.CalculatePokemonValue().ToString().PadLeft(3)}V-{pokemon.Cp.ToString().PadLeft(4,'-')}CP-{pokemon.GetPerfection().ToString("0.00").PadLeft(6,'-')}%-LV{pokemon.GetLevel().ToString("00")}-{pokemon.StaminaMax.ToString().PadLeft(3,'-')}HP)";
        }

        public static string GetMoveName(this PokemonMove move)
        {
            var val = move.ToString();
            if (val.ToLower().EndsWith("fast")) val = val.Substring(0, val.Length - 4);
            return val;
        }

        private static int CalculateMoveValue(string moveName)
        {
            var m1a = 100;
            var move1 = PokeRoadieSettings.Current.PokemonMoves.GetMove(moveName);
            if (move1 == null) return 20;
            m1a = move1.Power + move1.Accuracy + move1.Hit;
            m1a = m1a < 51 ? 50 : m1a > 200 ? 200 : m1a;
            double m1b = (move1.PP > 0 && move1.PP < 15) ?
                3.0d : 4.0d;
            return Convert.ToInt32(m1a / m1b);
        }

        public static double CalculatePokemonValue(this PokemonData pokemon)
        {
            var p = System.Convert.ToInt32(PokemonInfo.CalculatePokemonPerfection(pokemon));
            var cp = Convert.ToInt32(pokemon.Cp == 0 ? 0 : pokemon.Cp / twoThousand * oneHundred);
            var m1 = CalculateMoveValue(pokemon.Move1.GetMoveName());
            var m2 = CalculateMoveValue(pokemon.Move2.GetMoveName());
            var l = (pokemon.GetLevel() == 0 ? 0 : (pokemon.GetLevel() / 40) * oneHundred);
            return Math.Round(p + cp + m1 + m2 + l, 0);
        }

        public static void Save(this FortDetailsResponse fortInfo, string filePath, double currentAltitude)
        {
            try
            {
                var data = new Xml.Pokestop();
                data.Id = fortInfo.FortId;
                data.Latitude = fortInfo.Latitude;
                data.Longitude = fortInfo.Longitude;
                data.Altitude = currentAltitude;
                data.Name = fortInfo.Name;
                data.Description = fortInfo.Description;
                data.Fp = fortInfo.Fp;
                foreach (var img in fortInfo.ImageUrls)
                {
                    data.ImageUrls.Add(img);
                }
                Xml.Serializer.SerializeToFile(data, filePath);
            }
            catch// (Exception e)
            {
                //Logger.Write($"Could not save the pokestop information file for {fortInfo.FortId} - {e.ToString()}", LogLevel.Error);
            }
        }

        public static void Save(this GetGymDetailsResponse fortDetails, FortDetailsResponse fortInfo, string filePath, double currentAltitude)
        {
            //write data file
            try
            {
                var data = new Xml.Gym();
                data.Id = fortInfo.FortId;
                data.Latitude = fortDetails.GymState.FortData.Latitude;
                data.Longitude = fortDetails.GymState.FortData.Longitude;
                data.Altitude = currentAltitude;
                data.Name = fortDetails.Name;
                data.Description = fortDetails.Description;
                data.Fp = fortInfo.Fp;
                data.CooldownCompleteTimestampMs = fortDetails.GymState.FortData.CooldownCompleteTimestampMs;
                data.GymPoints = fortDetails.GymState.FortData.GymPoints;
                data.LastModifiedTimestampMs = fortDetails.GymState.FortData.LastModifiedTimestampMs;
                data.Sponsor = fortDetails.GymState.FortData.Sponsor.ToString();
                data.Team = fortDetails.GymState.FortData.OwnedByTeam.ToString();
                if (fortDetails.GymState.Memberships != null && fortDetails.GymState.Memberships.Count() > 0)
                {
                    foreach (var membership in fortDetails.GymState.Memberships)
                    {
                        var m = new Xml.Membership();
                        m.Player.Name = membership.TrainerPublicProfile.Name;
                        m.Player.Level = membership.TrainerPublicProfile.Level;
                        m.Pokemon.BattlesAttacked = membership.PokemonData.BattlesAttacked;
                        m.Pokemon.BattlesDefended = membership.PokemonData.BattlesDefended;
                        m.Pokemon.Cp = membership.PokemonData.Cp;
                        m.Pokemon.Favorite = membership.PokemonData.Favorite;
                        m.Pokemon.HeightM = membership.PokemonData.HeightM;
                        m.Pokemon.Id = membership.PokemonData.Id;
                        m.Pokemon.IndividualAttack = membership.PokemonData.IndividualAttack;
                        m.Pokemon.IndividualDefense = membership.PokemonData.IndividualDefense;
                        m.Pokemon.IndividualStamina = membership.PokemonData.IndividualStamina;
                        m.Pokemon.IsEgg = membership.PokemonData.IsEgg;
                        m.Pokemon.Move1 = membership.PokemonData.Move1.ToString();
                        m.Pokemon.Move2 = membership.PokemonData.Move2.ToString();
                        m.Pokemon.Nickname = membership.PokemonData.Nickname;
                        m.Pokemon.NumUpgrades = membership.PokemonData.NumUpgrades;
                        m.Pokemon.BattlesAttacked = membership.PokemonData.BattlesAttacked;
                        data.Memberships.Add(m);
                    }
                }

                foreach (var img in fortInfo.ImageUrls)
                {
                    data.ImageUrls.Add(img);
                }
                Xml.Serializer.SerializeToFile(data, filePath);

            }
            catch// (Exception e)
            {
                //Logger.Write($"Could not save the gym information file for {fortInfo.FortId} - {e.ToString()}", LogLevel.Error);
            }
        }

        public static void Save(this PokeRoadieInventory inventory, PokemonData pokemon, GeoCoordinate geo, string playerName, int playerLevel, string playerTeam, ulong encounterId, EncounterSourceTypes encounterType, string filePath)
        {
            try
            {
                var data = new Xml.PokemonEncounter()
                {
                    EncounterId = encounterId,
                    EncounterType = Convert.ToInt32(encounterType),
                    Latitude = geo.Latitude,
                    Longitude = geo.Longitude,
                    Altitude = geo.Altitude,
                    Player = playerName,
                    PlayerLevel = playerLevel,
                    PlayerTeam = playerTeam,
                    Cp = pokemon.Cp,
                    IV = pokemon.GetPerfection(),
                    V = pokemon.CalculatePokemonValue(),
                    NumberOfUpgrades = System.Convert.ToInt32(pokemon.GetLevel()),
                    Type = pokemon.PokemonId.ToString()
                };
                Xml.Serializer.SerializeToFile(data, filePath);
            }
            catch// (Exception e)
            {
                //Logger.Write($"Could not save the encounter information file for {encounterId} - {e.ToString()}", LogLevel.Error);
            }
        }
    }
}
