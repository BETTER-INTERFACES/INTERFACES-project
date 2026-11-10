using UnityEngine;
using UnityEngine.InputSystem;

public class FreeCam : MonoBehaviour
{
    [Header("Déplacements")]
    public float movementSpeed = 10f;
    public float fastMovementSpeed = 30f;
    public float freeLookSensitivity = 0.1f; // Sensibilité ajustée pour le nouveau système

    [Header("Contrôles (Nouveau Input System)")]
    private Vector2 moveInput;
    private Vector2 mouseInput;
    private float heightInput;
    private bool isBoosting;
    private bool isLooking;

    void Update()
    {
        // 1. Récupération des entrées via le clavier/souris actuel
        if (Keyboard.current != null)
        {
            // Déplacements ZQSD / Flèches (géré automatiquement par le layout du clavier)
            float moveX = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.qKey.isPressed ? 1f : 0f);
            float moveZ = (Keyboard.current.zKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);

            // Flèches directionnelles en secours
            if (moveX == 0 && moveZ == 0)
            {
                moveX = (Keyboard.current.rightArrowKey.isPressed ? 1f : 0f) - (Keyboard.current.leftArrowKey.isPressed ? 1f : 0f);
                moveZ = (Keyboard.current.upArrowKey.isPressed ? 1f : 0f) - (Keyboard.current.downArrowKey.isPressed ? 1f : 0f);
            }

            moveInput = new Vector2(moveX, moveZ);

            // Hauteur (Espace / Ctrl Gauche)
            heightInput = (Keyboard.current.spaceKey.isPressed ? 1f : 0f) - (Keyboard.current.leftCtrlKey.isPressed ? 1f : 0f);

            // Boost (Shift)
            isBoosting = Keyboard.current.leftShiftKey.isPressed;
        }

        if (Mouse.current != null)
        {
            // Gestion du clic droit
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                isLooking = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                isLooking = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Mouvement de la souris
            mouseInput = Mouse.current.delta.ReadValue();
        }

        // 2. Application des rotations (Regard)
        if (isLooking)
        {
            float newRotationX = transform.localEulerAngles.y + mouseInput.x * freeLookSensitivity;
            float newRotationY = transform.localEulerAngles.x - mouseInput.y * freeLookSensitivity;
            transform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
        }

        // 3. Application des déplacements
        float currentSpeed = isBoosting ? fastMovementSpeed : movementSpeed;

        Vector3 moveDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        transform.Translate(moveDir * currentSpeed * Time.deltaTime);

        // Application de la hauteur
        if (heightInput != 0)
        {
            transform.Translate(Vector3.up * heightInput * currentSpeed * Time.deltaTime, Space.World);
        }
    }
}
