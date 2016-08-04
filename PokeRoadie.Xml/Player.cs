using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace PokeRoadie.Xml
{
    [DataContract]
    public class Player
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public int Level { get; set; }
        [DataMember]
        public List<Pokemon> Pokemon { get; set; } = new List<Pokemon>();
    }
}
