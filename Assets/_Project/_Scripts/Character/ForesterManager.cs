namespace PunkyFruitBat
{
    public class ForesterManager : BaseSpecificCharacterManager<Forester>
    {
        public override CharacterType ManagedType => CharacterType.Forester;

        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
