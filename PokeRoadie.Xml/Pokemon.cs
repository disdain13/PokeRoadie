using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace PokeRoadie.Xml
{
    [DataContract]
    public class Pokemon
    {
        [DataMember]
        public ulong Id { get; set; }
        [DataMember]
        public string Type { get; set; }
        [DataMember]
        public string Nickname { get; set; }
        [DataMember]
        public int Cp { get; set; }
        [DataMember]
        public int Favorite { get; set; }
        [DataMember]
        public float HeightM { get; set; }
        [DataMember]
        public float WeightKg { get; set; }
        [DataMember]
        public string Move1 { get; set; }
        [DataMember]
        public string Move2 { get; set; }
        [DataMember]
        public int IndividualAttack { get; set; }
        [DataMember]
        public int IndividualDefense { get; set; }
        [DataMember]
        public int IndividualStamina { get; set; }
        [DataMember]
        public bool IsEgg { get; set; }
        [DataMember]
        public int NumUpgrades { get; set; }
        [DataMember]
        public int Origin { get; set; }
        [DataMember]
        public string Pokeball { get; set; }
        [DataMember]
        public int BattlesAttacked { get; set; }
        [DataMember]
        public int BattlesDefended { get; set; }
    }
}
