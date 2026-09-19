# Japan Market — arquitetura

> **Para montar na cena, veja o [SETUP.md](SETUP.md).** Este arquivo explica o
> *porquê* de cada decisão; o outro é a lista de montagem.

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
| `Domain/` | `JapanMarket.Domain` | contratos de móvel, `FurnitureRegistry`, `StateMachine<T>`, `PricingService`, checkout, economia e relógio |
| `Gameplay/` | `JapanMarket.Gameplay` | `GameContext`, móveis e capacidades, `CheckoutStation`, `GameClockRunner`, e todo o sistema de cliente |
| `UI/` | `JapanMarket.UI` | vazio até a Fase 7 |
| `Editor/` | `JapanMarket.Editor` | drawers, sincronização e validação do catálogo |
| `Tests/` | `JapanMarket.Tests` | testes de unidade — rodam sem Play Mode. Dublês compartilhados em `Tests/Fakes/` |

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
| `CheckoutStation` | ter fila e atender clientes |

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
quanto espera na fila, se prefere cartão) é um `CustomerProfileData` — asset, não
campo de prefab.

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

## Checkout: o caixa deixou de ser especial

O `CashRegister` era um objeto único da cena, encontrado por
`FindObjectOfType`, com onze responsabilidades e 700 linhas. Hoje caixa é uma
**capacidade** que qualquer móvel pode ter, e existem quantos o jogador comprar.

| Peça | Camada | Responsabilidade |
|---|---|---|
| `CheckoutDesk` | Domain | o estado de UM balcão: fila, venda aberta, operação |
| `CheckoutQueue` | Domain | a fila, em C# puro |
| `CheckoutSession` | Domain | uma venda: linhas, o que já passou, o que foi entregue |
| `PaymentProcessor` | Domain | troco e valor entregue. Sem UI, sem tween |
| `CheckoutService` | Domain | escolhe a estação e fecha a venda |
| `CheckoutStation` | Gameplay | só geometria e ciclo de vida. Delega ao `CheckoutDesk` |

O balcão é `Domain`, e não um campo dentro do MonoBehaviour, por uma razão que
só apareceu ao escrever os testes: com o estado dentro do componente, o dublê de
teste precisava reimplementá-lo — e aí o caso mais importante do refatoramento
passava a ser verificado numa cópia, não no código que roda. Hoje o dublê e o
componente compartilham o mesmo `CheckoutDesk`.

O cliente **nunca** guarda referência a um caixa. Ele pede um ao serviço, e o
devolve quando termina ou quando ele some. Por isso "arrancar a registradora no
meio do expediente" é um caso normal: a estação avisa cada cliente da fila
enquanto ainda é um objeto válido, e cada um decide sozinho se procura outro
caixa ou vai embora.

Três defeitos do código antigo que sumiram por construção:

* **Contadores paralelos.** O `_totalExpected` e o `_scannedCount` só ficavam
  consistentes porque a sacola animava por 0,90 s enquanto os itens chegavam a
  cada 0,20 s — encurtar a animação quebrava o fecho da venda. Hoje "tudo
  passado" é `ScannedCount >= Lines.Count`.
* **Array fixo de pontos de fila.** `queuePoints[i]` era indexado pela contagem
  de clientes, sem checar o tamanho: o nono cliente numa fila de oito pontos era
  `IndexOutOfRangeException`. Hoje a posição sai de âncora + direção × índice.
* **Tolerância de meio iene no troco.** `Mathf.Abs(diff) < 0.5f` existia só para
  esconder erro de ponto flutuante. Com `Money` em inteiro, a comparação é `==`.

O evento `SaleCompleted` no barramento é o ponto de partida da economia, dos
objetivos e das estatísticas — nenhum deles é conhecido pelo checkout. Quem
precisa do detalhe por produto assina `ICheckoutService.SaleCompleted`, que
entrega a sessão inteira; o evento do barramento carrega só totais, porque
`Core` não enxerga `Data`.

**Ainda não existe:** a interface do caixa (leitor, gaveta, câmera, minigame do
troco) e o crédito no saldo — são a Fase 5b e a Fase 6. Até lá, o campo
*Sandbox → Auto Serve Seconds* do `CheckoutStation` conclui a venda sozinho,
e o menu de contexto **Depuração → Concluir venda agora** faz o mesmo sob
demanda. Assim o ciclo completo — entra, compra, pega fila, paga, sai — roda de
ponta a ponta antes de existir uma única tela.

