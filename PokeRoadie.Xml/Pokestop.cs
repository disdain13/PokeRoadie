using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace PokeRoadie.Xml
{

    [DataContract]
    public class Pokestop
    {
        [DataMember]
        public string Id { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Description { get; set; }
        [DataMember(EmitDefaultValue = true, Name = "Lat")]
        public double Latitude { get; set; }
        [DataMember(EmitDefaultValue = true, Name = "Lng")]
        public double Longitude { get; set; }
        [DataMember(EmitDefaultValue = true, Name = "Alt")]
        public double Altitude { get; set; }
        [DataMember]
        public int Fp { get; set; }
        [DataMember]
        public List<string> ImageUrls { get; set; } = new List<string>(); 
    }
}
