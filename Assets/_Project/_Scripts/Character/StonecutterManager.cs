namespace PunkyFruitBat
{
    public class StonecutterManager : BaseSpecificCharacterManager<Stonecutter>
    {
        public override CharacterType ManagedType => CharacterType.Stonecutter;
        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
