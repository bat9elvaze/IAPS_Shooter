using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Инвентарь оружия и кошелёк игрока. ownedWeaponIds — полный каталог того,
/// что игрок когда-либо получил ("есть/нет"). loadoutSlot1/2WeaponId — какое
/// оружие сейчас назначено в каждый из двух слотов (одно оружие — только в
/// одном слоте одновременно: назначение в новый слот убирает его из старого).
/// activeSlotIndex — какой слот сейчас "в руках". equippedWeaponId всегда
/// зеркалит содержимое activeSlotIndex и пересчитывается сервером при любом
/// изменении активного слота ИЛИ содержимого слотов. currency — валюта
/// игрока, начисляется сервером (EnemyHealth при убийстве, покупки, кейсы).
/// Всё изменяется только сервером, через ServerRpc/серверные методы.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    public const int NoWeaponId = -1;
    public const int NoSlot = -1;
    public const int SlotCount = 2;

    [Header("Стартовое оружие (для тестов)")]
    [Tooltip("Первый элемент попадает в Слот 1 (и сразу экипируется), второй — в Слот 2. " +
             "Остальные элементы (если есть) просто окажутся в инвентаре как полученные, " +
             "но не в слоте. Очисти список, когда появится станция \"Арсенал\".")]
    [SerializeField] private int[] startingWeaponIds = new int[0];

    [Header("Стартовая валюта (для тестов)")]
    [Tooltip("Сколько монет у игрока сразу при спавне. Удобно для проверки магазина, " +
             "не дожидаясь убийств. Поставь 0, чтобы честно проверить начисление за килл.")]
    [SerializeField] private int startingCurrency = 0;

    [Header("Отладка")]
    [Tooltip("Писать в консоль при изменении инвентаря/экипировки/кейсов")]
    [SerializeField] private bool logChanges = false;

    public NetworkList<int> ownedWeaponIds = new NetworkList<int>();

    public NetworkVariable<int> loadoutSlot1WeaponId = new NetworkVariable<int>(
        NoWeaponId, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> loadoutSlot2WeaponId = new NetworkVariable<int>(
        NoWeaponId, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Какой слот сейчас "в руках" (0, 1 или NoSlot).</summary>
    public NetworkVariable<int> activeSlotIndex = new NetworkVariable<int>(
        NoSlot, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> equippedWeaponId = new NetworkVariable<int>(
        NoWeaponId, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Валюта игрока. Меняется только сервером (AddCurrency, BuyWeaponServerRpc, OpenCaseServerRpc).</summary>
    public NetworkVariable<int> currency = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Срабатывает у ВСЕХ клиентов при смене экипированного оружия.</summary>
    public event Action<int> WeaponEquippedChanged;

    /// <summary>
    /// Срабатывает ТОЛЬКО у владельца после открытия кейса: id выпавшего оружия,
    /// был ли это дубликат, и сколько валюты вернулось, если да (0, если не дубликат).
    /// </summary>
    public event Action<int, bool, int> CaseOpened;

    public override void OnNetworkSpawn()
    {
        equippedWeaponId.OnValueChanged += HandleEquippedChanged;
        ownedWeaponIds.OnListChanged += HandleListChanged;

        if (IsServer)
        {
            currency.Value = startingCurrency;

            for (int i = 0; i < startingWeaponIds.Length; i++)
            {
                int weaponId = startingWeaponIds[i];
                if (weaponId == NoWeaponId) continue;

                if (WeaponDatabase.Instance == null || WeaponDatabase.Instance.GetById(weaponId) == null)
                {
                    Debug.LogWarning($"[PlayerInventory] Попытка выдать неизвестное стартовое оружие с id {weaponId}.");
                    continue;
                }

                if (!HasWeapon(weaponId))
                {
                    ownedWeaponIds.Add(weaponId);
                }

                if (i == 0) loadoutSlot1WeaponId.Value = weaponId;
                else if (i == 1) loadoutSlot2WeaponId.Value = weaponId;
            }

            activeSlotIndex.Value = loadoutSlot1WeaponId.Value != NoWeaponId ? 0 : NoSlot;
            RefreshEquippedFromActiveSlot();
        }

        // OnValueChanged не срабатывает на начальное значение при спавне,
        // поэтому инициализируем визуал вручную (как в EnemyHealth.UpdateVisual()).
        HandleEquippedChanged(NoWeaponId, equippedWeaponId.Value);
    }

    public override void OnNetworkDespawn()
    {
        equippedWeaponId.OnValueChanged -= HandleEquippedChanged;
        ownedWeaponIds.OnListChanged -= HandleListChanged;
    }

    public bool HasWeapon(int weaponId) => ownedWeaponIds.Contains(weaponId);

    public int GetSlotWeaponId(int slotIndex)
    {
        return slotIndex == 0 ? loadoutSlot1WeaponId.Value
             : slotIndex == 1 ? loadoutSlot2WeaponId.Value
             : NoWeaponId;
    }

    /// <summary>
    /// Начисляет валюту. Вызывается напрямую серверным кодом (например,
    /// EnemyHealth при убийстве моба) — это НЕ ServerRpc, клиент не может
    /// вызвать это у себя и подделать себе баланс.
    /// </summary>
    public void AddCurrency(int amount)
    {
        if (!IsServer || amount <= 0) return;
        currency.Value += amount;
    }

    /// <summary>
    /// Вызывает владелец: "хочу купить это оружие" (станция "Арсенал", основная
    /// вкладка и вкладка скидок). Сервер сам проверяет цену (WeaponDefinition.EffectivePrice,
    /// учитывает скидку) и баланс, списывает деньги и только потом добавляет оружие.
    /// Если игрок уже владеет оружием или денег не хватает — запрос просто отклоняется.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void BuyWeaponServerRpc(int weaponId)
    {
        if (weaponId == NoWeaponId) return;

        if (HasWeapon(weaponId))
        {
            Debug.LogWarning($"[PlayerInventory] Клиент {OwnerClientId} пытается купить " +
                              $"оружие {weaponId}, которое у него уже есть.");
            return;
        }

        WeaponDefinition weapon = WeaponDatabase.Instance != null ? WeaponDatabase.Instance.GetById(weaponId) : null;
        if (weapon == null)
        {
            Debug.LogWarning($"[PlayerInventory] Попытка купить неизвестное оружие с id {weaponId}.");
            return;
        }

        int price = weapon.EffectivePrice;
        if (currency.Value < price)
        {
            Debug.LogWarning($"[PlayerInventory] Клиенту {OwnerClientId} не хватает валюты " +
                              $"на оружие {weaponId} (нужно {price}, есть {currency.Value}).");
            return;
        }

        currency.Value -= price;
        ownedWeaponIds.Add(weaponId);
    }

    /// <summary>
    /// Вызывает владелец: "хочу открыть этот кейс". Сервер проверяет баланс,
    /// списывает цену, кидает взвешенный случайный выбор из содержимого кейса.
    /// Если выпало оружие, которое уже есть — вместо него возвращается часть
    /// валюты (WeaponCaseDefinition.DuplicateRefundAmount). Результат отправляется
    /// обратно владельцу через CaseOpenedRpc, чтобы было что показать в UI.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void OpenCaseServerRpc(int caseId)
    {
        WeaponCaseDefinition caseDefinition = WeaponCaseDatabase.Instance != null
            ? WeaponCaseDatabase.Instance.GetById(caseId)
            : null;

        if (caseDefinition == null) return;

        int price = caseDefinition.Price;
        if (currency.Value < price)
        {
            Debug.LogWarning($"[PlayerInventory] Клиенту {OwnerClientId} не хватает валюты " +
                              $"на кейс {caseId} (нужно {price}, есть {currency.Value}).");
            return;
        }

        WeaponDefinition rolledWeapon = caseDefinition.RollWeapon();
        if (rolledWeapon == null)
        {
            Debug.LogWarning($"[PlayerInventory] Кейс {caseId} пуст — нечего разыгрывать.");
            return;
        }

        currency.Value -= price;

        bool wasDuplicate = HasWeapon(rolledWeapon.WeaponId);
        int refundAmount = 0;

        if (wasDuplicate)
        {
            refundAmount = caseDefinition.DuplicateRefundAmount;
            if (refundAmount > 0) currency.Value += refundAmount;
        }
        else
        {
            ownedWeaponIds.Add(rolledWeapon.WeaponId);
        }

        if (logChanges)
        {
            Debug.Log($"[PlayerInventory] Игрок {OwnerClientId}: открыт кейс {caseId}, выпало оружие " +
                      $"{rolledWeapon.WeaponName} (дубликат: {wasDuplicate}, возврат: {refundAmount})");
        }

        CaseOpenedRpc(rolledWeapon.WeaponId, wasDuplicate, refundAmount);
    }

    [Rpc(SendTo.Owner)]
    private void CaseOpenedRpc(int weaponId, bool wasDuplicate, int refundAmount)
    {
        CaseOpened?.Invoke(weaponId, wasDuplicate, refundAmount);
    }

    /// <summary>
    /// Бесплатная выдача оружия в обход магазина — оставлена для отладки/читов,
    /// UI станции её больше не вызывает (там теперь BuyWeaponServerRpc).
    /// </summary>
    [Rpc(SendTo.Server)]
    public void AddWeaponServerRpc(int weaponId)
    {
        if (weaponId == NoWeaponId) return;

        if (WeaponDatabase.Instance == null || WeaponDatabase.Instance.GetById(weaponId) == null)
        {
            Debug.LogWarning($"[PlayerInventory] Попытка выдать неизвестное оружие с id {weaponId}.");
            return;
        }

        if (!HasWeapon(weaponId))
        {
            ownedWeaponIds.Add(weaponId);
        }
    }

    /// <summary>
    /// Вызывает владелец из экрана инвентаря: положить имеющееся оружие в слот 1 или 2.
    /// Одно оружие может занимать только один слот одновременно — если оно уже
    /// стоит в другом слоте, оно оттуда убирается (переносится, а не копируется).
    /// </summary>
    [Rpc(SendTo.Server)]
    public void AssignToSlotServerRpc(int slotIndex, int weaponId)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;

        if (weaponId != NoWeaponId && !HasWeapon(weaponId))
        {
            Debug.LogWarning($"[PlayerInventory] Клиент {OwnerClientId} пытается " +
                              $"поставить в слот {slotIndex} оружие {weaponId}, которого у него нет.");
            return;
        }

        if (weaponId != NoWeaponId)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (i != slotIndex && GetSlotWeaponId(i) == weaponId)
                {
                    SetSlotWeaponId(i, NoWeaponId);
                }
            }
        }

        SetSlotWeaponId(slotIndex, weaponId);

        if (weaponId != NoWeaponId && activeSlotIndex.Value == NoSlot)
        {
            activeSlotIndex.Value = slotIndex;
        }

        RefreshEquippedFromActiveSlot();
    }

    /// <summary>Вызывает владелец (клавишами 1/2): взять в руки оружие из указанного слота.</summary>
    [Rpc(SendTo.Server)]
    public void EquipSlotServerRpc(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        if (GetSlotWeaponId(slotIndex) == NoWeaponId) return;

        activeSlotIndex.Value = slotIndex;
        RefreshEquippedFromActiveSlot();
    }

    private void SetSlotWeaponId(int slotIndex, int weaponId)
    {
        if (slotIndex == 0) loadoutSlot1WeaponId.Value = weaponId;
        else if (slotIndex == 1) loadoutSlot2WeaponId.Value = weaponId;
    }

    private void RefreshEquippedFromActiveSlot()
    {
        if (!IsServer) return;

        int slot = activeSlotIndex.Value;
        equippedWeaponId.Value = slot == NoSlot ? NoWeaponId : GetSlotWeaponId(slot);
    }

    private void HandleEquippedChanged(int previousId, int newId)
    {
        if (logChanges)
        {
            Debug.Log($"[PlayerInventory] Игрок {OwnerClientId}: экипировано оружие {newId}");
        }

        WeaponEquippedChanged?.Invoke(newId);
    }

    private void HandleListChanged(NetworkListEvent<int> change)
    {
        if (logChanges)
        {
            Debug.Log($"[PlayerInventory] Игрок {OwnerClientId}: список оружия изменился " +
                      $"({change.Type}): {change.Value}");
        }
    }
}