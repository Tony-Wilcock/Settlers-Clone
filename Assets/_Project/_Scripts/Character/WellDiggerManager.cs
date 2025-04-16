namespace PunkyFruitBat
{
    public class WellDiggerManager : BaseSpecificCharacterManager<WellDigger>
    {
        public override CharacterType ManagedType => CharacterType.WellDigger;
        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
