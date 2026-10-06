using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Реестр всех WeaponDefinition в игре. Позволяет по id (пришедшему по сети)
/// получить сам ассет оружия. Ассет этого класса должен лежать
/// в Assets/Resources и называться ровно "WeaponDatabase".
/// </summary>
[CreateAssetMenu(fileName = "WeaponDatabase", menuName = "IAPS Shooter/Weapon Database")]
public class WeaponDatabase : ScriptableObject
{
    private const string ResourcePath = "WeaponDatabase";

    [SerializeField] private List<WeaponDefinition> weapons = new List<WeaponDefinition>();

    private static WeaponDatabase cachedInstance;
    private Dictionary<int, WeaponDefinition> lookup;

    public static WeaponDatabase Instance
    {
        get
        {
            if (cachedInstance == null)
            {
                cachedInstance = Resources.Load<WeaponDatabase>(ResourcePath);

                if (cachedInstance == null)
                {
                    Debug.LogError($"[WeaponDatabase] Не найден ассет по пути 'Resources/{ResourcePath}'. " +
                                   "Создай его: Assets → Create → IAPS Shooter → Weapon Database, " +
                                   "положи в Assets/Resources и назови WeaponDatabase.");
                }
            }

            return cachedInstance;
        }
    }

    public IReadOnlyList<WeaponDefinition> AllWeapons => weapons;

    public WeaponDefinition GetById(int weaponId)
    {
        EnsureLookupBuilt();

        if (lookup.TryGetValue(weaponId, out var definition))
        {
            return definition;
        }

        Debug.LogWarning($"[WeaponDatabase] Оружие с id {weaponId} не найдено в базе.");
        return null;
    }

    private void EnsureLookupBuilt()
    {
        if (lookup != null) return;

        lookup = new Dictionary<int, WeaponDefinition>();

        foreach (var weapon in weapons)
        {
            if (weapon == null) continue;

            if (lookup.ContainsKey(weapon.WeaponId))
            {
                Debug.LogError($"[WeaponDatabase] Дублирующийся id {weapon.WeaponId}: " +
                               $"'{weapon.WeaponName}' и '{lookup[weapon.WeaponId].WeaponName}'.", weapon);
                continue;
            }

            lookup.Add(weapon.WeaponId, weapon);
        }
    }

    private void OnEnable()
    {
        // Сбрасываем кэш при перезагрузке домена / входе в play mode в редакторе
        lookup = null;
    }
}