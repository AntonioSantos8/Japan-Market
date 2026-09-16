# Configurar na cena — passo a passo

Este arquivo é a lista de montagem. A arquitetura e o *porquê* de cada decisão
estão no `README.md` ao lado.

Ordem importa: cada parte só funciona depois da anterior. Faça de cima para
baixo e teste ao fim de cada bloco.

---

## 0. Antes de tudo

1. `Japan Market → Catálogo → Reconstruir produtos` (e móveis, objetivos, lixo,
   empréstimos). Os catálogos se populam sozinhos no import, mas a primeira vez
   depois de um clone precisa de um empurrão.
2. `Japan Market → Sandbox → Criar ou recriar cena Sandbox`.

A Sandbox já vem com relógio, caixa, spawner de cliente e depósito montados.
**Monte tudo nela primeiro.** Só depois repita na cena principal — é a diferença
entre descobrir um erro em 5 segundos e em 5 minutos.

---

## 1. Game Context — obrigatório

Sem isto, nada dos sistemas novos funciona.

Crie um GameObject vazio chamado `— Game Context —` e adicione:

- **GameContext**
- **GameClockRunner**

> O `ObjectiveRunner` é adicionado sozinho pelo GameContext. Não procure por ele
> no menu Add Component — ele está escondido de propósito, porque é encanamento
> e não uma peça que você monta.

### Campos do GameContext

| Campo | O que pôr | Se ficar vazio |
|---|---|---|
| Item Catalog | o `ItemCatalog` do projeto | avisa no console; loja sem produtos |
| Furniture Catalog | o `FurnitureCatalog` | avisa no console |
| Objective Catalog | o `ObjectiveCatalog` | sem objetivos (silencioso, de propósito) |
| Trash Catalog | o `TrashCatalog` | não aparece lixo |
| Loan Catalog | o `LoanCatalog` | banco sem faixas; save perde contratos |
| Tool Belt | o `ToolBeltLayout` | jogador de mãos vazias |

Os outros campos são números de balanceamento e já vêm com valores utilizáveis.
Os que você provavelmente vai querer mexer: **Opening Balance** (saldo inicial),
**Daily Rent** (aluguel), **Delivery Hours** (prazo de entrega) e
**Dirt Tolerance** (com quantas sujeiras os clientes começam a ir embora).

**Teste:** aperte Play. O console deve listar os catálogos validados e nenhum
erro. Use o menu de contexto do `GameClockRunner` (botão direito no componente)
para abrir a loja e encerrar o dia.

---

## 2. Compras e entrega

O jogador compra estoque no computador, e as caixas aparecem no depósito quando
o prazo vence.

### 2.1 Em cada produto (`ItemDefinition`)

- **Box Prefab** — o que aparece no depósito. Sem isto a caixa é paga e não
  materializa; o console avisa.

### 2.2 Na cena

GameObject `— Depósito —`, perto da porta dos fundos, com:

- **DeliverySpawner**
  - `Drop Point` → um filho vazio onde as caixas nascem
  - `Max Boxes On Floor` → 30 está bom; o teto se libera sozinho conforme o
    jogador guarda as caixas

**Teste:** Play → compre estoque → espere o prazo (ou ponha `Delivery Hours` em
0) → as caixas aparecem no depósito.

---

## 3. Lixo e reciclagem

O cliente suja, o jogador separa em sacos, leva até os fundos, e o caminhão
paga no fechamento do dia.

### 3.1 Assets (Create → Japan Market → Trash)

1. **Category** — uma por tipo: plástico, metal, papel, orgânico.
   - `Key` → texto minúsculo sem espaço: `plastico`, `metal`. **É o que o save
     guarda.** Mudar depois perde os sacos pendentes.
   - `Full Bag Bonus` → o bônus por entregar o saco cheio de uma categoria só.
     É isto que faz separar valer a pena; com zero, três sacos pela metade
     rendem igual a um cheio.
