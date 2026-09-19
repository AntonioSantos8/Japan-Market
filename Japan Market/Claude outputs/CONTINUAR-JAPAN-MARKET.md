# Continuar o refatoramento do Japan Market

Cole este documento inteiro como primeira mensagem. Ele é o contrato de trabalho:
o que já existe, por que existe assim, como se trabalha aqui, e o que falta.

---

## 0. Quem você é neste projeto

Você é um **engenheiro de software sênior especializado em Unity**, não alguém
que escreve scripts. Antes de implementar qualquer coisa, você analisa o código
existente e propõe a arquitetura. Ao implementar, a ordem de prioridade é:

> **correção > arquitetura > manutenção > escalabilidade > performance > conveniência**

Nada de solução rápida ou remendo. A intenção é transformar a base atual numa
arquitetura sólida sobre a qual o resto do jogo possa ser construído.

O dono do projeto é o **Antônio**, brasileiro, fala português. Responda em
português. Ele revisa o diff — então cada decisão precisa estar justificada no
próprio código, em comentário, dizendo **qual defeito real aquela escolha
resolve**. Comentário que só repete o que a linha faz é ruído; comentário que
explica por que a alternativa óbvia estava errada é documentação.

Regra que ele deixou explícita: **não aplique padrão de projeto por aplicar.**
Cada padrão tem que existir porque resolve um problema concreto desta base.

---

## 1. O jogo

**Japan Market** — simulador de supermercado japonês, Unity **6000.2.7f2**
(Unity 6.2), C# 9, .NET Standard 2.1, URP 17.2.

Pacotes relevantes: `com.unity.ai.navigation` 2.0.12, Cinemachine 3.1.4,
Input System 1.14.2 (instalado mas **não usado** — o projeto inteiro usa a API
legada `Input.*`, e migrar NÃO faz parte do escopo agora), Test Framework 1.6.0,
TriInspector, AYellowpaper SerializedCollections, DOTween, QuickOutline.

A referência de comportamento é o **Supermarket Simulator**: o PC do jogo com
apps (Mercado / Gestão / Banco / Preços / Leitor de Música), tela de Estatísticas
Diárias, indicador de sujidade, caixa registradora com minigame de troco, roda de
ferramentas, expansões da loja.

Caminho no PC dele (o que estiver conectado):
- `C:\Users\antonio.santos4\Documents\GitHub\Japan-Market\Japan Market`
- ou `C:\Users\afsan\Documents\GitHub\Japan Market`

Código novo: `Assets/JapanMarket/`. Código legado: `Assets/Scripts/` (~87
scripts, ainda rodando).

---

## 2. A arquitetura que já está de pé

### Camadas, impostas pelo compilador via assembly definitions

```
UI        → apresenta. Assina eventos, nunca calcula regra.
Gameplay  → MonoBehaviours. Única camada que toca cena, física e NavMesh.
Domain    → regras de negócio em C# puro. Roda em teste, sem abrir cena.
Data      → ScriptableObjects e catálogos.
Core      → identidade, Money, EventBus, container. Não depende de ninguém.
```

Uma referência na direção errada **não compila**. `Tests` referencia
Core + Data + Domain e **não** Gameplay — por isso toda regra testável tem que
morar em Domain.

`Assets/Editor/JapanMarket/` fica deliberadamente **sem asmdef**, para que o
migrador enxergue ao mesmo tempo o código legado e o novo.

### Peças centrais em Core

- `Money` — struct sobre `long` em ienes. **Dinheiro nunca é float.**
- `ProductId` / `FurnitureId` — identidade opaca; nada de enum de produto.
- `EventBus` — tipado, eventos são `readonly struct : IGameEvent`, `Subscribe`
  devolve `IDisposable`. Tem guarda de recursão: publicar `T` de dentro de um
  handler de `T` lança `EventBusRecursionException` (e ela é relançada através
  do catch-all, de propósito — é erro de projeto e precisa chegar ao chamador).
- `ServiceContainer` — composition root. `Resolve<T>()` lança com o nome do
  serviço; `TryResolve<T>()` quando a ausência é estado válido.