## Economia: um saldo só, com histórico

O dinheiro era um `float money` dentro do `MarketManager`, no mesmo método que
tocava o som, animava o tween, piscava o texto e spawnava doze faíscas. Agora:

| Peça | Camada | Responsabilidade |
|---|---|---|
| `Ledger` | Domain | o saldo e o livro-razão. Só o número e o histórico |
| `Transaction` | Domain | uma linha: valor com sinal, motivo, dia, nota |
| `ExpenseService` | Domain | apura e cobra as contas do fim do dia |
| `PowerExpense` | Domain | a conta de luz |
| `DailyReport` | Domain | o fechamento do dia, como dado |
| `DayCycle` | Domain | a sequência de fechamento, numa ordem definida |
| `GameClock` | Domain | dia, hora, porta aberta. Alimentado de fora |
| `GameClockRunner` | Gameplay | empurra `Time.deltaTime` para dentro do relógio |
| `MarketManager` | legado | virou só a APRESENTAÇÃO do saldo |

Três coisas que valem ser ditas.

**`TryWithdraw` e `Charge` não são a mesma operação.** Comprar uma prateleira sem
dinheiro tem que FALHAR e a loja continua igual; o aluguel é cobrado com saldo ou
sem, e é assim que o jogador entra no vermelho e precisa do banco. O
`Lose_Money` antigo fazia a coisa mais perigosa entre as duas: devolvia `void` e
simplesmente não descontava, então quem chamou seguia achando que comprou.

**A conta de luz é uma consulta, não uma soma.** `PowerExpense` é literalmente
`registry.WithCapability<IPowerConsumer>()` somado. Um móvel elétrico novo entra
na conta porque tem a capacidade, não porque alguém lembrou de somá-lo — o aviso
da referência ("quanto mais aparelhos, mais caras as despesas") deixou de ser uma
regra escrita em algum lugar e virou consequência do modelo.

**A ordem do fechamento é explícita.** Cobrar → fechar o relatório → anunciar →
virar o relógio. Ela vive no `DayCycle` e não em quem assinou primeiro, porque
ordem de assinatura não é controlável e as etapas dependem umas das outras: o
saldo final do dia precisa já incluir o aluguel, e as linhas precisam ser
registradas no dia que acabou, não no seguinte. `DayCycleTests` fixa isso em
código executável.

O `MarketManager` mantém `Money`, `Earn_Money`, `Lose_Money` e `Open` com a mesma
assinatura, então nada que os chama precisou mudar — mas agora todos atravessam o
livro-razão, e o efeito visual reage ao evento `BalanceChanged` em vez de ser
disparado por quem chama. Consequência prática: uma venda fechada no caixa novo
ganha a mesma animação sem conhecer o `MarketManager`.

## Comprar, precificar, financiar (Fase 7)

O computador da loja, sem o computador: as três regras existem em Domain e são
testáveis sem abrir a Unity. A tela é casca por cima disto.

| Peça | Camada | Responsabilidade |
|---|---|---|
| `MarketCart` | Domain | o carrinho, com teto por produto |
| `MarketOrderService` | Domain | valida, cobra, cria o pedido, vence o prazo |
| `MarketOrder` | Domain | um pedido pago. Imutável |
| `DeliveryQueue` | Domain | o que já foi pago e ainda não apareceu |
| `DeliverySpawner` | Gameplay | ONDE a caixa aparece. Só isso |
| `PricingService` | Domain | preço de venda e histórico de custo |
| `BankService` | Domain | empréstimos, como fonte de despesa diária |
| `StoreLevelService` | Domain | XP e nível, a partir de `SaleCompleted` |

Quatro coisas que valem ser ditas.

**O prazo é em horas ABSOLUTAS, não em hora do dia.** Um pedido feito às 23h com
duas horas de prazo chega "às 25h", e comparando hora do dia isso nunca acontece.
Por isso `IGameClock.TotalHours` é um contador acumulado e não `(Dia−1)×24 + hora`:
o dia só é simulado das 6h às 24h, então a fórmula presenteava seis horas por
virada — e quem apertasse "dormir" às 9h ganhava vinte e uma. Na prática, dormir
cancelava qualquer entrega.

