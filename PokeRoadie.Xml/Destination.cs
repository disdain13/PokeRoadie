using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace PokeRoadie.Xml
{
    [DataContract]
    public class Destination
    {
        [DataMember(EmitDefaultValue = true, IsRequired = false)]
        public string Name { get; set; }
        [DataMember(EmitDefaultValue = true, Name = "Lat")]
        public double Latitude { get; set; }
        [DataMember(EmitDefaultValue = true, Name = "Lng")]
        public double Longitude { get; set; }
        [DataMember(EmitDefaultValue = true, Name = "Alt")]
        public double Altitude { get; set; }

    }
}
