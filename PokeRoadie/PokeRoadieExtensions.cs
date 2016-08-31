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

        #region " Constants "

        const double twoThousand = 2000d;
        const double twoHundred = 200d;
        const double oneHundred = 100d;

        #endregion
        #region " PokemonData Extensions "

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


        public static string GetStats(this PokemonData pokemon)
        {
            return $"{((String.IsNullOrWhiteSpace(pokemon.DeployedFortId) ? "" : "^")+(pokemon.Favorite == 1 ? "*" : "")+pokemon.PokemonId.ToString()).PadRight(21)} {pokemon.CalculatePokemonValue().ToString().PadRight(3)} V | {pokemon.Cp.ToString().PadLeft(4)} CP | {pokemon.GetPerfection().ToString("0.00").PadLeft(6)} % | LV {pokemon.GetLevel().ToString("00")} | {(pokemon.Stamina.ToString() + "/" + pokemon.StaminaMax.ToString()+" HP").PadLeft(10)} | {pokemon.IndividualAttack.ToString("00").PadLeft(2)} A | {pokemon.IndividualDefense.ToString("00").PadLeft(2)} D | {pokemon.IndividualStamina.ToString("00").PadLeft(2)} S | {pokemon.Move1.GetMoveName().PadRight(14)}({CalculateMoveValue(pokemon.Move1.GetMoveName())}) | {pokemon.Move2.GetMoveName().PadRight(14)}({CalculateMoveValue(pokemon.Move2.GetMoveName())})";
        }

        public static string GetMinStats(this PokemonData pokemon)
        {
            var name = pokemon.PokemonId.ToString();
            if (PokeRoadieSettings.Current.DisplayStyle == "disdain")
            {
                if (name.Length > 10) name = name.Substring(0, 10);
                return $"{(String.IsNullOrWhiteSpace(pokemon.DeployedFortId) ? "" : "^")+(pokemon.Favorite == 1 ? "*" : "")}{name} "+$"({pokemon.CalculatePokemonValue()}V-{pokemon.Cp.ToString()}Cp-{pokemon.GetPerfection().ToString("0.00")}%-Lv{pokemon.GetLevel().ToString("00")}-{pokemon.StaminaMax.ToString()}Hp)";
            }
            else if (PokeRoadieSettings.Current.DisplayStyle == "spastic")
            {
                return $"{((String.IsNullOrWhiteSpace(pokemon.DeployedFortId) ? "" : "^")+(pokemon.Favorite == 1 ? "*" : "")+name).PadRight(21)} "+$"({pokemon.CalculatePokemonValue()}V-{pokemon.Cp.ToString()}CP-{pokemon.GetPerfection().ToString("0.00")}%-LV{pokemon.GetLevel().ToString("00")}-{pokemon.StaminaMax.ToString()}HP)".PadRight(33);
            }
            else
            {
                return $"Please enter value disdain or spastic in <DisplayStyle></DisplayStyle>!";
            }
        }

        #endregion
        #region " Move Extensions "
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
            return $"{((String.IsNullOrWhiteSpace(pokemon.DeployedFortId) ? "" : "^")+(pokemon.Favorite == 1 ? "*" : "")+pokemon.PokemonId.ToString()).PadRight(21)} {pokemon.CalculatePokemonValue().ToString().PadRight(3)} V | {pokemon.Cp.ToString().PadLeft(4)} CP | {pokemon.GetPerfection().ToString("0.00").PadLeft(6)} % | LV {pokemon.GetLevel().ToString("00")} | {(pokemon.Stamina.ToString() + "/" + pokemon.StaminaMax.ToString()+" HP").PadLeft(10)} | {pokemon.IndividualAttack.ToString("00").PadLeft(2)} A | {pokemon.IndividualDefense.ToString("00").PadLeft(2)} D | {pokemon.IndividualStamina.ToString("00").PadLeft(2)} S | {pokemon.Move1.GetMoveName().PadRight(14)}({CalculateMoveValue(pokemon.Move1.GetMoveName())}) | {pokemon.Move2.GetMoveName().PadRight(14)}({CalculateMoveValue(pokemon.Move2.GetMoveName())})";
        }

        public static string GetMinStats(this PokemonData pokemon)
        {
            var name = pokemon.PokemonId.ToString();
            if (PokeRoadieSettings.Current.DisplayStyle == "disdain")
            {
                if (name.Length > 10) name = name.Substring(0, 10);
                return $"{(String.IsNullOrWhiteSpace(pokemon.DeployedFortId) ? "" : "^")+(pokemon.Favorite == 1 ? "*" : "")}{name} "+$"({pokemon.CalculatePokemonValue()}V-{pokemon.Cp.ToString()}Cp-{pokemon.GetPerfection().ToString("0.00")}%-Lv{pokemon.GetLevel().ToString("00")}-{pokemon.StaminaMax.ToString()}Hp)";
            }
            else if (PokeRoadieSettings.Current.DisplayStyle == "spastic")
            {
                return $"{((String.IsNullOrWhiteSpace(pokemon.DeployedFortId) ? "" : "^")+(pokemon.Favorite == 1 ? "*" : "")+name).PadRight(21)} "+$"({pokemon.CalculatePokemonValue()}V-{pokemon.Cp.ToString()}CP-{pokemon.GetPerfection().ToString("0.00")}%-LV{pokemon.GetLevel().ToString("00")}-{pokemon.StaminaMax.ToString()}HP)".PadRight(33);
            }
            else
            {
                return $"Please enter value disdain or spastic in <DisplayStyle></DisplayStyle>!";
            }

            return move;
        }

        public static string GetMoveName(this PokemonMove move)
        {
            var val = move.ToString();
            if (val.ToLower().EndsWith("fast")) val = val.Substring(0, val.Length - 4);
            return val;
        }

        #endregion
        #region " True Value Extensions "

        public static double CalculatePokemonValue(this PokemonData pokemon)
        {
            var p = System.Convert.ToInt32(PokemonInfo.CalculatePokemonPerfection(pokemon) * 1.5);
            var cp = Convert.ToInt32(pokemon.Cp == 0 ? 0 : pokemon.Cp / twoThousand * oneHundred);
            var m1 = CalculateMoveValue(pokemon.Move1.GetMoveName()) * .5;
            var m2 = CalculateMoveValue(pokemon.Move2.GetMoveName()) * .5;
            var l = (pokemon.GetLevel() == 0 ? 0 : pokemon.GetLevel() * 3.5);
            return Math.Round(p + cp + m1 + m2 + l, 0);
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

        #endregion
        #region " Xlo Extesnions "

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
                var data = new Xml.Gym2();
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
                        var m = new Xml.Membership2();
                        m.Player.Name = membership.TrainerPublicProfile.Name;
                        m.Player.Level = membership.TrainerPublicProfile.Level;
                        m.Pokemon.BattlesAttacked = membership.PokemonData.BattlesAttacked;
                        m.Pokemon.BattlesDefended = membership.PokemonData.BattlesDefended;
                        m.Pokemon.Cp = membership.PokemonData.Cp;
                        m.Pokemon.Hp = membership.PokemonData.StaminaMax;
                        m.Pokemon.HeightM = membership.PokemonData.HeightM;
                        m.Pokemon.WeightKg = membership.PokemonData.WeightKg;
                        m.Pokemon.Id = membership.PokemonData.Id;
                        m.Pokemon.IndividualAttack = membership.PokemonData.IndividualAttack;
                        m.Pokemon.IndividualDefense = membership.PokemonData.IndividualDefense;
                        m.Pokemon.IndividualStamina = membership.PokemonData.IndividualStamina;
                        m.Pokemon.PlayerLevel = membership.TrainerPublicProfile.Level;
                        m.Pokemon.PlayerTeam = fortDetails.GymState.FortData.OwnedByTeam.ToString();
                        m.Pokemon.IV = membership.PokemonData.GetPerfection();
                        m.Pokemon.Nickname = membership.PokemonData.Nickname;
                        m.Pokemon.V = membership.PokemonData.CalculatePokemonValue();
                        m.Pokemon.Move1 = membership.PokemonData.Move1.ToString();
                        m.Pokemon.Move2 = membership.PokemonData.Move2.ToString();
                        m.Pokemon.Nickname = membership.PokemonData.Nickname;
                        m.Pokemon.Level = membership.PokemonData.NumUpgrades;
                        m.Pokemon.Origin = membership.PokemonData.Origin;
                        m.Pokemon.Type = membership.PokemonData.PokemonId.ToString();
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

        #endregion
        #region " Location Extensions "

        //for backwards compatibility
        public static double CalculateDistanceInMeters(this GeoCoordinate sourceLocation, GeoCoordinate destinationLocation)
        {
            return sourceLocation.GetDistanceTo(destinationLocation);
        }

        public static GeoCoordinate CreateWaypoint(this GeoCoordinate sourceLocation, double distanceInMeters, double bearingDegrees)
        //from http://stackoverflow.com/a/17545955
        {
            var distanceKm = distanceInMeters / 1000.0;
            var distanceRadians = distanceKm / 6371; //6371 = Earth's radius in km

            var bearingRadians = ToRad(bearingDegrees);
            var sourceLatitudeRadians = ToRad(sourceLocation.Latitude);
            var sourceLongitudeRadians = ToRad(sourceLocation.Longitude);

            var targetLatitudeRadians = Math.Asin(Math.Sin(sourceLatitudeRadians) * Math.Cos(distanceRadians)
                                                  +
                                                  Math.Cos(sourceLatitudeRadians) * Math.Sin(distanceRadians) *
                                                  Math.Cos(bearingRadians));

            var targetLongitudeRadians = sourceLongitudeRadians + Math.Atan2(Math.Sin(bearingRadians)
                                                                             * Math.Sin(distanceRadians) *
                                                                             Math.Cos(sourceLatitudeRadians),
                Math.Cos(distanceRadians)
                - Math.Sin(sourceLatitudeRadians) * Math.Sin(targetLatitudeRadians));

            // adjust toLonRadians to be in the range -180 to +180...
            targetLongitudeRadians = (targetLongitudeRadians + 3 * Math.PI) % (2 * Math.PI) - Math.PI;

            return new GeoCoordinate(ToDegrees(targetLatitudeRadians), ToDegrees(targetLongitudeRadians));
        }

        public static GeoCoordinate CreateWaypoint(this GeoCoordinate sourceLocation, double distanceInMeters, double bearingDegrees, double altitude)
        //from http://stackoverflow.com/a/17545955
        {
            var distanceKm = distanceInMeters / 1000.0;
            var distanceRadians = distanceKm / 6371; //6371 = Earth's radius in km

            var bearingRadians = ToRad(bearingDegrees);
            var sourceLatitudeRadians = ToRad(sourceLocation.Latitude);
            var sourceLongitudeRadians = ToRad(sourceLocation.Longitude);

            var targetLatitudeRadians = Math.Asin(Math.Sin(sourceLatitudeRadians) * Math.Cos(distanceRadians)
                                                  +
                                                  Math.Cos(sourceLatitudeRadians) * Math.Sin(distanceRadians) *
                                                  Math.Cos(bearingRadians));

            var targetLongitudeRadians = sourceLongitudeRadians + Math.Atan2(Math.Sin(bearingRadians)
                                                                             * Math.Sin(distanceRadians) *
                                                                             Math.Cos(sourceLatitudeRadians),
                Math.Cos(distanceRadians)
                - Math.Sin(sourceLatitudeRadians) * Math.Sin(targetLatitudeRadians));

            // adjust toLonRadians to be in the range -180 to +180...
            targetLongitudeRadians = (targetLongitudeRadians + 3 * Math.PI) % (2 * Math.PI) - Math.PI;

            return new GeoCoordinate(ToDegrees(targetLatitudeRadians), ToDegrees(targetLongitudeRadians), altitude);
        }

        public static double DegreeBearing(this GeoCoordinate sourceLocation, GeoCoordinate targetLocation)
        // from http://stackoverflow.com/questions/2042599/direction-between-2-latitude-longitude-points-in-c-sharp
        {
            var dLon = ToRad(targetLocation.Longitude - sourceLocation.Longitude);
            var dPhi = Math.Log(
                Math.Tan(ToRad(targetLocation.Latitude) / 2 + Math.PI / 4) /
                Math.Tan(ToRad(sourceLocation.Latitude) / 2 + Math.PI / 4));
            if (Math.Abs(dLon) > Math.PI)
                dLon = dLon > 0 ? -(2 * Math.PI - dLon) : 2 * Math.PI + dLon;
            return ToBearing(Math.Atan2(dLon, dPhi));
        }
        private static double ToBearing(double radians)
        {
            // convert radians to degrees (as bearing: 0...360)
            return (ToDegrees(radians) + 360) % 360;
        }
        private static double ToDegrees(double radians)
        {
            return radians * 180 / Math.PI;
        }
        private static double ToRad(double degrees)
        {
            return degrees * (Math.PI / 180);
        }

        #endregion

    }
}
