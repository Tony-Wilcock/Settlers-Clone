namespace PunkyFruitBat
{
    public class GrainFarmerManager : BaseSpecificCharacterManager<GrainFarmer>
    {
        public override CharacterType ManagedType => CharacterType.GrainFarmer;
        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