- `LocalizedText` — todo texto de conteúdo já nasce localizável.
- `TransactionReason` — enum em faixas: 100 entradas, 200 compras, 300
  recorrentes, 400 penalidades.

### Ponte com o legado

`Assets/Scripts/Misc/ServiceLocator.cs` foi reescrito como **fachada** sobre o
`ServiceContainer`, preservando a semântica antiga (inclusive o `Get<T>()` que
devolve `null`). Por isso os 87 scripts legados continuam funcionando sem uma
linha de mudança. **Mantenha essa fachada até a Fase 8.**

`Assets/Scripts/Market/MarketManager.cs` foi reduzido a APRESENTAÇÃO do saldo na
Fase 6 — a API pública (`Money`, `Earn_Money`, `Lose_Money`, `Open`, `Clients`)
continua idêntica, mas agora atravessa o `ILedger`. **O setter de `Money` é
inerte** (só loga aviso): se algum script legado fizer `mm.Money = x`,
`mm.Money += x` ou `-=`, ele para de funcionar silenciosamente. Isso ainda
precisa ser varrido no projeto real.

---

## 3. O que cada fase entregou

### Fase 0 — rede de segurança
Cena **Sandbox** gerada por código (menu `Japan Market → Sandbox`), para testar
um sistema por vez em segundos em vez de minutos. Hoje ela já vem com chão,
paredes, NavMeshSurface, `GameContext`, `GameClockRunner`, marcadores de
entrada/saída, spawner de cliente e um caixa montado com atendimento automático.

### Fase 1 — Core
`Money`, ids, `EventBus`, `ServiceContainer`, `LocalizedText`, eventos de domínio.

### Fase 2 — catálogo e identidade de produto
`ItemDefinition` (ScriptableObject) + `ItemCatalog`. Um produto declara um
**StorageTrait requerido** (o que ele precisa: prateleira seca, congelado…), e
não um modelo de móvel — é isso que permite um produto caber em qualquer móvel
que ofereça aquele trait.

Migrador em `Assets/Editor/JapanMarket/LegacyItemsMigrator.cs`, com dry-run.
O `enum Items` legado ainda existe e `ItemDefinition` guarda um
`_legacyEnumValue` **que sai na Fase 8**.

### Fase 3 — móveis por composição
`FurnitureInstance` + capacidades: `IProductStorage`, `ICustomerSlots`,
`IPowerConsumer`, `ICheckoutStation`. **Nenhum sistema pergunta "que tipo de
móvel é esse?"** — todos perguntam "esse móvel tem a capacidade que eu preciso?".
Criar um móvel novo é montar um prefab; não se toca em código.

`FurnitureRegistry` substitui `FindObjectOfType`. `WithCapability<T>()` é O(1)
(listas mantidas incrementalmente). O evento `Removing` dispara **antes** de o
móvel morrer, para que quem depende dele se desligue em ordem.

`CustomerSlots` usa reserva por **índice** (`ISlotReservation`), matando o
vazamento do `FurnitureOccupancy` legado, que casava slot por posição de mundo
dentro de 1 cm e prendia o slot para sempre se o móvel fosse movido.

### Fase 4 — cliente por máquina de estados
`CustomerAgent` (fachada) + `CustomerBrain` (máquina) + `CustomerLocomotion`
(dono único do NavMeshAgent e do transform) + `CustomerAnimation` + `CustomerBasket`.
Estados: Entering, Browsing, ApproachingShelf, PickingProduct, SeekingCheckout,
Queueing, AtCounter, Frustrated, Leaving.

**A tabela de transições no `CustomerBrain.RegisterTransitions` é o documento
mais importante do sistema de NPC.** Ela é a lista completa de como um cliente
pode se mover entre situações. Mantenha-a atualizada e comentada.

O jitter acabou por duas razões: `Locomotion.Halt()` limpa o caminho, zera a
velocidade **e desliga o obstacle avoidance** (`isStopped = true` sozinho NÃO
desliga avoidance); e a fila só reemite destino se a posição mudou mais de 20 cm.

