using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject menuOptions;
    [SerializeField] private GameObject anyKeyText;

    private readonly List<GameObject> cachedTexts = new();

    private void Start()
    {
        
    }

    private void Update()
    {
        if (PressedThisFrame())
            ToggleMenu();
    }

    private bool PressedThisFrame()
    {
        // Teclado
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        // Mouse
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame ||
             Mouse.current.forwardButton.wasPressedThisFrame ||
             Mouse.current.backButton.wasPressedThisFrame ||
             Mouse.current.scroll.ReadValue().y != 0))
            return true;

        // Gamepads
       

      

        return false;
    }

    private void ToggleMenu()
    {
      
        menuOptions.SetActive(true);
        anyKeyText.SetActive(false);
        
    }
}
