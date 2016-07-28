using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.GeneratedCode;

namespace PokemonGo.RocketAPI.Extensions
{
    public static class PokemonDataExtensions
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
        public static PokemonMoveDetail GetMove(this ICollection<PokemonMoveDetail> list, string name)
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
                Logging.Logger.Write($"Pokemon move '{name}' could not be found in the PokemonMoveDetails.xml file, using default move.", Logging.LogLevel.Error);
                move = new PokemonMoveDetail();
                move.Name = name;
                move.Power = 50;
                move.PP = 25;
                move.Type = "Normal";
                move.Category = "Physical";
                move.Effect = "Unknown";
                move.Accuracy = 15;
            }
                
            return move;
        }
        public static string ToString(this PokemonData pokemon, ISettings settings)
        {
            return $"{((pokemon.Favorite == 1 ? "*" : "") + pokemon.PokemonId.ToString()).PadRight(19,' ')} {PokemonInfo.CalculatePokemonValue(pokemon, settings.PokemonMoveDetails.GetMove(ToString(pokemon.Move1)), settings.PokemonMoveDetails.GetMove(ToString(pokemon.Move2)))} Total Value | {pokemon.Cp.ToString().PadLeft(4, ' ')} Cp | {pokemon.GetPerfection().ToString("0.00")}% Perfect | Lvl {pokemon.GetLevel().ToString("00")} | {ToString(pokemon.Move1)}/{ToString(pokemon.Move2)}";
        }
        public static string ToMinimizedString(this PokemonData pokemon, ISettings settings)
        {
            return $"{pokemon.PokemonId.ToString()} {PokemonInfo.CalculatePokemonValue(pokemon, settings.PokemonMoveDetails.GetMove(ToString(pokemon.Move1)), settings.PokemonMoveDetails.GetMove(ToString(pokemon.Move2)))} V | {pokemon.Cp.ToString().PadLeft(4, ' ')} Cp | {pokemon.GetPerfection().ToString("0.00")}% | Lvl {pokemon.GetLevel().ToString("00")}";
        }
        public static string ToString(this PokemonMove move)
        {
            var val = move.ToString();
            if (val.ToLower().EndsWith("fast")) val = val.Substring(0, val.Length - 4);
            return val;
        }
    }
}
