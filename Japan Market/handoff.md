# Handoff — sistemas montados na Main

Atualizado em 18/09/2026. Projeto Unity 6000.2.7f2.

## Estado atual

A montagem baseada em `C:\Users\Ariele\Downloads\SETUP.md` foi gravada em `Assets/Scenes/Main.unity` e `Assets/Scenes/Sandbox.unity`. O usuário confirmou que Main é a cena de jogo e pediu este handoff e um README de funcionalidades e uso.

**Validação automática concluída: 292/292 testes EditMode aprovados; 18 checks em cada cena e 5 regressões específicas aprovados em Play, com zero erros. As hierarquias da HUD de objetivos e do fluxo de fim do dia também passaram nas validações dedicadas.** Isso não equivale à aprovação manual do fluxo inteiro pelo jogador: arte, sensação da porta e operações completas de computador, abastecimento e caixa ainda precisam de revisão manual.

## Correções concluídas em 18/09

- Foi montado `Player/Canvas/Objective Tracker`, painel de HUD ancorado no canto superior direito. Durante o tutorial, a HUD e a aba Objetivos do computador mostram somente `Finish tutorial`; as metas econômicas não progridem nem recompensam nesse período. Depois elas começam do zero e as telas passam para até três metas ativas.
- `ITutorialStatus` mantém a UI desacoplada das etapas concretas do tutorial. O montador idempotente e sua validação estão em `Assets/Editor/JapanMarket/ObjectiveHudSetup.cs`.
- O primeiro dia agora fica protegido contra fechamento automático/manual até três vendas concluídas (quantidade configurável no `TutorialManager`). A terceira venda agenda o fechamento para o frame seguinte, depois que o caixa termina de remover o cliente da fila.
- O relógio visível para antes do fechamento durante essa proteção, mas `TotalHours` continua avançando para entregas. Dias posteriores oferecem a decisão às 21h e encerram obrigatoriamente às 00h.
- `Player/Canvas/Day Flow UI` contém a decisão modal das 21h e o resumo completo. **Finalizar dia** fecha imediatamente; **Continuar aberto** mantém porta/clientes até 00h sem repetir o modal; meia-noite fecha e mostra o relatório. `DaySummaryView` consome `ReportClosed` e `DayEndDecisionView` coordena a decisão, pausa e cursor.
- O montador idempotente e a validação estão em `Assets/Editor/JapanMarket/DayFlowUISetup.cs`; a cena ficou com um único EventSystem e três botões ligados em runtime.
- A porta provisória foi removida. O gatilho agora anima `Market/LojaCartoon/Doors/Window/Cube.010`, o segmento central do modelo já posicionado, e continua enviando `EnteredStore` ao tutorial.
- A tela do computador exibe cinco abas: Mercado, Preços, Objetivos, Banco e Estatísticas. O layout foi corrigido para manter cabeçalho em 44 px, abas em 40 px e o corpo ocupando o restante.
- O Price Display pode iniciar oculto/inativo e é reativado por `ShowDisplay`, evitando o travamento da etapa `HasPutPrice`.
- As teclas 1–5 agora alternam a ferramenta: pressionar novamente o mesmo número guarda o item. A tecla 0 continua esvaziando as mãos.
- O setup idempotente está em `Assets/Editor/JapanMarket/MainSetupFinisher.cs`; a regressão automatizada está em `RegressionDiagnostics.cs`.

O guia de uso está no [README.md](README.md). A arquitetura anterior está em [Assets/JapanMarket/README.md](Assets/JapanMarket/README.md).

## O que foi feito

| Área | Implementação |
|---|---|
| Assets e catálogos | Cinco catálogos configurados; produtos migrados dos dados legados; quatro categorias/resíduos; saco e lixeira; três superfícies; cinco ferramentas/cinto; sete objetivos; três empréstimos. |
| Main | GameContext/GameClockRunner, depósito, doca, TrashSpawner, lixeira nova, sujeiras de exemplo e ToolUser/SetupToolInput no jogador. TrashSystem legado desativado para evitar geração simultânea dos dois sistemas. |
| Sandbox | Cliente simples, NavMesh gerada, prateleira com estoque inicial, caixa automático e sistemas configurados. |
| Computador | Cinco abas funcionais montadas no Canvas existente. Compra de alimentos passa pelo MarketOrderService. Compra de móveis continua no fluxo legado. |
| Entrega | DeliverySpawner inicializa o produto no ItemBox por IStockDeliveryReceiver. Corrigida a importação das malhas da caixa para QuickOutline. |
| Reciclagem | TrashBinInteraction conecta retirada de saco ao InteractableBase. Resíduos e sacos usam Rigidbody, Collider, HoldableItem, Outline e camada Interactive. |
| Ferramentas | 1–5 selecionam e o mesmo número desequipa; 0 também desequipa; botão direito usa na mira. Cursor livre, pausa e objeto carregado bloqueiam uso. |
| Relógio/placa | MarketManager consulta/alimenta o relógio novo. Placa relê o estado ao interagir, resolve referências tardias e recusa abertura após o horário. |
| Caixa/objetivos | SaleCompleted publica receita, custo, quantidade e forma de pagamento. SalesAccountant credita uma vez. Pagamentos bloqueiam confirmação repetida durante animação. |
| Clientes/relatórios | Registro de clientes publica CustomerEntered. Cliente atendido é removido da lista com Purchased; a remoção posterior não o conta como perdido. |
| Persistência dos assets | Condições de objetivos e desbloqueios separadas em arquivos próprios para o Unity gravar referências de script válidas. |
| Save | Version tem default 0: JSON sem versão não assume que é save válido. SaveService.Capture continua gravando CurrentVersion explicitamente. |
| Testes | Controlador de Play corrigido para reload de domínio desativado, com timeout e save/load desligados na cópia de teste. Teste de recompensa de XP passou a medir o acréscimo da recompensa separadamente do XP legítimo das vendas. |

