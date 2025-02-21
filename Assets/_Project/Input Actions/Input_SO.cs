using System;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "Inputs", menuName = "Scriptable Objects/Inputs")]
public class Input_SO : ScriptableObject, PlayerInput.IPlayerActions
{
    public event Action<Vector2> OnMoveAction;
    public event Action OnInteractAction;
    public event Action OnCheckCellAction;

    public PlayerInput playerInput;

    public void OnEnable()
    {
        if (playerInput == null)
        {
            playerInput = new PlayerInput();
            playerInput.Player.SetCallbacks(this);
        }
        playerInput.Enable();
    }

    public void OnDisable()
    {
        playerInput.Disable();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnInteractAction?.Invoke();
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {

    }

    public void OnMove(InputAction.CallbackContext context)
    {
        Debug.Log(context.ReadValue<Vector2>());
        OnMoveAction?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnNext(InputAction.CallbackContext context)
    {

    }

    public void OnPrevious(InputAction.CallbackContext context)
    {

    }

    public void OnCheckCell(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnCheckCellAction?.Invoke();
        }
    }
}