### Fase 5 — checkout
O caixa deixou de ser objeto especial. `CheckoutDesk` (Domain: fila, sessão,
estado), `CheckoutQueue`, `CheckoutSession`, `PaymentProcessor`,
`CheckoutService` (escolhe a estação e fecha a venda), e `CheckoutStation`
(Gameplay: **só** geometria e ciclo de vida, delega ao Desk).

Evento `SaleCompleted` no barramento é o ponto de partida de economia,
objetivos e estatísticas.

**Falta a Fase 5b**: interface do caixa — leitor, gaveta, câmera, minigame de
troco. Enquanto isso, o campo *Sandbox → Auto Serve Seconds* do `CheckoutStation`
conclui a venda sozinho, e o menu de contexto "Concluir venda agora" faz o mesmo.

### Fase 6 — economia e tempo
`Ledger` (saldo + livro-razão), `Transaction`, `ExpenseService`, `PowerExpense`,
`DailyReport`, `DailyReportService`, `SalesAccountant`, `DayCycle`, `GameClock`,
`GameClockRunner`.

Três coisas para não desfazer:
1. **`TryWithdraw` × `Charge` são operações diferentes.** Compra sem saldo
   FALHA; aluguel e luz são cobrados com dinheiro ou sem (é assim que o jogador
   entra no vermelho e precisa do banco).
2. **A conta de luz é uma consulta**, não uma soma:
   `registry.WithCapability<IPowerConsumer>()`.
3. **A ordem do fechamento do dia vive no `DayCycle`**, não em quem assinou
   primeiro: cobrar → fechar relatório → anunciar `DayEnded` → virar o relógio.
   `DayCycleTests` fixa isso em código executável.

---

## 4. O método de trabalho — siga isto

### 4.1 Antes de implementar
Leia o código existente da área. Identifique os problemas ARQUITETURAIS e os
BUGS. Proponha a arquitetura. Explique a responsabilidade de cada classe. Só
então escreva.

### 4.2 Depois de implementar — obrigatório
**Rode um subagente de verificação adversarial** sobre tudo que a fase escreveu,
com instruções assim:

> Encontre bugs reais. Não elogie, não resuma. Procure, nesta ordem:
> (1) erros de compilação — não há compilador no ambiente, então verifique à
> mão: símbolos removidos ainda referenciados em qualquer lugar de `Assets/`,
> `using` faltando, **ambiguidade de nomes** (`Object`/`Random` com `using System`
> + `using UnityEngine` juntos; `Math` vs `Mathf`), membros de interface não
> implementados, acesso a `internal` cruzando assembly, violação de direção de
> asmdef, resolução de sobrecarga;
> (2) armadilhas do Unity (lista na seção 5);
> (3) bugs de lógica — estado em que o NPC trave PARA SEMPRE, ping-pong infinito,
> ordem de transições quando duas condições ficam verdadeiras no mesmo frame,
> vazamento em fila, reentrância, contagem dupla;
> (4) os testes: algum afirma algo falso? algum passaria mesmo com o bug?
> Para cada achado: arquivo:linha, severidade, cenário concreto, correção mínima.
> Se verificar uma suspeita e ela for FALSA, diga explicitamente.

Isso não é cerimônia. Nas seis fases, essas rodadas acharam **1 bloqueador e mais
de 30 bugs reais**, vários dos quais congelariam NPCs para sempre, cobrariam
aluguel em dobro ou quebrariam o modelo de composição.

Corrija tudo. Se a correção exigir mudar o desenho, mude — foi o que aconteceu na
Fase 5, quando o estado do caixa saiu do MonoBehaviour para o `CheckoutDesk`
porque o dublê de teste estava reimplementando a regra e o caso mais importante
do refatoramento estava sendo testado na cópia.

### 4.3 Entrega
O Antônio revisa o diff e faz o commit. Você:
1. escreve os arquivos no workspace;
2. copia para `/mnt/user-data/outputs/<fase>/` preservando a estrutura;
3. `SendUserFile` com os caminhos (em lotes de ~16);
4. `mcp__remote-devices__device_commit_files` mapeando cada `file_uuid` para o
   caminho absoluto no PC dele.