Os serviços centrais já existiam no repositório. Esta tarefa criou/configurou assets e cenas, ligou sistemas legados e corrigiu problemas encontrados na montagem e validação.

## Arquivos principais

- `Assets/Editor/JapanMarket/MainSetupBuilder.cs`: geração/configuração; também contém RepairDeliveryImports.
- `Assets/Editor/JapanMarket/SandboxSceneBuilder.cs`: corrigido o componente FurnitureInstance para o namespace JapanMarket.Gameplay, em vez do global legado.
- `Assets/Editor/JapanMarket/SetupValidation.cs`: checks de Play nas duas cenas.
- `Assets/Editor/JapanMarket/SetupInspection.cs`: inventário da cena.
- `Assets/JapanMarket/Setup/`: assets, prefabs e materiais criados.
- `Assets/JapanMarket/Data/Products/Generated/`: produtos gerados pelo migrador existente.
- `Assets/JapanMarket/Data/Objectives/*Condition.cs` e `Data/Unlocks/StoreLevelUnlock.cs` / `AllOfUnlock.cs`: classes separadas para serialização.
- `Assets/JapanMarket/Gameplay/Market/DeliverySpawner.cs` e `IStockDeliveryReceiver.cs`.
- `Assets/Scripts/Prateleiras/ItemBox.cs`.
- `Assets/Scripts/RecycleSystem/TrashBinInteraction.cs`.
- `Assets/Scripts/Player/SetupToolInput.cs` e `Assets/Scripts/SandboxStock.cs`.
- `Assets/Scripts/Shop/ShopBuyItems.cs`, `Market/MarketManager.cs`, `StoreSign.cs`.
- `Assets/Scripts/CashRegistrer/CashRegister.cs`, `PaymentCard.cs`, `PaymentMoney.cs`.
- `Assets/JapanMarket/Domain/Save/GameSave.cs` e `Assets/JapanMarket/Tests/ObjectiveServiceTests.cs`.
- `Assets/Mystery Studio/CArdBoard 1.fbx.meta`: Read/Write habilitado para QuickOutline ler os modelos das caixas.

## Evidências de validação

### Play nas cenas

`Logs/setup-validation.txt`: `SCENE PASS Sandbox`, `SCENE PASS Main`, `ERRORS 0`.

Por cena: GameContext, catálogos/caixas, cinco slots, seleção da esponja, rejeição de vidro sem desgaste, limpeza de chão, descarte e retirada de saco carregável, trigger da doca, compra de estoque, sete objetivos válidos, empréstimos registrados, caixa materializada, entrada física do saco na doca, primeira venda liberando flag, save/restauração em memória, fechamento e coleta da reciclagem.

Execução final com `-batchmode -force-d3d11`, mantendo câmeras ativas. O teste tenta capturar screenshots, mas **nenhuma imagem foi produzida em batch**; não apresentar essas tentativas como revisão visual.

Os checks de objetivos usam eventos e os de save usam memória. Não foram testados todos os cliques de pagamento, menus ou recuperação de save real em disco.

### EditMode

`Logs/editmode-day-flow.xml`: **292 testes; 292 passaram; 0 falhas** em 18/09/2026. Inclui as regressões da trava de horário, do fechamento após a terceira venda, da suspensão de objetivos durante o tutorial e o novo intervalo 21h→00h.

`Logs/objective-hud-validation.log`: `OBJECTIVE_HUD_VALIDATION_PASS` no Canvas `Player/Canvas`, retângulo `440 x 300`, referências serializadas e texto inicial `Finish tutorial`.

`Logs/day-flow-ui-validation.log`: `DAY_FLOW_UI_VALIDATION_PASS` no Canvas `Player/Canvas`, 3 botões, um EventSystem, decisão às 21h e fechamento às 00h.

`Logs/editmode-four-fixes.xml`: execução anterior com **287 testes; 287 passaram; 0 falhas**.

`Logs/regression-diagnostics.txt`: **5/5 PASS** para alternância de ferramenta, movimento do modelo real da porta, reativação do Price Display, altura do cabeçalho e presença/layout das cinco abas.

