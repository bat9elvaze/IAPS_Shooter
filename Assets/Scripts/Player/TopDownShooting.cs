using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownShooting : NetworkBehaviour
{
    [Header("Точка вылета пуль")]
    [SerializeField] private Transform firePoint;

    // Слот 1 -> Digit1, Слот 2 -> Digit2 — совпадает с двумя слотами инвентаря
    private static readonly Key[] SlotKeys = { Key.Digit1, Key.Digit2 };

    private static bool warnedAboutNetworkBullet;

    private PlayerInventory inventory;
    private PlayerInventoryUI inventoryUI;
    private float nextFireTime;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        inventoryUI = GetComponent<PlayerInventoryUI>();
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (inventoryUI != null && inventoryUI.IsOpen) return; // открыт инвентарь
        if (ArsenalStation.IsOpenForLocalPlayer) return;        // открыта станция "Арсенал"

        HandleWeaponSwitchInput();
        HandleFireInput();
    }

    private void HandleFireInput()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.isPressed) return;
        if (Time.time < nextFireTime) return;

        WeaponDefinition weapon = GetEquippedWeaponDefinition();
        if (weapon == null) return; // оружие не экипировано — стрелять нечем

        nextFireTime = Time.time + weapon.FireRate;
        Shoot(weapon);
    }

    private void HandleWeaponSwitchInput()
    {
        if (Keyboard.current == null || inventory == null) return;

        for (int slotIndex = 0; slotIndex < SlotKeys.Length; slotIndex++)
        {
            if (Keyboard.current[SlotKeys[slotIndex]].wasPressedThisFrame)
            {
                inventory.EquipSlotServerRpc(slotIndex);
                break;
            }
        }
    }

    private void Shoot(WeaponDefinition weapon)
    {
        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : transform.position + transform.forward * 1.2f + Vector3.up * 0.5f;

        Vector3 direction = transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        // Клиент сразу показывает свою пулю, не дожидаясь сервера (предсказание).
        // Она не наносит урон, поэтому damageSource ей не нужен (null).
        if (!IsServer)
        {
            SpawnBullet(spawnPos, direction, false, weapon, null);
        }

        FireServerRpc(spawnPos, direction);
    }

    [Rpc(SendTo.Server)]
    private void FireServerRpc(Vector3 position, Vector3 direction)
    {
        // Оружие берём из СВОЕЙ (серверной, авторитетной) копии инвентаря,
        // а не доверяем клиенту — иначе можно было бы подделать урон или снаряд.
        WeaponDefinition weapon = GetEquippedWeaponDefinition();
        if (weapon == null) return;

        // Это боевая пуля — передаём ей инвентарь стрелка (это "this.inventory",
        // ведь FireServerRpc выполняется на сервере именно в копии того игрока,
        // который стрелял), чтобы при убийстве моба начислить валюту правильному игроку.
        SpawnBullet(position, direction, true, weapon, inventory);
        FireVisualRpc(position, direction);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void FireVisualRpc(Vector3 position, Vector3 direction)
    {
        if (IsServer) return; // у сервера/хоста уже есть боевая пуля
        if (IsOwner) return;  // у стрелка уже есть своя предсказанная пуля

        WeaponDefinition weapon = GetEquippedWeaponDefinition();
        if (weapon == null) return;

        SpawnBullet(position, direction, false, weapon, null);
    }

    private WeaponDefinition GetEquippedWeaponDefinition()
    {
        if (inventory == null) return null;

        int weaponId = inventory.equippedWeaponId.Value;
        if (weaponId == PlayerInventory.NoWeaponId) return null;

        return WeaponDatabase.Instance != null ? WeaponDatabase.Instance.GetById(weaponId) : null;
    }

    private void SpawnBullet(Vector3 position, Vector3 direction, bool authoritative, WeaponDefinition weapon, PlayerInventory damageSource)
    {
        if (weapon.BulletPrefab == null) return;
        if (direction.sqrMagnitude < 0.0001f) return;

        GameObject instance = Instantiate(weapon.BulletPrefab, position, Quaternion.LookRotation(direction));

        if (!warnedAboutNetworkBullet && instance.GetComponent<NetworkObject>() != null)
        {
            warnedAboutNetworkBullet = true;
            Debug.LogError("[TopDownShooting] На префабе пули остался NetworkObject/NetworkTransform. " +
                           "Удалите их (сначала NetworkTransform, потом NetworkObject) — пуля теперь не сетевой объект.", weapon.BulletPrefab);
        }

        if (instance.TryGetComponent<Bullet>(out var bullet))
        {
            bullet.Init(authoritative, weapon.Damage, damageSource);
        }
    }
}