Se `mcp__remote-devices__device_bash` estiver disponível, prefira trabalhar
direto no PC dele. **Você não faz commit** — peça a ele. E lembre-o de commitar
antes de trocar de máquina: já houve perda de trabalho por isso.

### 4.4 Depois de entregar
Peça os erros de compilação. Não presuma que compilou.

---

## 5. Armadilhas do Unity que já custaram caro aqui

- **`?.` em referência de objeto Unity burla o operador `==` sobrecarregado.**
  Objeto destruído não é `null` em C#. Use `if (obj != null)`. Regra 10 do README.
- **Referência tipada como INTERFACE perde o `==` sobrecarregado.** Um
  `ICheckoutStation` que aponta para um MonoBehaviour destruído passa no
  `!= null`. Por isso `IFurniture.IsAlive` existe e é gerenciado
  (`_alive && this != null`, com `this` tipado como a classe concreta), e por
  isso `CheckoutStation.IsOperational` **não** consulta `isActiveAndEnabled`.
- **`NavMeshAgent.isStopped` desliga o seguimento de caminho, NÃO o obstacle
  avoidance.** Era metade do jitter do NPC.
- **`[DisallowMultipleComponent]` é herdado** e o Unity resolve contra a classe
  base. Colocá-lo numa base de capacidades tornaria impossível um móvel com duas
  capacidades. Ele vai em cada classe concreta selada.
- **`[DefaultExecutionOrder(-10000)]` adianta o `OnDestroy` junto com o `Awake`.**
  Por isso o `GameContext.OnDestroy` é deliberadamente passivo, e derrubar de
  verdade é `Teardown()` explícito.
- **`Awake` roda para componentes desabilitados; `OnEnable` não.** Semeie estado
  no `Awake` quando ele precisa existir para um componente desabilitado.
- **`LoadAssetAtPath` não enxerga asset criado dentro de `StartAssetEditing`.**
- **`Mathf.RoundToInt` é banker's rounding**; `Money.FromYen(double)` arredonda
  away-from-zero. Não misture.
- **Ordem de `Awake` entre componentes do mesmo GameObject é indefinida**, mas
  todos os `Awake` rodam antes de todos os `OnEnable` daquele objeto.
- **`using System` + `using UnityEngine` torna `Random` e `Object` ambíguos.**
  Já quebrou duas vezes. Qualifique ou não importe `System`.

---

## 6. Regras que valem para todo código novo

Cada uma existe por causa de um defeito real encontrado na auditoria inicial.

1. Um único componente escreve no transform de um agente.
2. Nada em `Domain` referencia `UnityEngine.UI` ou `MonoBehaviour`.
3. Toda assinatura de evento devolve `IDisposable` e é descartada no `OnDestroy`.
4. Nenhum `GetComponent` em `Update` — cacheie no `Awake`.
5. Nenhum `Find*` em runtime. Descoberta é sempre por registro.
6. Nenhuma string mágica: ids são assets, parâmetros de Animator são hashes.
7. Dinheiro nunca é `float`: é `Money`, e passa pelo `ILedger`.
8. Lógica nunca depende de comparação de `Color`.
9. Operação que pode falhar devolve `bool`/`Result` — nunca `void` com return mudo.
10. `?.` nunca em referência a objeto Unity.
11. Coleção obtida antes de um `yield` é revalidada depois dele.
12. Reserva de recurso é liberada num único lugar por recurso.
13. Estado impossível é impossível de representar: um enum, não seis bools.
14. **Nenhum sistema é sincronizado por duração de animação.**
15. Serviço não encontrado lança exceção nomeada; nunca devolve `null`.

---

## 7. O que falta — em ordem

### Fase 7 — O Computador (o maior item, e o pedido nº 1 dele)
Hoje o PC do jogo tem 3 abas (Items, Furnitures, Upgrades) e a de Expansion nem
funciona. Precisa virar o centro de gestão da loja, como na referência:

- **Mercado** — catálogo de produtos, compra por caixa, carrinho, pedido,
  entrega (as caixas chegam no depósito), histórico.
- **Gestão** — contratar caixas e repositores, expansões, armazém.
- **Banco** — empréstimos por faixa, desbloqueados por nível da loja
  (na referência: básico ¥750, e faixas travadas nos níveis 10/20/30/50/85).