A primeira execução encontrou duas falhas e ambas foram corrigidas: default de versão do save e expectativa de XP no teste de recompensa. A regra de XP por venda não foi alterada.

`git diff --check` dos arquivos C# alterados nesta retomada passou. Materiais serializados pelo Unity têm espaços finais em campos vazios; não foram reformatados à mão.

## O que falta fazer

1. **Revisão manual/visual na Main**: confirmar posições, acessibilidade, chão, colisões, alcance, outline, pickup e leitura das duas telas de fim do dia em diferentes resoluções. O montador usou aproximadamente `(25, 0.1, 5)` como referência do depósito/doca; respeitar ajustes manuais posteriores.
2. **Fluxo completo pela interface**: comprar alimento → esperar entrega → pegar caixa → abastecer prateleira → atender três clientes → confirmar resumo; no dia 2 testar as duas opções das 21h e o fechamento às 00h.
3. **Placa e relógio**: testar visualmente que **Continuar aberto** mantém placa/clientes e que o evento das 00h sincroniza o modelo da placa com o estado fechado.
4. **Polimento de objetivos/banco/ferramentas**: revisar textos, motivos de recusa, conserto/reposição, ícones e arte final. As telas e a barra já estão funcionais.
5. **Arte**: substituir modelos provisórios de resíduos, saco, lixeira, ferramentas e manchas; posicionar sujeira de vidro em uma superfície visual adequada.
6. **Tablet, engradado e taco**: implementar ações próprias. Hoje só são selecionáveis e exibidos na mão.
7. **Migração futura descrita no item 8 do SETUP**: prateleiras/NPCs restantes, colocação de móveis, save das posições/conteúdos legados e remoção gradual do legado. Não remover ServiceLocator/Items/AllIThingsData sem migrar consumidores.
8. **Save real**: testar escrita/leitura em arquivo separado ou com backup, e revisar o arquivo existente conforme o aviso abaixo.
9. Antes de commit, revisar o Git atual. O usuário consolidou arquivos entre mensagens; não presumir que todo arquivo modificado pertence ao assistente. Nenhum commit/push foi feito por esta tarefa.

## Save existente — atenção ao retomar

Na primeira sessão, um teste ficou rodando com save automático ligado porque o controlador dependia de reload de domínio. Existe um arquivo em:

`C:\Users\Ariele\AppData\LocalLow\DefaultCompany\Daichi Market\japanmarket.save.json`

Última conferência: **1181 bytes, modificado em 15/09/2026 22:30:07 local**. O horário é compatível com aquele teste. Não havia backup prévio e não foi possível confirmar qual estado existia antes. O arquivo foi preservado; não apagar nem presumir que seja uma partida desejada. Conferir antes de habilitar Load On Start.

As execuções corrigidas desativam save/load apenas na cópia de teste, sem salvar esses ajustes nas cenas. O arquivo manteve tamanho e horário após essas execuções.

## Como retomar tecnicamente

- Skills usadas: `.agents/skills/unity-cli/SKILL.md`, `.agents/skills/ui-ugui/SKILL.md` e `.agents/skills/initialize-ai-navigation/SKILL.md`.
- Editor: `C:\Program Files\Unity\Hub\Editor\6000.2.7f2\Editor\Unity.exe`.
- Unity CLI instalado na primeira sessão: `C:\Users\Ariele\AppData\Local\Unity\bin\unity.exe`, versão 1.0.0-beta.9. Pode não estar no PATH da shell.
- Não foi instalado com.unity.pipeline. As cenas e assets foram modificados por C# no Editor; não editar YAML com um Editor conectado.
- Confira processos e linha de comando antes de abrir outro Editor. Não reutilizar PIDs de sessões anteriores nem executar montagem e testes simultaneamente.
- A última instância problemática da primeira sessão já não existia nesta retomada. Uma tentativa antiga de encerramento foi bloqueada por quota da revisão automática; esse bloqueio não impediu a retomada.
- SetupValidation.Run deve ser executado **com gráficos ativos**, sem `-nographics`, pois o caixa depende de Camera.main e a Main usa URP.
- Os testes EditMode podem usar `-nographics`: Unity CLI `test`, `--mode EditMode --filter JapanMarket.Tests --output Logs/setup-editmode.xml --timeout 600`.
- Não executar MainSetupBuilder.Run rotineiramente: ele reaplica valores/listas e pode sobrescrever balanceamento manual. RepairDeliveryImports é a correção específica de importação, sem remontar as cenas.
- Houve crash interno do Unity ao sair após um salvamento na primeira sessão; as execuções posteriores com gráficos ativos concluíram. Avisos preexistentes de pacotes/Visual Scripting devem ser analisados separadamente.

## Documentação entregue

`README.md` contém as funcionalidades disponíveis, teclas, fluxo de compra/reciclagem, configuração de ferramentas e objetivos, faixas de empréstimo, save, uso da Sandbox e limitações. Este handoff contém o estado técnico, evidências e próximas tarefas.
