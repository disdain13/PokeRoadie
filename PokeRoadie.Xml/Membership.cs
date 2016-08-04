using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace PokeRoadie.Xml
{
    [DataContract]
    public class Membership
    {
        [DataMember]
        public Player Player { get; set; } = new Player();
        [DataMember]
        public Pokemon Pokemon { get; set; } = new Pokemon();
    }
}