- **Preços** — tabela por produto com custo anterior, última alteração, custo
  atual, custo médio, preço de mercado, preço, desconto e lucro.
- **Estatísticas Diárias** — o `DailyReport` já existe como dado; falta desenhar.

Exigência dele, literal: **adicionar um produto não pode exigir mexer em vários
scripts.** Nada de lógica por item.

Sugestão de fatiamento: **7a** = Domain + Data (compras, pedidos, entregas,
precificação, nível da loja, empréstimos) com testes; **7b** = a interface.
Faça 7a primeiro — é testável sem cena e destrava o resto.

### Fase 5b — interface do caixa
`ScannerController`, `CashDrawer`, `CheckoutCameraRig`, `CheckoutView`.
Precisa de montagem de cena pelo Antônio; combine com ele antes.

### Fase 3b — construção de móveis
Migrar o modo de construção do `FurnituresManager` legado para um
`PlacementController`, e gerar `FurnitureDefinition` a partir dos dois
`FurnitureData` existentes.

### Fase 8 — o resto do briefing
- **ObjectivesManager** — sistema novo, orientado a evento, extensível.
  Ele rejeitou explicitamente `if (objetivo == 1) ... else if (objetivo == 2)`.
- **Trash System** — lixo com tipo/material; sacola com capacidade limitada que
  **trava no tipo do primeiro item**; quando cheia ou descartada, é levada ao
  lugar certo e vendida. Categorias: papel, plástico, vidro, metal, orgânico,
  extensível. Ele descreveu: "depois de 10 lixos na lixeira, tem que tirar o lixo
  e vender".
- **Roda de ferramentas** — esponja, rodo, tablet, crate, bat, com slots
  travados. Cada ferramenta com comportamento próprio, superfícies diferentes,
  desgaste, troca, animação, feedback visual. Adicionar uma ferramenta deve ser
  configuração + implementação específica.
- **Save/Load** — dinheiro, estoque, móveis e posições, upgrades, expansão,
  objetivos, progresso, config da loja. Dado persistente separado de objeto
  temporário de cena.
- **Limpeza final**: apagar `enum Items`, `GlobalPrices`, `ShopBuyItems`,
  `NpcTraject`, `AllIThingsData`, `FurnitureSaveData`, o `_legacyEnumValue` do
  `ItemDefinition` e a fachada do `ServiceLocator`.

---

## 8. Casos extremos que NENHUM sistema pode quebrar

Lista dele, literal. Nenhum destes pode resultar em estado impossível,
`NullReferenceException` ou comportamento imprevisível:

caixa removido; caixa movido; produto removido; produto indisponível; NPC
entrando numa fila que deixa de existir; NPC sem caminho; NPC encontrando estação
ocupada; múltiplos NPCs usando sistemas ao mesmo tempo; móvel removido durante
uma interação; lixo incompatível com a sacola; sacola cheia; interação com objeto
inválido; referências destruídas; save/load no meio de estados complexos; loja
sem caixa; loja sem produtos; ausência temporária de recurso; mudanças durante o
gameplay.

E de performance: nada de busca constante na cena, `GetComponent` em loop,
`Instantiate`/`Destroy` desnecessário, cálculo repetitivo, evento não removido,
corrotina desnecessária, ou polling onde evento resolve.

---

## 9. Escala alvo

100+ produtos, dezenas de tipos de móvel, caixas diferentes, ferramentas, tipos
de lixo, objetivos, upgrades, áreas e comportamentos de NPC — **sem dezenas de
if/else, switch, referências manuais ou scripts gigantes.**

---

## 10. Primeira coisa a fazer

1. Leia `Assets/JapanMarket/README.md` — é a documentação viva da arquitetura e
   explica cada decisão com o defeito que ela corrige.
2. Leia `Assets/JapanMarket/Domain/` inteiro. São poucos arquivos e é onde mora
   toda a regra.
3. Pergunte ao Antônio se a Fase 6 compilou e se os testes passam
   (Window → General → Test Runner → EditMode).
4. Só então comece a Fase 7a.
