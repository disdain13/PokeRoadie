using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokemonGo.RocketAPI
{
    [Serializable]
    public class PokemonMoveDetail
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public string Effect { get; set; }
        public int Power { get; set; }
        public int Accuracy { get; set; }
        public int PP { get; set; }
        public string TM { get; set; }
        public int Hit { get; set; }
    }
}
