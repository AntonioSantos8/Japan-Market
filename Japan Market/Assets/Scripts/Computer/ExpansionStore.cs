using UnityEngine;
public class ExpansionStore : MonoBehaviour
{
    [SerializeField] GameObject[] storesExpansion;
    int currentExpansion = 0;
    void Start()
    {
        UpdateStore();
    }
    public void BuyExpansion()
    {
        if (currentExpansion + 1 >= storesExpansion.Length)
        {
            return;
        }

        currentExpansion++;
        UpdateStore();
    }
    void NextExpansion()
    {
        int next = currentExpansion + 1 >= storesExpansion.Length ? 0 : currentExpansion + 1;
        ChangeExpansion(next);
    }
    void PreviousExpansion()
    {
        int prev = currentExpansion - 1 < 0 ? storesExpansion.Length - 1 : currentExpansion - 1;
        ChangeExpansion(prev);
    }
    void ChangeExpansion(int newLevel)
    {
        if (newLevel == currentExpansion) return;



        currentExpansion = newLevel;
        UpdateStore();
    }
    void UpdateStore()
    {
        for (int i = 0; i < storesExpansion.Length; i++)
        {
            storesExpansion[i].SetActive(i == currentExpansion);
        }
    }
    public int GetCurrentExpansion()
    {
        return currentExpansion;
    }
}