using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Показывает модель экипированного оружия в WeaponSocket игрока.
/// Работает у ВСЕХ клиентов (не только у владельца) — подписывается на
/// PlayerInventory.WeaponEquippedChanged, который тоже срабатывает у всех,
/// потому что equippedWeaponId — синхронизированный NetworkVariable.
/// </summary>
public class PlayerWeaponVisual : NetworkBehaviour
{
    [Header("Точки крепления")]
    [SerializeField] private Transform weaponSocket;
    [Tooltip("Заглушка-кубик, которая изображает 'безоружного' игрока. " +
             "Прячется, когда экипирована модель настоящего оружия.")]
    [SerializeField] private GameObject unarmedPlaceholder;

    private PlayerInventory inventory;
    private GameObject currentModelInstance;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
    }

    public override void OnNetworkSpawn()
    {
        if (inventory == null) return;

        inventory.WeaponEquippedChanged += ApplyWeaponVisual;

        // На случай, если PlayerInventory уже успел выставить значение
        // до того, как мы подписались (порядок OnNetworkSpawn между
        // компонентами не гарантирован).
        ApplyWeaponVisual(inventory.equippedWeaponId.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (inventory != null)
        {
            inventory.WeaponEquippedChanged -= ApplyWeaponVisual;
        }
    }

    private void ApplyWeaponVisual(int weaponId)
    {
        if (currentModelInstance != null)
        {
            Destroy(currentModelInstance);
            currentModelInstance = null;
        }

        WeaponDefinition weapon = weaponId != PlayerInventory.NoWeaponId && WeaponDatabase.Instance != null
            ? WeaponDatabase.Instance.GetById(weaponId)
            : null;

        bool hasRealModel = weapon != null && weapon.HandModelPrefab != null && weaponSocket != null;

        if (hasRealModel)
        {
            currentModelInstance = Instantiate(weapon.HandModelPrefab, weaponSocket);
            currentModelInstance.transform.localPosition = Vector3.zero;
            // localRotation больше не трогаем — сохраняется поворот,
            // заданный на самом префабе handModelPrefab (авторский разворот оружия).
        }

        if (unarmedPlaceholder != null)
        {
            unarmedPlaceholder.SetActive(!hasRealModel);
        }
    }
}