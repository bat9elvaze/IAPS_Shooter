using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Реестр всех WeaponCaseDefinition в игре. Позволяет по caseId (пришедшему
/// по сети) получить сам ассет кейса. Ассет этого класса должен лежать
/// в Assets/Resources и называться ровно "WeaponCaseDatabase" —
/// точно как WeaponDatabase для оружия.
/// </summary>
[CreateAssetMenu(fileName = "WeaponCaseDatabase", menuName = "IAPS Shooter/Weapon Case Database")]
public class WeaponCaseDatabase : ScriptableObject
{
    private const string ResourcePath = "WeaponCaseDatabase";

    [SerializeField] private List<WeaponCaseDefinition> cases = new List<WeaponCaseDefinition>();

    private static WeaponCaseDatabase cachedInstance;
    private Dictionary<int, WeaponCaseDefinition> lookup;

    public static WeaponCaseDatabase Instance
    {
        get
        {
            if (cachedInstance == null)
            {
                cachedInstance = Resources.Load<WeaponCaseDatabase>(ResourcePath);

                if (cachedInstance == null)
                {
                    Debug.LogError($"[WeaponCaseDatabase] Не найден ассет по пути 'Resources/{ResourcePath}'. " +
                                   "Создай его: Assets → Create → IAPS Shooter → Weapon Case Database, " +
                                   "положи в Assets/Resources и назови WeaponCaseDatabase.");
                }
            }

            return cachedInstance;
        }
    }

    public IReadOnlyList<WeaponCaseDefinition> AllCases => cases;

    public WeaponCaseDefinition GetById(int caseId)
    {
        EnsureLookupBuilt();

        if (lookup.TryGetValue(caseId, out var definition))
        {
            return definition;
        }

        Debug.LogWarning($"[WeaponCaseDatabase] Кейс с id {caseId} не найден в базе.");
        return null;
    }

    private void EnsureLookupBuilt()
    {
        if (lookup != null) return;

        lookup = new Dictionary<int, WeaponCaseDefinition>();

        foreach (WeaponCaseDefinition caseDefinition in cases)
        {
            if (caseDefinition == null) continue;

            if (lookup.ContainsKey(caseDefinition.CaseId))
            {
                Debug.LogError($"[WeaponCaseDatabase] Дублирующийся id {caseDefinition.CaseId}: " +
                               $"'{caseDefinition.CaseName}' и '{lookup[caseDefinition.CaseId].CaseName}'.", caseDefinition);
                continue;
            }

            lookup.Add(caseDefinition.CaseId, caseDefinition);
        }
    }

    private void OnEnable()
    {
        lookup = null;
    }
}