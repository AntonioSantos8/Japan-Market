using UnityEngine;
public class ObjectEmission : MonoBehaviour
{
    Renderer rend;
    Material mat;

    void Start()
    {
        rend = GetComponent<Renderer>();
        mat = rend.material;
    }
    public void ActiveEmission()
    {
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.white * 0.17f);
    }
    public void DisableEmission()
    {
        mat.SetColor("_EmissionColor", Color.black);
    }
}
