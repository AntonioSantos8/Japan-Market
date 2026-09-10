# Japan Market — arquitetura

Código novo. O código legado continua em `Assets/Scripts/` e vai sendo aposentado
fase a fase, sem que o jogo pare de rodar em nenhum momento.

## Camadas

As setas de dependência só apontam para baixo, e isso é **cobrado pelo compilador**
através dos assembly definitions — uma referência na direção errada não compila.

```
UI        → apresenta. Assina eventos, nunca calcula regra.
Gameplay  → MonoBehaviours. Única camada que toca cena, física e NavMesh.
Domain    → regras de negócio em C# puro. Roda em teste, sem abrir cena.
Data      → ScriptableObjects e catálogos.
Core      → identidade, Money, EventBus, container. Não depende de ninguém.
```

Efeito colateral útil: mexer na UI recompila só a UI, não os 87 scripts do projeto.

## Onde fica o quê

| Pasta | Assembly | Conteúdo |
|---|---|---|
| `Core/` | `JapanMarket.Core` | `ProductId`, `Money`, `EventBus`, `ServiceContainer`, `LocalizedText` |
| `Data/` | `JapanMarket.Data` | `ItemDefinition`, `ItemCatalog`, `StorageTrait`, `UnlockCondition` |
| `Domain/` | `JapanMarket.Domain` | vazio até a Fase 5 (fila, pagamento, economia, objetivos) |
| `Gameplay/` | `JapanMarket.Gameplay` | `GameContext`, `StoreProgress` |
| `UI/` | `JapanMarket.UI` | vazio até a Fase 7 |
| `Editor/` | `JapanMarket.Editor` | drawers, sincronização e validação do catálogo |
| `Tests/` | `JapanMarket.Tests` | testes de unidade — rodam sem Play Mode |

`Assets/Editor/JapanMarket/` fica **fora** de assembly definition de propósito: só
assim o migrador enxerga ao mesmo tempo o código legado e o novo. Sai na Fase 8.

## Primeiros passos no Editor

1. **Japan Market → Sandbox → Criar ou recriar cena Sandbox**
   Ambiente mínimo para testar um sistema por vez, em segundos em vez de minutos.
2. **Assets → Create → Japan Market → Item Catalog**
   Crie **um** catálogo. Ele se popula sozinho — não arraste produtos à mão.
3. **Japan Market → Migração → 1 · Analisar (dry-run)**
   Não escreve nada. Mostra o que sairia de cada `AllIThingsData`.
4. **Japan Market → Migração → 2 · Gerar ItemDefinitions**
   Cria os assets. Nenhum asset legado é apagado, nenhuma cena é tocada.
5. **Japan Market → Catálogo → Validar catálogo**
   Aponta produto sem preço, sem prefab, com margem negativa, id duplicado.
6. Arraste o catálogo no `GameContext` da cena.

## Regras que valem para todo código novo

Cada uma existe por causa de um defeito real encontrado na auditoria.

1. Um único componente escreve no transform de um agente.
2. Nada em `Domain` referencia `UnityEngine.UI` ou `MonoBehaviour`.
3. Toda assinatura de evento devolve `IDisposable` e é descartada no `OnDestroy`.
4. Nenhum `GetComponent` em `Update` — cacheie no `Awake`.
5. Nenhum `Find*` em runtime. Descoberta é sempre por registro.
6. Nenhuma string mágica: ids são assets, parâmetros de Animator são hashes.
7. Dinheiro nunca é `float`.
8. Lógica nunca depende de comparação de `Color`.
9. Operação que pode falhar devolve `bool`/`Result` — nunca `void` com return mudo.
10. `?.` nunca em referência a objeto Unity. Use `if (obj != null)`.
11. Coleção obtida antes de um `yield` é revalidada depois dele.
12. Reserva de recurso é liberada no `Exit()` do estado que a fez, por índice.
13. Estado impossível é impossível de representar: um enum, não seis bools.
14. Nenhum sistema é sincronizado por duração de animação.
15. Serviço não encontrado lança exceção nomeada; nunca devolve `null`.

## O adaptador do ServiceLocator

`Assets/Scripts/Misc/ServiceLocator.cs` foi reescrito como fachada sobre o
`ServiceContainer`, mantendo exatamente o comportamento antigo — inclusive o
`Get<T>()` que devolve `null` quando o serviço não existe, do qual vários call
sites dependem. É isso que permite os 87 scripts atuais continuarem funcionando
sem uma linha de mudança enquanto a migração acontece.

Em código novo, receba `IServiceContainer` por injeção e use `Resolve<T>()`
(lança com o nome do serviço faltando) ou `TryResolve<T>()` (quando a ausência
for um estado válido do jogo).
