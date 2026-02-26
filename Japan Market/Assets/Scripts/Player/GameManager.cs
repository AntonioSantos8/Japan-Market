using UnityEngine;

public class GameManager : MonoBehaviour
{

    [SerializeField] float money;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T)) { money += 20; }
    }
}
