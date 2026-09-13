using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class TopDownPlayerController : NetworkBehaviour
{
    [Header("Передвижение")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float gravity = -20f;

    [Header("Камера")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 16f, -10f);
    [SerializeField] private float cameraAngle = 60f;

    private CharacterController controller;
    private Vector3 verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // Отключаем камеру и микрофон чужого персонажа
        if (!IsOwner)
        {
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);
            return;
        }

        // Для топ-дауна курсор мыши должен быть видимым и свободным
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
        HandleAiming();
    }

    private void LateUpdate()
    {
        if (!IsOwner || playerCamera == null) return;

        // Фиксируем позицию и угол камеры над персонажем, исключая вращение от прицеливания
        playerCamera.transform.position = transform.position + cameraOffset;
        playerCamera.transform.rotation = Quaternion.Euler(cameraAngle, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
        }

        Vector3 move = new Vector3(input.x, 0f, input.y).normalized;

        if (controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }

        controller.Move((move * moveSpeed + verticalVelocity) * Time.deltaTime);
    }

    private void HandleAiming()
    {
        if (Mouse.current == null || playerCamera == null) return;

        // Пускаем луч из камеры через положение курсора
        Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        // Создаем математическую плоскость на высоте персонажа для расчета точки взгляда
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

        if (groundPlane.Raycast(ray, out float enterDistance))
        {
            Vector3 worldHitPoint = ray.GetPoint(enterDistance);
            Vector3 lookDirection = worldHitPoint - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.05f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }
}