using Unity.AI.Navigation;
using UnityEngine;
public class NavMeshManager : MonoBehaviour
{
    [SerializeField] NavMeshSurface surface;

    public void RebuildNavMesh()
    {
        surface.BuildNavMesh(); // síncrono — trava o frame
    }

    // Melhor: assíncrono para não dropar FPS
    public async void RebuildAsync()
    {
        //var data = new NavMeshData();
        //var operation = surface.UpdateNavMesh(data);

        //  await operation; // não bloqueia a thread principal
        // aplica o novo dado após concluir
    }
}