namespace PunkyFruitBat
{
    public class BakerManager : BaseSpecificCharacterManager<Baker>
    {
        public override CharacterType ManagedType => CharacterType.Baker;

        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
