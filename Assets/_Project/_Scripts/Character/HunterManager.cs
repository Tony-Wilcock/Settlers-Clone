namespace PunkyFruitBat
{
    public class HunterManager : BaseSpecificCharacterManager<Hunter>
    {
        public override CharacterType ManagedType => CharacterType.Hunter;
        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