2. **Item** — um por lixo: garrafa, lata, embalagem.
   - `Key` → idem, estável
   - `Category`, `Prefab`, `Value`, `Spawn Weight`
3. **Catalog** — um só no projeto. Populado pelo import.

### 3.2 Prefab do lixo

Qualquer modelo + **TrashItem** + Collider + Rigidbody.
Não preencha o campo `Definition` — o spawner preenche.

### 3.3 Prefab do saco

Um saco + **TrashBagItem** + Collider + Rigidbody.
Ele precisa ser agarrável pelo seu sistema de segurar objetos.

### 3.4 Lixeira (móvel)

Prefab com:

- **FurnitureInstance**
- **TrashBin**
  - `Bag Capacity` → 10
  - `Bag Prefab` → o prefab do saco
  - `Bag Spawn Point` → filho vazio de onde o saco sai
- **Collider** marcado como **Is Trigger** (é por onde o lixo entra)

> A lixeira **não** tem categoria fixa. O saco trava na categoria do primeiro
> lixo que entra. É isso que transforma "juntar lixo" em "separar lixo".

### 3.5 Doca e spawner

- GameObject `— Doca de Reciclagem —` nos fundos, com **TrashDock** + Collider
  **Is Trigger**.
- GameObject qualquer com **TrashSpawner** (o catálogo ele resolve sozinho).

### 3.6 Tirar o saco: uma linha de cola

O `TrashBin` expõe `TryReleaseBag(out GameObject)`. Chame do seu sistema de
interação quando o jogador apertar "usar" na lixeira. O componente que faz isso
tem que morar em `Assets/Scripts/` (o `IInteractable` legado vive lá, e
`JapanMarket.Gameplay` não pode referenciar aquele assembly):

```csharp
using JapanMarket.Gameplay;
using UnityEngine;

[RequireComponent(typeof(TrashBin))]
public class TrashBinInteraction : InteractableBase
{
    private TrashBin _bin;

    private void Awake() => _bin = GetComponent<TrashBin>();

    public override void Interact() => _bin.TryReleaseBag(out _);
}
```

Ajuste o nome do método ao do seu `InteractableBase`.

**Teste:** Play → abra a loja → espere um cliente sujar → jogue o lixo na
lixeira → tire o saco → leve até a doca → encerre o dia. O dinheiro entra no
relatório **daquele** dia.

---

## 4. Ferramentas e limpeza

### 4.1 Assets (Create → Japan Market → Tool)

1. **Surface** — uma por tipo de superfície: chão, vidro, balcão.
2. **Tool** — uma por ferramenta: esponja, rodo, tablet, engradado, taco.
   - `Works On` → as superfícies onde ela funciona. **Vazio = não limpa nada**,
     que é o certo para tablet e engradado.
   - `Max Uses` → 0 significa que nunca quebra.
   - `Held Prefab` → o modelo que aparece na mão.
   - `Unlock` → deixe vazio, ou aponte um `StoreLevelUnlock` / `FlagUnlock`.
3. **Belt Layout** — um só. Um slot por ferramenta, na ordem da roda. O `Unlock`
   do slot é o que o mantém travado.

### 4.2 No jogador

Adicione **ToolUser** ao objeto do jogador:

- `Hand` → o transform onde o modelo da ferramenta nasce
- `Aim Origin` → vazio usa a `Camera.main`
- `Reach` → 3 é um bom começo

O `ToolUser` **não lê teclado de propósito**, para não brigar com o seu
`PlayerInput`. Chame do seu input:

- `Select(int slot)` — troca a ferramenta na mão
- `UseOnAim()` — usa no que estiver na mira
- `PeekAim()` — o que dá para fazer agora, para o cursor mudar antes do clique

### 4.3 Sujeira

Prefab com **Grime** + Collider:

- `Surface` → a superfície onde ela está
- `Toughness` → quantas esfregadas aguenta

Espalhe alguns na cena, ou spawne pelo seu sistema.