**O custo é registrado no PEDIDO, e pelo TOTAL.** No pedido porque é aí que o
dinheiro sai, e é isso que "custo atual" significa na tela de Preços; esperar a
entrega faria a tabela mentir durante o prazo. Pelo total porque o custo unitário
é uma divisão arredondada: uma caixa de ¥100 com 3 unidades dá ¥33, e ¥33 × 3 =
¥99 contra ¥100 debitados — um ien por caixa, acumulando. Hoje isso não acontece,
porque `BoxCost` é derivado (custo unitário × unidades) e a divisão volta exata; a
assinatura assume o total mesmo assim, porque a alternativa é um contrato que só
está certo enquanto ninguém der preço próprio à caixa. Desconto por volume é o
próximo pedido óbvio.

Pelo mesmo motivo `PricingData` guarda `TotalSpent` e deriva a média dele — e esse
aqui **mordia**: recalcular a média a partir da própria média já arredondada, com
divisão inteira, não acumula erro devagar, ela *congela*. `(m·n + c) / (n+1)`
volta a `m` sempre que `|c − m| < n+1`, então com treze unidades acumuladas a média
parava de andar e a margem ficava errada para sempre.

**Domain não instancia caixa na cena.** O serviço enfileira em `DeliveryQueue`
quando o prazo vence; o `DeliverySpawner` consome a fila e instancia o prefab.
Trocar o depósito de lugar não toca em regra nenhuma, e a compra inteira é
testável sem cena. Sem um consumidor na cena, porém, o ciclo não fecha: a Sandbox
já vem com um.

**Desbloqueio é filtro de vitrine, não só de pagamento.** `AvailableProducts`
devolve o que o nível atual permite comprar, para que a tela nem ofereça o que vai
ser recusado. A validação no `TryCheckout` continua lá — uma tela é uma
conveniência, não uma garantia — e nada é debitado quando ela falha.

**Ainda não existe:** salário de funcionário e as telas (Mercado, Preços, Banco,
Estatísticas Diárias). O relatório existe como dado, falta desenhá-lo.

## Objetivos (Fase 8a)

Criar um objetivo é criar um asset. Não existe lista para arrastar, número para
cadastrar, nem `switch` para editar — e é isso que o `ObjectiveServiceTests`
prova: cada teste monta a definição que quer e todos passam pelo mesmo caminho
de código.

| Peça | Camada | Responsabilidade |
|---|---|---|
| `ObjectiveCondition` | Data | uma coisa a contar, e quanto. Assina o barramento sozinha |
| `ObjectiveDefinition` | Data | título, condições, recompensa, desbloqueio |
| `ObjectiveCatalog` | Data | o catálogo, populado pelo import |
| `ActiveObjective` | Domain | a instância em jogo: progresso desta partida |
| `ObjectiveService` | Domain | ativa, avalia, paga, encadeia |
| `ObjectiveRunner` | Gameplay | chama `Flush()` uma vez por frame |
| `FlagUnlock` | Data | "só depois que a flag X for levantada" |

Três decisões que valem ser ditas.

**Ninguém pergunta "que tipo de objetivo é esse?".** O serviço chama
`condition.Watch(bus, progress)` e espera o número chegar. Um tipo novo de
objetivo é uma classe nova herdando de `ObjectiveCondition` — nenhum arquivo
existente muda. É o mesmo formato do `UnlockCondition`, que já tinha resolvido
este problema para desbloqueios; a alternativa era um `switch` no gerente, e aí
todo objetivo novo edita o gerente.

A consequência, e ela é intencional: uma condição só enxerga o que passa pelo
barramento. Um objetivo sobre algo que não é publicado começa publicando o
evento. Foi assim que o `StoreLevelChanged` nasceu — Data não pode enxergar
Domain, então "chegue ao nível 5" exigia o nível no barramento.

**A conclusão é adiada de propósito.** O progresso é anotado DENTRO do despacho
de um evento. Pagar ali publicaria um evento de dinheiro de dentro de um handler
de dinheiro, e o `EventBus` tem guarda de recursão por tipo — ele **lança**. Não
é teoria: um objetivo que ouve `TransactionRecorded` e paga em dinheiro é
exatamente essa combinação, e existe um teste que a provoca. Por isso o serviço
anota durante o evento e paga no `Flush()`, que o `ObjectiveRunner` chama no
`LateUpdate`.

**A cadeia não é uma estrutura, é uma consequência.** O objetivo A levanta a flag
`primeira_venda` ao concluir; o objetivo B tem um `FlagUnlock` apontando para ela.
Nenhum dos dois assets referencia o outro, e o serviço não sabe que existe
cadeia. O mesmo `FlagUnlock` serve para produto, móvel e faixa de empréstimo —
tudo que já usava `UnlockCondition` ganhou "liberado por objetivo" de graça.

