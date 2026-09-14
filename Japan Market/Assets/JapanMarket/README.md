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
| `Data/` | `JapanMarket.Data` | `ItemDefinition`, `FurnitureDefinition`, os dois catálogos, `StorageTrait`, `UnlockCondition` |
| `Domain/` | `JapanMarket.Domain` | contratos de móvel, `FurnitureRegistry`, `StateMachine<T>`, `PricingService` |
| `Gameplay/` | `JapanMarket.Gameplay` | `GameContext`, móveis e capacidades, e todo o sistema de cliente |
| `UI/` | `JapanMarket.UI` | vazio até a Fase 7 |
| `Editor/` | `JapanMarket.Editor` | drawers, sincronização e validação do catálogo |
| `Tests/` | `JapanMarket.Tests` | testes de unidade — rodam sem Play Mode |

`Assets/Editor/JapanMarket/` fica **fora** de assembly definition de propósito: só
assim o migrador enxerga ao mesmo tempo o código legado e o novo. Sai na Fase 8.

## Móveis: como criar um novo

Nenhum código. Um asset e um prefab:

1. **Assets → Create → Japan Market → Furniture** — preço, categoria, footprint,
   custo de energia, condição de desbloqueio. O id se gera sozinho.
2. Monte o prefab e coloque um **`FurnitureInstance`** na raiz.
3. Adicione só as capacidades que ele realmente tem:

| Componente | O móvel passa a… |
|---|---|
| `ProductStorage` | guardar unidades de um produto (prateleira, freezer, vitrine) |
| `CustomerSlots` | ter pontos onde o cliente para |
| `PowerConsumer` | entrar na conta de luz do fim do dia |
| *(Fase 5)* `CheckoutStation` | atender clientes |

Uma "Prateleira Quádrupla" é um `ProductStorage` com quatro âncoras de seção.
Uma "Vitrine Refrigerada com leitor" é `ProductStorage` + `PowerConsumer` +
`CheckoutStation` no mesmo prefab. Em nenhum dos dois casos existe um `switch`
sobre tipo de móvel, porque nenhum sistema pergunta que tipo o móvel é — todos
perguntam se ele tem a capacidade de que precisam.

## Primeiros passos no Editor

1. **Japan Market → Sandbox → Criar ou recriar cena Sandbox**
   Ambiente mínimo para testar um sistema por vez, em segundos em vez de minutos.
2. **Assets → Create → Japan Market → Item Catalog**
   Crie **um** catálogo. Ele se popula sozinho — não arraste produtos à mão.
3. **Japan Market → Migração → 1 · Analisar (dry-run)**
   Não escreve nada. Mostra o que sairia de cada `AllIThingsData`.
4. **Japan Market → Migração → 2 · Gerar ItemDefinitions**
   Cria os assets. Nenhum asset legado é apagado, nenhuma cena é tocada.
5. **Japan Market → Catálogo → Validar produtos** (e **Validar móveis**)
   Aponta preço zerado, prefab faltando, margem negativa, id duplicado.
6. Arraste os dois catálogos no `GameContext` da cena.

## Clientes: quem faz o quê

Cinco componentes, sem sobreposição. A regra que sustenta o resto:
**só o `CustomerLocomotion` escreve no transform.**

| Componente | Responsabilidade |
|---|---|
| `CustomerAgent` | fachada e ciclo de vida. Não decide nem move |
| `CustomerBrain` | a máquina de estados e a tabela de transições |
| `CustomerLocomotion` | dono único do `NavMeshAgent` e do transform |
| `CustomerAnimation` | lê a intenção do Locomotion, escreve no Animator |
| `CustomerBasket` | o que ele pegou e por qual preço |

O perfil de compra (quanto compra, quanto aceita pagar, quanta sujeira tolera,
quanto espera na fila) é um `CustomerProfileData` — asset, não campo de prefab.

### Por que o jitter acabou

Eram três donos do mesmo transform: o `NavMeshAgent`, que continua aplicando
desvio de obstáculo mesmo com `isStopped = true` porque essa flag desliga o
seguimento de caminho e não o *avoidance*; o `DOMoveY` do bob de fila; e o
`DORotateQuaternion` do fidget. `CustomerLocomotion.Halt()` resolve o primeiro
— limpa o caminho, zera a velocidade e desliga o avoidance — e os outros dois
deixaram de existir: a inquietação de quem espera virou variação de idle no
Animator, onde não disputa posição com ninguém.

A segunda causa era a fila: `RefreshQueuePositions` reemitia o destino de
**todos** os NPCs sempre que alguém entrava ou saía, então quem já estava
parado no lugar certo recebia `isStopped = false` + `SetDestination`, andava
alguns centímetros e parava de novo. O `QueueingState` só reemite se a posição
mudou mais de 20 cm.

### Como adicionar um estado

Uma classe herdando `CustomerStateBase` e uma linha na tabela do
`CustomerBrain`. A tabela é a lista completa de como um cliente pode se mover
entre situações — está documentada no comentário de `RegisterTransitions`.

Duas regras ao escrever um estado. O que uma **transição** precisa ler vai para
o `CustomerContext`, porque a tabela só enxerga o contexto; rascunho interno
pode ficar num campo do estado, já que cada cliente tem as próprias instâncias.
E tudo o que o estado **reservou** é liberado no `Exit()` — é isso, e não a
disciplina de quem chama, que torna o vazamento de slot impossível.

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
