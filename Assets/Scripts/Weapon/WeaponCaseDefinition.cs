using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Один слот возможной награды в кейсе: конкретное оружие и его "вес" —
/// не проценты, а относительная доля от суммы весов всех записей.
/// Например 1 / 1 / 3 при трёх записях означает, что третье оружие
/// выпадает в 3 раза чаще каждого из первых двух.
/// </summary>
[System.Serializable]
public class WeightedWeaponEntry
{
    public WeaponDefinition weapon;
    [Min(0.01f)] public float weight = 1f;
}

/// <summary>
/// Данные одного кейса. Сам по себе не участвует в сети — по сети передаётся
/// только CaseId (int), а этот ассет на сервере находится через WeaponCaseDatabase.
/// Сам розыгрыш (RollWeapon) должен вызываться ТОЛЬКО на сервере — вызывающий
/// код отвечает за эту проверку, сам класс этого не проверяет.
/// </summary>
[CreateAssetMenu(fileName = "Case_", menuName = "IAPS Shooter/Weapon Case Definition")]
public class WeaponCaseDefinition : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Уникальный id. Не должен повторяться у разных WeaponCaseDefinition — именно это число идёт по сети.")]
    [SerializeField] private int caseId;
    [SerializeField] private string caseName = "Новый кейс";

    [Header("UI")]
    [SerializeField] private Sprite icon;

    [Header("Магазин")]
    [SerializeField] private int price = 0;

    [Header("Содержимое")]
    [SerializeField] private List<WeightedWeaponEntry> possibleWeapons = new List<WeightedWeaponEntry>();

    [Header("Дубликаты")]
    [Tooltip("Сколько процентов от цены кейса вернуть игроку, если выпало оружие, которое у него уже есть.")]
    [Range(0, 100)]
    [SerializeField] private float duplicateRefundPercent = 50f;

    public int CaseId => caseId;
    public string CaseName => caseName;
    public Sprite Icon => icon;
    public int Price => price;
    public IReadOnlyList<WeightedWeaponEntry> PossibleWeapons => possibleWeapons;
    public int DuplicateRefundAmount => Mathf.RoundToInt(price * duplicateRefundPercent / 100f);

    /// <summary>
    /// Взвешенный случайный выбор одного оружия из содержимого кейса.
    /// Возвращает null, если в кейсе нет ни одной валидной записи.
    /// </summary>
    public WeaponDefinition RollWeapon()
    {
        float totalWeight = 0f;
        foreach (WeightedWeaponEntry entry in possibleWeapons)
        {
            if (entry.weapon != null) totalWeight += Mathf.Max(0f, entry.weight);
        }

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (WeightedWeaponEntry entry in possibleWeapons)
        {
            if (entry.weapon == null) continue;

            cumulative += Mathf.Max(0f, entry.weight);
            if (roll <= cumulative) return entry.weapon;
        }

        // Сюда попадём только из-за погрешности float — возвращаем последнюю валидную запись.
        for (int i = possibleWeapons.Count - 1; i >= 0; i--)
        {
            if (possibleWeapons[i].weapon != null) return possibleWeapons[i].weapon;
        }

        return null;
    }

    private void OnValidate()
    {
        if (price < 0) price = 0;
    }
}