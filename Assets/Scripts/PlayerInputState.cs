using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Klavye/gamepad ve mobil ekran kontrollerini tek bir yerde birleştirir.
/// Mobil UI (MobileControlsUI) bu sınıfa değer yazar, PlayerController buradan okur.
/// </summary>
public static class PlayerInputState
{
    // Mobil UI tarafından yazılır
    public static Vector2 TouchMove;
    public static Vector2 TouchLookDelta; // bu karedeki piksel değişimi
    public static bool TouchRun;          // koşma butonu basılı mı (toggle değil, basılı tutma)
    public static bool TouchJumpPressed;  // bu karede zıplama basıldı

    public static Vector2 Move
    {
        get
        {
            Vector2 v = TouchMove;
            var kb = Keyboard.current;
            if (kb != null)
            {
                float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                float y = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                v += new Vector2(x, y);
            }
            var gp = Gamepad.current;
            if (gp != null) v += gp.leftStick.ReadValue();
            return Vector2.ClampMagnitude(v, 1f);
        }
    }

    public static Vector2 Look
    {
        get
        {
            Vector2 v = TouchLookDelta * 0.15f;
            var mouse = Mouse.current;
            // PC Editöründe doğrudan fare hareketiyle bakış (herhangi bir tuşa basmadan serbest dönüş)
            if (mouse != null)
            {
                v += mouse.delta.ReadValue() * 0.15f;
            }
            var gp = Gamepad.current;
            if (gp != null) v += gp.rightStick.ReadValue() * 150f * Time.deltaTime;
            return v;
        }
    }

    public static bool Run
    {
        get
        {
            if (TouchRun) return true;
            var kb = Keyboard.current;
            if (kb != null && kb.leftShiftKey.isPressed) return true;
            var gp = Gamepad.current;
            return gp != null && gp.leftStickButton.isPressed;
        }
    }

    public static bool JumpPressed
    {
        get
        {
            if (TouchJumpPressed) return true;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.buttonSouth.wasPressedThisFrame;
        }
    }

    /// <summary>Kare sonunda tek seferlik girdileri temizler.</summary>
    public static void EndFrame()
    {
        TouchJumpPressed = false;
        TouchLookDelta = Vector2.zero;
    }
}