**Ainda não existe:** a tela de objetivos, e a persistência de verdade — o
`Snapshot()`/`Restore()` já estão prontos e testados, falta o arquivo de save
(Fase 8d) chamá-los.

## Lixo, ferramentas e save (Fase 8b–8d)

| Peça | Camada | Responsabilidade |
|---|---|---|
| `TrashBag` | Domain | o saco: capacidade, e a trava de categoria |
| `ITrashReceptacle` | Domain | capacidade de móvel: a lixeira |
| `TrashService` | Domain | a doca dos fundos. É uma fonte de receita diária |
| `IDailyIncomeSource` | Domain | receita apurada no fechamento, antes do relatório |
| `ToolBelt` / `ToolSlot` | Domain | a roda: seleção, superfícies, desgaste |
| `CleanlinessService` | Domain | quanta sujeira existe. Implementa `IStoreCleanliness` |
| `GameSave` | Domain | o save como dado puro, sem uma linha de Unity |
| `SaveService` | Gameplay | tira e repõe a fotografia da partida |

Quatro decisões que valem ser ditas.

**O saco trava na categoria do primeiro item.** É a regra inteira do sistema de
lixo. A alternativa — lixeira com categoria fixa no Inspector, como o `TrashBin`
legado — empurra a decisão para o momento de construir a loja, e depois disso o
jogo joga sozinho: cada lixo tem exatamente um destino possível e não sobra
escolha nenhuma. Com a trava, o jogador decide a que destinar cada saco, e errar
custa uma viagem.

**O caminhão é o fechamento do dia.** `TrashService` é um `IDailyIncomeSource`
registrado no `DayCycle`, e não um assinante de `DayEnded`. A diferença é que o
`DayCycle` publica `DayEnded` DEPOIS de fechar o relatório: assinando o evento, o
dinheiro do caminhão cairia no relatório do dia seguinte e o jogador veria um dia
que rendeu menos do que rendeu.

**A esponja não limpa vidro, e isso não é um `if`.** A ferramenta declara em que
superfícies trabalha, a sujeira declara em que superfície está, e o cinto cruza
os dois — mesmo truque do `StorageTrait` entre produto e móvel. Errar a
superfície não consome durabilidade: senão o custo do erro vira dinheiro em vez
de tempo, e o jogador quebra a esponja esfregando janela.

**O desgaste não mora no asset.** Um `ScriptableObject` é compartilhado e gravado
dentro do projeto: uma esponja que perdesse durabilidade no asset voltaria gasta
na sessão seguinte, e no build chegaria gasta de fábrica para todo mundo. Mesma
divisão de `LoanDefinition`/`ActiveLoan` e de objetivo/objetivo ativo.

**Sobre o save:** só tipos que o `JsonUtility` entende, dinheiro em `long` de
ienes, e referência a asset sempre por id ou chave — nunca por referência. Um
asset apagado do projeto entre duas versões vira uma linha descartada com aviso
no console, nunca uma exceção no carregamento. O `SaveFormatTests` existe porque
o `JsonUtility` falha em SILÊNCIO: ele não lança ao encontrar uma propriedade em
vez de um campo, um `Dictionary` ou uma interface — só não grava.

## Regras que valem para todo código novo

Cada uma existe por causa de um defeito real encontrado na auditoria.

1. Um único componente escreve no transform de um agente.
2. Nada em `Domain` referencia `UnityEngine.UI` ou `MonoBehaviour`.
3. Toda assinatura de evento devolve `IDisposable` e é descartada no `OnDestroy`.
4. Nenhum `GetComponent` em `Update` — cacheie no `Awake`.
5. Nenhum `Find*` em runtime. Descoberta é sempre por registro.
6. Nenhuma string mágica: ids são assets, parâmetros de Animator são hashes.
7. Dinheiro nunca é `float`: é `Money`, inteiro em ienes, e passa pelo `ILedger`.
8. Lógica nunca depende de comparação de `Color`.
9. Operação que pode falhar devolve `bool`/`Result` — nunca `void` com return mudo.
10. `?.` nunca em referência a objeto Unity. Use `if (obj != null)`.
11. Coleção obtida antes de um `yield` é revalidada depois dele.
12. Reserva de recurso é liberada por índice, num único lugar por recurso — o
    `Exit()` do estado que a fez, ou, quando a posse atravessa estados (a fila do
    caixa), o método do contexto que os estados de saída chamam.
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
