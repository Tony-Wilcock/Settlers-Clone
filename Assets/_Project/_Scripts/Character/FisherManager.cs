namespace PunkyFruitBat
{
    public class FisherManager : BaseSpecificCharacterManager<Fisher>
    {
        public override CharacterType ManagedType => CharacterType.Fisher;
        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
