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
        public static PokemonMoveDetail GetMove(this List<PokemonMoveDetail> list, string name)
        {
            var move = list.Where(x => x.Name.Replace(" ", "") == name).FirstOrDefault();
            if (move == null)
                Logging.Logger.Write("Pokemon move '{name}' could not be found in the PokemonMoveDetails.xml file.", Logging.LogLevel.Error);
            return move;
        }
    }
}
