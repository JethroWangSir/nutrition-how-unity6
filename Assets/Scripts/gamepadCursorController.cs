using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

public class gamepadCursorController : MonoBehaviour
{
    [SerializeField]
    private PlayerInput playerInput;
    [SerializeField]
    private RectTransform cursorTransform;
    [SerializeField]
    private Canvas canvas;
    [SerializeField]
    private RectTransform canvasRectTransform;
    [SerializeField]
    private float cursorSpeed = 1000f;
    [SerializeField]
    private float paddind = 10f;
    private bool previousMouseState;
    private Mouse virtualMouse;
    private Mouse currentMouse;
    private Camera mainCamera;
    private bool gamepadCursorActive;
    private const string gamepadScheme = "Gamepad";
    private const string mouseScheme = "Keyboard&Mouse";
    private const float activationThreshold = 0.35f;
    private void OnEnable()
    {
        mainCamera = Camera.main;
        currentMouse = Mouse.current;
        if(virtualMouse == null)
        {
            virtualMouse = (Mouse)InputSystem.AddDevice("VirtualMouse");
        }else if(!virtualMouse.added)
        {
            InputSystem.AddDevice(virtualMouse);
        }
        if(playerInput != null && playerInput.user.valid)
        {
            InputUser.PerformPairingWithDevice(virtualMouse, playerInput.user);
        }
        Vector2 initialPosition=currentMouse!=null
            ? currentMouse.position.ReadValue()
            : new Vector2(Screen.width*0.5f,Screen.height*0.5f);
        SetVirtualCursorPosition(initialPosition);
        UseMouseCursor(false);
        InputSystem.onAfterUpdate += UpdateMotion;
        if(playerInput != null)
        {
            playerInput.onControlsChanged += OnControlsChanged;
        }
    }
    private void OnDisable()
    {
        InputSystem.onAfterUpdate -= UpdateMotion;
        if(playerInput != null)
        {
            playerInput.onControlsChanged -= OnControlsChanged;
        }
        if(virtualMouse != null && virtualMouse.added)
        {
            InputSystem.RemoveDevice(virtualMouse);
        }
        UseMouseCursor(false);
    }
    private void UpdateMotion()
    {
        if(virtualMouse == null || Gamepad.current == null)
        {
            return;
        }
        Vector2 stickValue=Gamepad.current.rightStick.ReadValue();
        bool aButtonIsPressed = Gamepad.current.aButton.IsPressed();
        if(!gamepadCursorActive && ShouldActivateGamepadCursor(
               playerInput!=null ? playerInput.currentControlScheme : null,
               stickValue,
               Gamepad.current.aButton.wasPressedThisFrame))
        {
            UseGamepadCursor();
        }
        if(!gamepadCursorActive)
        {
            previousMouseState=aButtonIsPressed;
            return;
        }

        Vector2 deltaValue = stickValue*cursorSpeed*Time.unscaledDeltaTime;
        Vector2 currentPosition = virtualMouse.position.ReadValue();
        Vector2 newPosition = ClampToSafeArea(currentPosition + deltaValue);
        InputState.Change(virtualMouse.position, newPosition);
        InputState.Change(virtualMouse.delta, deltaValue);
        if(previousMouseState != aButtonIsPressed)
        {
            virtualMouse.CopyState<MouseState>(out var mouseState);
            mouseState.WithButton(MouseButton.Left, aButtonIsPressed);
            InputState.Change(virtualMouse, mouseState);
            previousMouseState = aButtonIsPressed;
        }
        AnchorCursor(newPosition);
    }
    public static bool ShouldActivateGamepadCursor(
        string controlScheme,
        Vector2 stickValue,
        bool submitPressedThisFrame)
    {
        return controlScheme==gamepadScheme &&
               (stickValue.sqrMagnitude>=activationThreshold*activationThreshold ||
                submitPressedThisFrame);
    }
    private void AnchorCursor(Vector2 position)
    {
        if(cursorTransform==null || canvasRectTransform==null || canvas==null)
        {
            return;
        }

        Vector2 anchoredPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, position, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera, out anchoredPosition);
        cursorTransform.anchoredPosition = anchoredPosition;
    }
    private void OnControlsChanged(PlayerInput input)
    {
        if(input.currentControlScheme==mouseScheme)
        {
            UseMouseCursor(true);
        }else if(input.currentControlScheme!=gamepadScheme)
        {
            UseMouseCursor(false);
        }
    }
    private void UseGamepadCursor()
    {
        Vector2 position=currentMouse!=null
            ? currentMouse.position.ReadValue()
            : virtualMouse.position.ReadValue();
        SetVirtualCursorPosition(position);
        if(cursorTransform!=null)
        {
            cursorTransform.gameObject.SetActive(true);
        }
        Cursor.visible=false;
        gamepadCursorActive=true;
    }
    private void UseMouseCursor(bool preserveGamepadPosition)
    {
        if(preserveGamepadPosition && gamepadCursorActive && currentMouse!=null && virtualMouse!=null)
        {
            currentMouse.WarpCursorPosition(virtualMouse.position.ReadValue());
        }
        if(cursorTransform!=null)
        {
            cursorTransform.gameObject.SetActive(false);
        }
        Cursor.visible=true;
        gamepadCursorActive=false;
        previousMouseState=false;
    }
    private void SetVirtualCursorPosition(Vector2 position)
    {
        if(virtualMouse==null)
        {
            return;
        }

        Vector2 clampedPosition=ClampToSafeArea(position);
        InputState.Change(virtualMouse.position,clampedPosition);
        InputState.Change(virtualMouse.delta,Vector2.zero);
        AnchorCursor(clampedPosition);
    }
    private Vector2 ClampToSafeArea(Vector2 position)
    {
        Rect safeArea=LegacyDisplayAdapter.SafeAreaPixels;
        if(safeArea.width<=0f || safeArea.height<=0f)
        {
            safeArea=new Rect(0f,0f,Screen.width,Screen.height);
        }
        position.x=Mathf.Clamp(position.x,safeArea.xMin+paddind,safeArea.xMax-paddind);
        position.y=Mathf.Clamp(position.y,safeArea.yMin+paddind,safeArea.yMax-paddind);
        return position;
    }
}