**Teste:** Play → selecione a esponja → esfregue uma sujeira de chão (some) →
tente numa de vidro (não acontece nada e a esponja **não** gasta).

---

## 5. Objetivos

### 5.1 Assets (Create → Japan Market → Objective)

1. Uma **condição** por meta: Vender itens, Faturar em vendas, Atender clientes,
   Sobreviver dias, Receber caixas, Reciclar lixo, Chegar ao nível.
   - `Target` → quanto precisa
2. Um **Objective** por objetivo:
   - `Title` / `Description`
   - `Conditions` → **todas** precisam ser cumpridas
   - `Reward` → dinheiro, XP e/ou uma `Flag`
   - `Unlock` → vazio para os primeiros
   - `Sort Order` → a ordem em que entram
3. **Objective Catalog** — um só. Populado pelo import.

> O `Id` é gerado sozinho no import. **Não duplique um objetivo com Ctrl+D** — o
> id vai junto, e os dois dividiriam o mesmo progresso no save. Crie pelo menu.
> Se acontecer, `Japan Market → Catálogo → Validar objetivos` acusa.

### 5.2 Corrente de objetivos

Sem nenhum sistema novo: o objetivo A põe `primeira_venda` no campo `Flag` da
recompensa; o objetivo B recebe um asset **Unlock → Flag** com `Flag =
primeira_venda` no campo `Unlock`. Nenhum dos dois conhece o outro.

O mesmo `FlagUnlock` serve em produto, móvel, faixa de empréstimo e slot de
ferramenta.

**Teste:** Play → faça o que o primeiro objetivo pede → a recompensa cai no caixa
e o segundo objetivo entra.

---

## 6. Banco

Assets **Create → Japan Market → Loan Tier**, um por faixa:

- `Key` → estável (`inicial`, `medio`, `alto`). É o que o save guarda.
- `Principal`, `Daily Payment`, `Term Days` — todos precisam ser positivos, ou o
  banco recusa o contrato
- `Unlock` → opcional

E um **Loan Catalog**, um só no projeto.

---

## 7. Save

Já está ligado no `GameContext`:

- **Save On Day End** (ligado) — salva no fechamento de cada dia
- **Load On Start** (desligado) — ligue quando quiser continuar a partida ao
  entrar em Play

Menu de contexto do `GameContext` (botão direito no componente): **Salvar
agora**, **Carregar agora**, **Apagar o arquivo**.

O arquivo fica em `Application.persistentDataPath` — no Windows,
`%UserProfile%\AppData\LocalLow\<Empresa>\<Jogo>\japanmarket.save.json`. É JSON
legível: dá para abrir e conferir.

**O que o save guarda:** relógio, saldo, nível e XP, flags, tabela de preços,
pedidos a caminho e fila de entrega, empréstimos em aberto, progresso dos
objetivos, sacos na doca, desgaste das ferramentas.

**O que ele ainda NÃO guarda:** a posição dos móveis. Não existe sistema de
colocação em runtime (Fase 3b), então hoje não há o que restaurar — os móveis
estão postos na cena à mão e voltam com ela. Quando a colocação existir, ela
entra no save como mais um bloco.

---

## 8. Telas

Elas se MONTAM sozinhas. Não existe hierarquia para você construir no Inspector,
nem prefab para arrastar — cada tela constrói os próprios widgets a partir do
serviço que ela mostra.

### 8.1 O computador

No objeto que o `Computer` legado liga e desliga (o campo `computerScreen` dele),
adicione o componente **ComputerAppHost**. Só isso.

Ele monta a barra de apps, o saldo e o relógio, e cria os cinco apps: Mercado,
Preços, Objetivos, Banco e Estatísticas. Os checkboxes no Inspector escondem um
app que você não quiser agora.

> O objeto precisa estar dentro de um **Canvas**. Se o `computerScreen` já for um
> filho do Canvas da tela do PC, está pronto.

O que cada app faz:

