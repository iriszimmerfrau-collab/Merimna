using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Input + physics helpers that work with both the legacy and the new Input System.</summary>
public static class GameInput
{
    public static bool AttackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    public static bool HugPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    // ---- menu input ----

    public static Vector2 MouseScreen()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    public static bool MouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    public static bool UpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
#endif
    }

    public static bool DownPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
#endif
    }

    public static bool ConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E);
#endif
    }

    public static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public static bool AnyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
#else
        return Input.anyKeyDown;
#endif
    }

    public static bool AdvancePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return (k != null && (k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#endif
    }

    public static bool YesPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.yKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Y);
#endif
    }

    public static bool NoPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.nKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public static bool CloseJournalPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public static bool LeftPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
#endif
    }

    public static bool RightPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
#endif
    }

    public static bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    public static Vector2 MouseWorld(Camera cam)
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 sp = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        Vector2 sp = Input.mousePosition;
#endif
        return cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -cam.transform.position.z));
    }

    public static void SetVelocity(Rigidbody2D rb, Vector2 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}
