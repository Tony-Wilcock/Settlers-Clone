using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class Carrier : Character
    {
        public CarrierManager CarrierManager { get; private set; }

        [field: SerializeField] public bool IsBusy { get; private set; } = false;
        public Resource CurrentResource { get; private set; } = null;
        public Resource ResourceToPickup { get; private set; } = null;
        public Path AssignedPath { get; private set; } = null;
        public Path newPath = null;
        public Flag DestinationFlag { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            // Cache the CarrierManager instance for efficiency
            if (HexGridManager.Instance != null && HexGridManager.Instance.CharacterManager != null)
            {
                CarrierManager = HexGridManager.Instance.CharacterManager.GetSpecificManager(CharacterType.Carrier) as CarrierManager;
                if (CarrierManager == null)
                {
                    Debug.LogError($"Carrier {GetInstanceID()} could not find CarrierManager!", this);
                }
            }
            else
            {
                Debug.LogError($"HexGridManager or CharacterManager not ready when Carrier {GetInstanceID()} awoke!", this);
            }
        }

        /// <summary>
        /// Assigns the path this carrier is responsible for. Called by CarrierManager.
        /// </summary>
        public void SetAssignedPath(Path path)
        {
            AssignedPath = path;
            WorkNodeIndex = AssignedPath.CenterNode;
            if (AssignedPath != null) gameObject.name = $"Carrier_Path_{AssignedPath.Id}";
            else gameObject.name = "Carrier_Unassigned";

            DestinationFlag = null;
        }

        /// <summary>
        /// Assigns a specific transport task to this carrier. Called by CarrierManager.
        /// </summary>
        public bool AssignTransportTask(Resource resource, Flag pickupFlag, Flag dropoffFlag)
        {
            if (IsBusy || resource == null || pickupFlag == null || dropoffFlag == null)
            {
                Debug.LogWarning($"Carrier {GetInstanceID()} cannot accept task. Busy: {IsBusy}, Resource: {resource?.name}, Pickup: {pickupFlag?.Id}, Dropoff: {dropoffFlag?.Id}");
                return false; // Cannot take task if busy or invalid args
            }

            ResourceToPickup = resource;

            if (IsMoving)
            {
                StartCoroutine(MoveToNextNodeThenPerformTransportTask(resource, pickupFlag, dropoffFlag));
                return true;
            }

            StartCoroutine(PerformTransportTask(resource, pickupFlag, dropoffFlag));
            return true;
        }

        private IEnumerator MoveToNextNodeThenPerformTransportTask(Resource resource, Flag pickupFlag, Flag dropoffFlag)
        {
            if (!IsMoving)
            {
                StartCoroutine(PerformTransportTask(resource, pickupFlag, dropoffFlag));
                yield break;
            }

            int targetNodeWeAreWaitingFor = NextNodeIndex;

            while (CurrentNodeIndex != targetNodeWeAreWaitingFor)
            {
                yield return null;
            }

            StopAllCoroutines();

            StartCoroutine(PerformTransportTask(resource, pickupFlag, dropoffFlag));
        }

        /// <summary>
        /// Coroutine performing the transport: move->pickup->move->dropoff->notify_idle.
        /// </summary>
        private IEnumerator PerformTransportTask(Resource resource, Flag pickupFlag, Flag dropoffFlag)
        {
            // 1. Move to Pickup Flag
            yield return StartCoroutine(PickupResource(resource, pickupFlag));

            // 2. Move to Dropoff Flag
            yield return StartCoroutine(DropoffResource(resource, dropoffFlag));
        }

        private IEnumerator PickupResource(Resource resource, Flag pickupFlag)
        {
            IsBusy = true;
            DestinationFlag = pickupFlag;
            yield return StartCoroutine(MoveCharacter(pickupFlag.Id));

            if (CurrentNodeIndex != pickupFlag.Id)
            {
                Debug.LogError($"Carrier {GetInstanceID()} failed to reach pickup flag {pickupFlag.Id}! Aborting task.", this);
                ResetCarrierState(); // Reset state if not busy
                yield break;
            }

            pickupFlag.RemoveResourceFromFlag(resource);
            resource.transform.SetParent(transform);
            resource.transform.localPosition = Vector3.up * 1.0f;
            CurrentResource = resource;
            ResourceToPickup = null; // Clear the resource to pickup
        }

        public IEnumerator DropoffResource(Resource resource, Flag dropoffFlag)
        {
            if (!dropoffFlag.gameObject.activeSelf)
            {
                dropoffFlag = CarrierManager.FindNextFlagOnRouteTowards(resource.CurrentFlag, resource.DestinationNodeIndex);
            }
            DestinationFlag = dropoffFlag;
            resource.CurrentFlag = null; // Clear the flag reference
            yield return StartCoroutine(MoveCharacter(dropoffFlag.Id));

            if (CurrentNodeIndex != dropoffFlag.Id)
            {
                Debug.LogError($"Carrier {GetInstanceID()} failed to reach dropoff flag {dropoffFlag.Id}! Aborting task.", this);
                // What to do with resource? Drop it? Return it?
                resource.transform.SetParent(null); // Drop it for now
                ResetCarrierState(); // Reset state if not busy
                yield break;
            }

            dropoffFlag.AddResourceToFlag(resource); // This notifies CarrierManager
            CurrentResource = null;
            IsBusy = false;
            DestinationFlag = null;

            if (newPath != null)
            {
                AssignedPath = newPath;
                newPath = null;
                WorkNodeIndex = AssignedPath.CenterNode;
                AssignedPath.SetCarrier(this);
                gameObject.name = $"Carrier_Path_{AssignedPath.Id}";
            }
            if (AssignedPath == null)
            {
                Debug.LogWarning($"Carrier {GetInstanceID()} has no assigned path/center! Returning home {WorkNodeIndex}.", this);
                manager.CharacterManager.ReturnCharacter(this);
                yield break;
            }

            if (manager.PathManager.GetPathAtNode(WorkNodeIndex) != AssignedPath)
            {
                Debug.LogWarning($"Carrier {GetInstanceID()} is not on the assigned path {AssignedPath.Id} at node {WorkNodeIndex}. Returning home.", this);
                manager.CharacterManager.ReturnCharacter(this);
                yield break;
            }
            yield return StartCoroutine(MoveCharacter(WorkNodeIndex));
        }

        protected override IEnumerator MoveAlongRoute(List<int> nodesOnPath)
        {
            IsMoving = true; // Set moving flag

            for (int i = 0; i < nodesOnPath.Count; i++)
            {
                NextNodeIndex = nodesOnPath[i]; // Set next node index
                Vector3 targetPosition = manager.NodeManager.GetNodePosition(NextNodeIndex);
                float arrivalThresholdSquared = 0.01f * 0.01f;
                while ((transform.position - targetPosition).sqrMagnitude > arrivalThresholdSquared)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = targetPosition;
                CurrentNodeIndex = NextNodeIndex; // Update current node index
                NextNodeIndex = -1; // Reset next node index

                if (CurrentResource != null || IsBusy) continue; // Skip flag checks if busy

                ResetCarrierState(); // Reset state if not busy
            }

            IsMoving = false; // Reset moving flag
        }

        // Helper to reset state
        public void ResetCarrierState()
        {
            if (CurrentResource != null) CurrentResource = null;
            if (IsBusy) IsBusy = false;
            if (DestinationFlag != null) DestinationFlag = null;

            CarrierManager.NotifyCarrierIdle(this); // Notify manager if idle
        }
    }
}
