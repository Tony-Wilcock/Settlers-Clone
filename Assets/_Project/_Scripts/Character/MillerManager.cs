namespace PunkyFruitBat
{
    public class MillerManager : BaseSpecificCharacterManager<Miller>
    {
        public override CharacterType ManagedType => CharacterType.Miller;
        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
