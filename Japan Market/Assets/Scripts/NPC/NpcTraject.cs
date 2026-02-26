using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NpcTraject : MonoBehaviour
{
    private NavMeshAgent _agent;
    private FurnitureManager _furnitureManager;

    [Header("Configurações de Destino")]
    [SerializeField] private Transform _pontoFinalEspecifico;
    [SerializeField] private float _tempoDeEsperaNoMovel = 2f;

    private void Awake() => _agent = GetComponent<NavMeshAgent>();

    private void Start()
    {
        _furnitureManager = ServiceLocator.Get<FurnitureManager>();
        StartCoroutine(RotinaDeMovimentacao());
    }

    private IEnumerator RotinaDeMovimentacao()
    {
        yield return new WaitForSeconds(4f);

        var todosMoveis = _furnitureManager.GetPlacedFurnitures();

        if (todosMoveis != null && todosMoveis.Count > 0)
        {
            int quantidadeParaVisitar = Random.Range(1, todosMoveis.Count + 1);

            List<FurnitureInstance> listaSorteada = SortearMoveisSemRepetir(todosMoveis, quantidadeParaVisitar);
            foreach (var movel in listaSorteada)
            {
                yield return StartCoroutine(IrAteDestino(movel.InteractionPosition));

                yield return new WaitForSeconds(_tempoDeEsperaNoMovel);
            }
        }

        if (_pontoFinalEspecifico != null)
        {
            yield return StartCoroutine(IrAteDestino(_pontoFinalEspecifico.position));
        }
    }

    private IEnumerator IrAteDestino(Vector3 destino)
    {
        _agent.SetDestination(destino);

        yield return new WaitUntil(() => !_agent.pathPending);
            
        yield return new WaitUntil(() => _agent.remainingDistance <= _agent.stoppingDistance);
        print("Chegoy");
    }

    private List<FurnitureInstance> SortearMoveisSemRepetir(List<FurnitureInstance> listaOriginal, int quantidade)
    {
        List<FurnitureInstance> copia = new List<FurnitureInstance>(listaOriginal);
        List<FurnitureInstance> resultado = new List<FurnitureInstance>();

        for (int i = 0; i < quantidade; i++)
        {
            if (copia.Count == 0) break;

            int index = Random.Range(0, copia.Count);
            resultado.Add(copia[index]);
            copia.RemoveAt(index);
        }

        return resultado;
    }
}