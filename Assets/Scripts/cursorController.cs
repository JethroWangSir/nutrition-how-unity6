using UnityEngine;

public class cursorController : MonoBehaviour
{
    public const int CursorTextureSize=64;
    private static readonly Vector2 CursorHotspotRatio=new Vector2(0.28f,0.18f);
    private static readonly Vector2 ClickedHotspotRatio=new Vector2(0.20f,0.20f);

    public Texture2D cursor,cursorClicked;
    private PlayerInputActions controls;
    private void Awake()
    {
        controls = new PlayerInputActions();
        ChangeCursor(cursor,false);
        Cursor.lockState = CursorLockMode.None;
    }
    private void OnEnable()
    {
        controls.Enable();
    }
    private void OnDisable()
    {
        controls.Disable();
    }
    private void OnDestroy()
    {
        if(controls != null)
        {
            controls.Dispose();
            controls = null;
        }
    }
    private void Start()
    {
        controls.UI.ClickForCursor.started += _ => StartedClick();
        controls.UI.ClickForCursor.performed += _ => EndedClick();
    }
    private void StartedClick()
    {
        ChangeCursor(cursorClicked,true);
    }
    private void EndedClick()
    {
        ChangeCursor(cursor,false);
    }
    private void ChangeCursor(Texture2D cursorType,bool isClicked)
    {
        if(cursorType==null)
        {
            Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);
            return;
        }

        Cursor.SetCursor(
            cursorType,
            GetHotspot(cursorType,isClicked),
            CursorMode.Auto);
    }
    public static Vector2 GetHotspot(Texture2D cursorTexture,bool isClicked)
    {
        if(cursorTexture==null)
        {
            return Vector2.zero;
        }

        Vector2 ratio=isClicked ? ClickedHotspotRatio : CursorHotspotRatio;
        return new Vector2(
            Mathf.Round(cursorTexture.width*ratio.x),
            Mathf.Round(cursorTexture.height*ratio.y));
    }
    private void OnApplicationFocus(bool hasFocus)
    {
        if(!hasFocus)
        {
            Cursor.visible=true;
        }
    }
}
