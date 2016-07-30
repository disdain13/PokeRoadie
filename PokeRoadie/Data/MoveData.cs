#region " Imports "

using System;

#endregion

namespace PokeRoadie
{
    [Serializable]
    public class MoveData
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
