using POGOProtos.Inventory.Item;

namespace PokeRoadie
{
    public class ThrowData
    {
        public double NormalizedRecticleSize { get; set; }
        public double SpinModifier { get; set; }
        public ItemId ItemId { get; set; }
        public string HitText { get; set; }
        public string SpinText { get; set; }
        public string BallName { get; set; }
    }
}
