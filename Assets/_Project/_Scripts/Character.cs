using UnityEngine;

public enum CharacterType
{
    None,
    Carrier,
    Builder,
    Planner,
    WoodCutter,
    Forester,
    Carpenter,
    Fisher,
    Hunter,
    Stonemason,
    Farmer,
    Miller,
    Baker,
    PigBreeder,
    Butcher,
    WaterWellDigger,
}

public abstract class Character : MonoBehaviour
{
    [SerializeField] protected CharacterType characterType;
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected int currentNode;
}
