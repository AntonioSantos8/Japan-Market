using UnityEngine;

public class ShopBuyItems : MonoBehaviour
{
    [SerializeField] GameObject[] objects;
    int currentIndex = 0;

    void Start()
    {
        for (int i = 0; i < objects.Length; i++)
            objects[i].SetActive(i == currentIndex);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q)) { Previous(); }
        if (Input.GetKeyDown(KeyCode.E)) { Next(); }
    }
    public void Next()
    {
        objects[currentIndex].SetActive(false);
        currentIndex++;
        if (currentIndex >= objects.Length)
            currentIndex = 0;
        objects[currentIndex].SetActive(true);
    }

    public void Previous()
    {
        objects[currentIndex].SetActive(false);
        currentIndex--;
        if (currentIndex < 0)
            currentIndex = objects.Length - 1;
        objects[currentIndex].SetActive(true);
    }
}
