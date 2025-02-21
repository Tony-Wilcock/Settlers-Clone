using System.Collections.Generic;
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

[RequireComponent(typeof(CharacterController))]
public class Character : MonoBehaviour
{
    [SerializeField] private CharacterType characterType;
    [SerializeField] private float speed = 5f;
    [SerializeField] private float distanceThreshold = 5f;
    [SerializeField] private GameObject hq;

    [SerializeField] private GridNodeManager gridNodeManager;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private NodeManager nodeManager;
    [SerializeField] private PathManager pathManager;
    [SerializeField] private NodeSelection nodeSelection;

    private List<Vector3> targets;
    private bool hasTarget = false;

    public CharacterType CharacterType 
    {
        get => characterType;
        set => characterType = value;
    }

    public float Speed
    {
        get => speed;
        set => speed = value;
    }

    public GameObject HQ
    {
        get => hq;
        set => hq = value;
    }

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public void Initialise(
        GridNodeManager gridNodeManager,
        GridManager gridManager,
        NodeManager nodeManager,
        PathManager pathManager,
        NodeSelection nodeSelection)
    {
        this.gridNodeManager = gridNodeManager;
        this.gridManager = gridManager;
        this.nodeManager = nodeManager;
        this.pathManager = pathManager;
        this.nodeSelection = nodeSelection;

        //Instantiate(hq, hq.transform.position, Quaternion.identity);
    }

    private void Update()
    {
        if (hasTarget)
        {

            Vector3 direction = (targets[0] - transform.position).normalized;

            Move(direction);

            if (Vector3.Distance(transform.position, targets[0] + Vector3.up * 1) < distanceThreshold)
            {
                targets.RemoveAt(0);

                if (targets.Count == 0)
                    hasTarget = false;
            }
        }
    }

    private void Move(Vector3 direction)
    {
        // Move the character
        characterController.Move(direction * speed * Time.deltaTime);
    }

    public void SetTarget(List<Vector3> targets)
    {
        this.targets = targets;
        hasTarget = true;
    }
}
