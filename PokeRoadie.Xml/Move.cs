using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace PokeRoadie.Xml
{
    [DataContract]
    public class Move
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Type { get; set; }
        [DataMember]
        public string Category { get; set; }
        [DataMember]
        public string Effect { get; set; }
        [DataMember]
        public int Power { get; set; }
        [DataMember]
        public int Accuracy { get; set; }
        [DataMember]
        public int PP { get; set; }
        [DataMember]
        public string TM { get; set; }
        [DataMember]
        public int Hit { get; set; }
    }
}
