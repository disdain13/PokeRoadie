using System;
using System.Runtime.Serialization;
using System.Collections.Generic;


namespace PokeRoadie.Xml
{
    [DataContract]
    public class Gym : Pokestop
    {
        [DataMember]
        public long CooldownCompleteTimestampMs { get; set; }
        [DataMember]
        public bool Enabled { get; set; }
        [DataMember]
        public long GymPoints { get; set; }
        [DataMember]
        public long LastModifiedTimestampMs { get; set; }
        [DataMember]
        public string Team { get; set; }
        [DataMember]
        public string Sponsor { get; set; }
        [DataMember]
        public List<Membership> Memberships { get; set; } = new List<Membership>();

    }
}