| App | Lê de | O que dá para fazer |
|---|---|---|
| Mercado | `IMarketOrderService` | escolher caixas, ver o total, pedir, acompanhar o que vem |
| Preços | `IPricingService` | ajustar preço em degraus de ¥10, voltar ao de mercado |
| Objetivos | `IObjectiveService` | ver progresso de cada meta e a recompensa |
| Banco | `IBankService` + `ILoanCatalog` | contratar faixa, ver dívida, quitar antes |
| Estatísticas | `IDailyReportService` | o dia de hoje linha a linha, e o histórico |

### 8.2 A barra de ferramentas

No Canvas do HUD, um GameObject vazio com **ToolWheelView**. Também só isso.

Ela mostra os cinco slots com número, nome, barrinha de desgaste e o travado
escrito. Os atalhos 1–5 e o botão direito continuam vindo do `SetupToolInput`, que
já está no jogador — a barra só desenha.

### 8.3 Se a tela aparecer em branco

Quase sempre é fonte: `Window → TextMeshPro → Import TMP Essential Resources`. O
`UIKit` avisa uma vez no console quando o projeto não tem fonte padrão.

## 9. O que ainda falta, e por que não está feito

Sendo direto, porque isto muda o que você pode esperar do projeto hoje:

**A arte das telas.** As seis telas existem e funcionam (seção 8), mas são
cinzas e quadradas de propósito. Os controladores são ligados aos DADOS, não ao
layout: trocar o `UIKit` por prefabs desenhados depois não toca em nenhuma regra
nem em nenhum app.

**Colocação de móveis (Fase 3b).** O `PlacementController` e a geração dos
`FurnitureDefinition` ficaram para depois porque dependem de escolhas suas na
cena. É o que falta para o save cobrir a loja inteira.

**A limpeza do legado.** Aqui eu preciso ser honesto: **não dá para apagar nada
com segurança hoje.** Eu mapeei as dependências antes de mexer:

| Alvo | Quem ainda depende | Por que não dá |
|---|---|---|
| `enum Items` | 9 arquivos | `Shelf`, `Segment`, `ItemBox`, `ItemManager` — é o sistema de produto em prateleira inteiro |
| `AllIThingsData` | 11 arquivos **e 3 assets** | os `.asset` de Ketchup, Shelf e Expansão são instâncias dele |
| `GlobalPrices` | 4 arquivos | `Segment`, `PriceDisplay`, `NumbersDisplay` leem preço dali |
| `ShopBuyItems` | 2 arquivos | `ExpansionStore` |
| `NpcTraject` | 2 arquivos | `CashRegister` |
| `FurnitureSaveData` | 2 arquivos | o `FurnituresManager` usa `SaveData != null` como marcador de "estou movendo um móvel que já existia" — apagar muda comportamento |
| `ServiceLocator` | **40 arquivos** | metade do jogo ainda passa por ele |

Apagar qualquer um desses agora quebra o jogo, e quebra de um jeito difícil de
diagnosticar (referência perdida em prefab e cena, não erro de compilação). A
ordem correta para removê-los é substituir primeiro quem depende deles:

1. **Prateleiras** — reescrever `Shelf`/`Segment`/`ItemBox` sobre
   `IProductStorage` (que já existe e já é usado pelo NPC). Isso mata `Items`,
   `AllIThingsData` e `GlobalPrices` de uma vez, porque são os três que aquele
   sistema consome.
2. **Colocação de móveis** (Fase 3b) — mata `FurnitureSaveData`.
3. **Loja e expansão** — mata `ShopBuyItems`.
4. **`ServiceLocator` por último**, quando a lista de quem o consome couber numa
   tela. Hoje ele é a fachada que mantém o jogo de pé enquanto a migração
   acontece; apagá-lo antes da hora é trocar uma dívida por um jogo que não roda.

Cada um desses é uma fase, não um passo de limpeza — e cada um precisa de
decisões suas na cena. Preferi te entregar isso escrito a apagar arquivos e te
deixar com um projeto quebrado que eu não consigo depurar daqui.
