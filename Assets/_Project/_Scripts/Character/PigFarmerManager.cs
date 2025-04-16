namespace PunkyFruitBat
{
    public class PigFarmerManager : BaseSpecificCharacterManager<PigFarmer>
    {
        public override CharacterType ManagedType => CharacterType.PigFarmer;
        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
