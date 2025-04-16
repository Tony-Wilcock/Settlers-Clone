namespace PunkyFruitBat
{
    public class ButcherManager : BaseSpecificCharacterManager<Butcher>
    {
        public override CharacterType ManagedType => CharacterType.Butcher;

        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
