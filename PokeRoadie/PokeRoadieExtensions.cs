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
                move.Power = 30;
                move.PP = 15;
                move.Type = "Normal";
                move.Category = "Physical";
                move.Effect = "Unknown";
                move.Accuracy = 50;
            }
                
            return move;
        }
        public static string GetStats(this PokemonData pokemon)
        {
            return $"{((pokemon.Favorite == 1 ? "*" : "") + pokemon.PokemonId.ToString()).PadRight(19,' ')} {pokemon.CalculatePokemonValue()} Total Value | {pokemon.Cp.ToString().PadLeft(4, ' ')} Cp | {pokemon.GetPerfection().ToString("0.00")}% Perfect | Lvl {pokemon.GetLevel().ToString("00")} | {pokemon.Move1.GetMoveName()}/{pokemon.Move2.GetMoveName()}";
        }
        public static string GetMinStats(this PokemonData pokemon)
        {
            return $"{pokemon.PokemonId.ToString()} {pokemon.CalculatePokemonValue()} V | {pokemon.Cp.ToString().PadLeft(4, ' ')} Cp | {pokemon.GetPerfection().ToString("0.00")}% | Lvl {pokemon.GetLevel().ToString("00")}";
        }
        public static string GetMoveName(this PokemonMove move)
        {
            var val = move.ToString();
            if (val.ToLower().EndsWith("fast")) val = val.Substring(0, val.Length - 4);
            return val;
        }
        public static double CalculatePokemonValue(this PokemonData pokemon)
        {
            const double twoThousand = 2000;
            const double twoHundred = 200;
            var move1 = PokeRoadieSettings.Current.PokemonMoves.GetMove(pokemon.Move1.GetMoveName());
            var move2 = PokeRoadieSettings.Current.PokemonMoves.GetMove(pokemon.Move2.GetMoveName());
            var p = System.Convert.ToInt32(PokemonInfo.CalculatePokemonPerfection(pokemon));
            var cp = Convert.ToInt32(pokemon.Cp == 0 ? 0 : pokemon.Cp / twoThousand * twoHundred);
            var m1 = move1 == null && move1.Power > 0 ? 0 : move1.Power / 2;
            var m2 = move2 == null && move2.Power > 0 ? 0 : move2.Power / 2;
            return p + cp + m1 + m2;
        }

        public static void Save(this FortDetailsResponse fortInfo, string filePath)
        {
            try
            {
                var data = new Xml.Pokestop();
                data.Id = fortInfo.FortId;
                data.Latitude = fortInfo.Latitude;
                data.Longitude = fortInfo.Longitude;
                data.Altitude = PokeRoadieClient.Current.CurrentLatitude;
                data.Name = fortInfo.Name;
                data.Description = fortInfo.Description;
                data.Fp = fortInfo.Fp;
                foreach (var img in fortInfo.ImageUrls)
                {
                    data.ImageUrls.Add(img);
                }
                Xml.Serializer.SerializeToFile(data, filePath);
            }
            catch (Exception e)
            {
                Logger.Write($"Could not save the pokestop information file for {fortInfo.FortId} - {e.ToString()}", LogLevel.Error);
            }
        }

        public static void Save(this GetGymDetailsResponse fortDetails, FortDetailsResponse fortInfo, string filePath)
        {
            //write data file
            try
            {
                var data = new Xml.Gym();
                data.Id = fortInfo.FortId;
                data.Latitude = fortDetails.GymState.FortData.Latitude;
                data.Longitude = fortDetails.GymState.FortData.Longitude;
                data.Altitude = PokeRoadieClient.Current.CurrentAltitude;
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
            catch (Exception e)
            {
                Logger.Write($"Could not save the gym information file for {fortInfo.FortId} - {e.ToString()}", LogLevel.Error);
            }
        }
    }
}
