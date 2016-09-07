#region " Imports "

using System;
using System.Collections.Generic;
using System.Linq;

using PokemonGo.RocketAPI;
using PokemonGo.RocketAPI.Logging;
using PokemonGo.RocketAPI.Helpers;

using POGOProtos.Data;
using POGOProtos.Enums;

using PokeRoadie.Extensions;
using POGOProtos.Networking.Responses;


#endregion

namespace PokeRoadie
{
    public class Utility
    {
        public Context Context { get; set; }

        public Utility(Context context)
        {
            Context = context;
        }

        #region " Stats methods "

        public string GetStats(PokemonData pokemon)
        {
            return $"{((pokemon.Favorite == 1 ? "*" : "") + pokemon.PokemonId.ToString()).PadRight(19)} {CalculatePokemonValue(pokemon).ToString().PadRight(3)} V | {pokemon.Cp.ToString().PadLeft(4)} Cp | {pokemon.GetPerfection().ToString("0.00").PadLeft(6)}% IV | Lvl {pokemon.GetLevel().ToString("00")} | {(pokemon.Stamina.ToString() + "/" + pokemon.StaminaMax.ToString() + " Hp").PadLeft(10)} | {pokemon.IndividualAttack.ToString("00").PadLeft(2)} A | {pokemon.IndividualDefense.ToString("00").PadLeft(2)} D | {pokemon.IndividualStamina.ToString("00").PadLeft(2)} S | {GetMoveName(pokemon.Move1).PadRight(14)}{CalculateMoveValue(GetMoveName(pokemon.Move1))} | {GetMoveName(pokemon.Move2).PadRight(14)}{CalculateMoveValue(GetMoveName(pokemon.Move2))}";
        }

        public string GetMinStats(PokemonData pokemon)
        {
            var name = pokemon.PokemonId.ToString();
            if (name.Length > 10) name = name.Substring(0, 10);
            return $"{(String.IsNullOrWhiteSpace(pokemon.DeployedFortId) ? "" : "^") + (pokemon.Favorite == 1 ? "*" : "")}{name} " + $"({CalculatePokemonValue(pokemon)}V-{pokemon.Cp.ToString()}Cp-{pokemon.GetPerfection().ToString("0.00")}%-Lv{pokemon.GetLevel().ToString("00")}-{pokemon.StaminaMax.ToString()}Hp)";
        }

        #endregion
        #region " Move Extensions "
        public MoveData GetMove(ICollection<MoveData> list, string name)
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

        public string GetMoveName(PokemonMove move)
        {
            var val = move.ToString();
            if (val.ToLower().EndsWith("fast")) val = val.Substring(0, val.Length - 4);
            return val;
        }

        #endregion
        #region " True Value Extensions "

        public double CalculatePokemonValue(PokemonData pokemon)
        {
            var p = System.Convert.ToInt32(PokemonInfo.CalculatePokemonPerfection(pokemon) * 1.5);
            var cp = Convert.ToInt32(pokemon.Cp == 0 ? 0 : pokemon.Cp / 2000 * 100);
            var m1 = CalculateMoveValue(GetMoveName(pokemon.Move1)) * .5;
            var m2 = CalculateMoveValue(GetMoveName(pokemon.Move2)) * .5;
            var l = (pokemon.GetLevel() == 0 ? 0 : pokemon.GetLevel() * 3.5);
            return Math.Round(p + cp + m1 + m2 + l, 0);
        }
        public int CalculateMoveValue(string moveName)
        {
            var m1a = 100;
            var move1 = GetMove(Context.Settings.PokemonMoves, moveName);

            if (move1 == null) return 20;
            m1a = move1.Power + move1.Accuracy + move1.Hit;
            m1a = m1a < 51 ? 50 : m1a > 200 ? 200 : m1a;
            double m1b = (move1.PP > 0 && move1.PP < 15) ?
                3.0d : 4.0d;
            return Convert.ToInt32(m1a / m1b);
        }

        #endregion
        #region " Xlo Extesnions "

        public void Save(FortDetailsResponse fortInfo, string filePath, double currentAltitude)
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

        public void Save(GetGymDetailsResponse fortDetails, FortDetailsResponse fortInfo, string filePath, double currentAltitude)
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
                        m.Pokemon.V = CalculatePokemonValue(membership.PokemonData);
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

        public void Save(PokeRoadieInventory inventory, PokemonData pokemon, GeoCoordinate geo, string playerName, int playerLevel, string playerTeam, ulong encounterId, EncounterSourceTypes encounterType, string filePath)
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
                    V = CalculatePokemonValue(pokemon),
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
    }
}