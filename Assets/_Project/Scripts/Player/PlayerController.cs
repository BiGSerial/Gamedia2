using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerJump))]
public class PlayerController : MonoBehaviour
{
    PlayerMovement movement;
    PlayerJump jump;

    float horizontalInput;
    bool sprintHeld;
    bool jumpPressed;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        jump     = GetComponent<PlayerJump>();
    }

    void Update()
    {
        // ----- Input (Keyboard) -----
        horizontalInput = 0f;
        sprintHeld = false;
        jumpPressed = false;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  horizontalInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontalInput += 1f;
            sprintHeld = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                jumpPressed = true;
        }

        // ----- Input (Gamepad) -----
        var gp = Gamepad.current;
        if (gp != null)
        {
            float stickX = gp.leftStick.ReadValue().x;
            if (Mathf.Abs(stickX) > Mathf.Abs(horizontalInput)) horizontalInput = stickX;
            sprintHeld = sprintHeld || gp.leftShoulder.isPressed || gp.rightTrigger.ReadValue() > 0.5f;
            if (gp.buttonSouth.wasPressedThisFrame) jumpPressed = true;
        }

        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);

        // Movimento
        movement.Tick(horizontalInput, sprintHeld);

        // Em vez de pular aqui, só BUFFERIZAMOS o clique.
        if (jumpPressed) jump.BufferJump();
    }
}
