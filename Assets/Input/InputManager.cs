using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public delegate void GenericEmptyCallback();
public delegate void OverUIChanged(bool newValue);

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    private MainInput mainControls;
    public MainInput MainControls => mainControls;
    public MainInput.PlayerActions PlayerActions => mainControls.Player;

    public bool overUI = false;
    public OverUIChanged OnOverUIChanged;

    public bool leftMouseDown = false;
    public GenericEmptyCallback OnLeftMouseDown;
    public GenericEmptyCallback OnLeftMouseDownUI;
    public GenericEmptyCallback OnLeftMouseHeld;
    public GenericEmptyCallback OnLeftMouseHeldUI;
    public GenericEmptyCallback OnLeftMouseUp;
    public GenericEmptyCallback OnLeftMouseUpUI;
    private Coroutine leftMouseProcess;

    public bool IsOverUI
    {
        get => overUI;
        private set
        {
            if (overUI != value)
            {
                overUI = value;
                OnOverUIChanged?.Invoke(value);
            }
        }
    }

    private void Awake()
    {
        Instance = this;
        mainControls = new MainInput();
        PlayerActions.Enable();
        PlayerActions.LeftMouse.started += MALeftMouseDown;
        PlayerActions.LeftMouse.canceled += MALeftMouseUp;
    }

    private void Update()
    {
        IsOverUI = EventSystem.current.IsPointerOverGameObject();
    }

    private void MALeftMouseDown(InputAction.CallbackContext context)
    {
        leftMouseDown = true;
        OnLeftMouseDownUI?.Invoke();
        if (!IsOverUI)
        {
            OnLeftMouseDown?.Invoke();
        }
        if (leftMouseProcess != null)
        {
            StopCoroutine(leftMouseProcess);
        }
        leftMouseProcess = StartCoroutine(MALeftMouseHeld());
    }

    private IEnumerator MALeftMouseHeld()
    {
        while (true)
        {
            yield return null;
            OnLeftMouseHeld?.Invoke();
            if (!IsOverUI)
            {
                OnLeftMouseHeldUI?.Invoke();
            }
        }
    }

    private void MALeftMouseUp(InputAction.CallbackContext context)
    {
        if (leftMouseProcess != null)
        {
            StopCoroutine(leftMouseProcess);
            leftMouseProcess = null;
        }
        leftMouseDown = false;
        OnLeftMouseUpUI?.Invoke();
        if (!IsOverUI)
        {
            OnLeftMouseUp?.Invoke();
        }
    }

}
