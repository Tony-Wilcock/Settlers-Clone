using System;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "Inputs", menuName = "Scriptable Objects/Inputs")]
public class Input_SO : ScriptableObject, PlayerInput.IPlayerActions
{
    public event Action<Vector2> OnMoveAction;
    public event Action<Vector2> OnMouseDeltaAction;
    public event Action<float> OnRotateAction;
    public event Action<float> OnZoomAction;
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

    public void OnMove(InputAction.CallbackContext context)
    {
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

    public void OnRotate(InputAction.CallbackContext context)
    {
        OnRotateAction?.Invoke(context.ReadValue<float>());
    }

    public void OnMouseDelta(InputAction.CallbackContext context)
    {
        OnMouseDeltaAction?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnZoom(InputAction.CallbackContext context)
    {
        OnZoomAction?.Invoke(context.ReadValue<float>());
    }
}
