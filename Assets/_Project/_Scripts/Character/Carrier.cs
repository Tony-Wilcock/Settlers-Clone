using System.Collections;
using UnityEngine;

namespace PunkyFruitBat
{
    public class Carrier : Character
    {
        [field: SerializeField] public bool IsBusy { get; set; } = false;

        protected override void Awake()
        {
            base.Awake();

            characterType = CharacterType.Carrier;
        }

        public IEnumerator RetrieveResource(Resource resource, Path path, Flag flag)
        {
            IsBusy = true;
            Flag otherFlag = path.Flag1 == this ? path.Flag2 : path.Flag1;
            yield return StartCoroutine(MoveCharacter(flag.Id, () =>
            {
                resource.transform.parent = transform;
                flag.RemoveResourceFromFlag(resource);
            }));

            yield return StartCoroutine(MoveCharacter(otherFlag.Id, () =>
            {
                resource.transform.parent = null;
                otherFlag.AddResourceToFlag(resource);
            }));

            IsBusy = false;
            yield return StartCoroutine(MoveCharacter(path.CenterNode));
        }
    }
}
