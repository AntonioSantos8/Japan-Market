# Japan Market — funcionalidades e uso

Abra o projeto com **Unity 6000.2.7f2**. A cena de jogo é `Assets/Scenes/Main.unity`; `Assets/Scenes/Sandbox.unity` é o ambiente de teste dos sistemas novos.

Este guia descreve a montagem feita a partir do SETUP.md. O estado da validação e as pendências estão em [handoff.md](handoff.md). A arquitetura dos serviços já existentes está em [Assets/JapanMarket/README.md](Assets/JapanMarket/README.md).

Validação automática em 18/09/2026: **287 testes EditMode aprovados**, **18 checks em cada cena aprovados em Play** e **5 regressões de interface/gameplay aprovadas**, com zero erros. A revisão manual de arte, posicionamento e do fluxo completo jogado ainda está pendente.

## O que foi adicionado ou conectado

| Sistema | Disponível nesta montagem |
|---|---|
| Contexto e relógio | GameContext com cinco catálogos, cinto de ferramentas e GameClockRunner nas cenas. Abertura da loja ligada ao relógio. |
| Computador | Cinco abas funcionais: Mercado, Preços, Objetivos, Banco e Estatísticas. A compra cria pedidos com prazo; o depósito materializa as caixas. |
| Reciclagem | Quatro categorias, resíduos físicos, lixeira, saco carregável e doca. Pagamento pelo serviço no fechamento do dia. |
| Limpeza | Três superfícies, esponja e rodo, seleção por teclado, uso na mira, desgaste e sujeiras de exemplo. |
| Objetivos | Sete objetivos configurados, recompensas de dinheiro/XP e uma sequência liberada pela primeira venda. |
| Caixa | Vendas legadas notificam objetivos e contabilidade; o crédito é centralizado para evitar pagamento duplicado. |
| Banco | Três faixas de empréstimo configuradas e tela de contratação/quitação no computador. |
| Save | Configurado para salvar ao fechar o dia; carregamento automático inicialmente desligado. |
| Sandbox | Cliente de teste, navegação, prateleira com estoque inicial, caixa automático e sistemas montados. |

Os serviços centrais de economia, objetivos, banco e save já existiam. Esta entrega criou os assets, montou as cenas e conectou partes do jogo legado. Modelos de ferramentas, lixo, saco, lixeira e sujeira são provisórios. Tablet, engradado e taco são selecionáveis, mas ainda não têm ação própria.

## Começar na Main

1. Abra `Assets/Scenes/Main.unity` e entre em Play.
2. Entre pela porta de madeira existente: o segmento central sobe automaticamente e libera a primeira etapa do tutorial.
3. Use a placa existente para abrir a loja.
4. Abra o computador e use as abas no topo. A cobrança de Mercado acontece no pedido e a entrega chega depois do prazo configurado.
5. Procure `— Depósito —` na Hierarchy para localizar o ponto de entrega. Pegue a caixa pelo sistema de interação existente e abasteça as prateleiras.
6. Atenda no caixa normalmente. A finalização da venda alimenta o saldo e os objetivos.

No objeto `— Game Context —`, os campos Opening Balance, Daily Rent e Delivery Hours controlam saldo inicial, aluguel e prazo. Para testar entrega imediata, mude temporariamente Delivery Hours para 0 **antes de Play**. Evite gravar esse ajuste na cena se quiser manter o prazo normal.

A compra de móveis continua usando o fluxo anterior. As interfaces de Mercado, Preços, Banco, Objetivos e Estatísticas estão funcionais e montadas por código; o visual ainda é uma base técnica que pode receber arte final depois.

## Ferramentas

| Tecla | Ferramenta | Superfícies/ação |
|---|---|---|
| 1 | Esponja | Limpa chão e balcão. |
| 2 | Rodo | Limpa chão e vidro. |
| 3 | Tablet | Modelo na mão; ação ainda pendente. |
| 4 | Engradado | Modelo na mão; ação ainda pendente. |
| 5 | Taco | Modelo na mão; ação ainda pendente. |
| 0 | Mãos vazias | Desequipa a ferramenta. |
| Botão direito | Usar | Aplica a ferramenta na sujeira sob a mira, até 3 metros. |

Pressionar novamente a tecla da ferramenta selecionada também a guarda (por exemplo, `1` seleciona a esponja e `1` novamente volta às mãos vazias). É necessário estar controlando o jogador com o cursor capturado. A seleção fica bloqueada em menus ou durante pausa; carregar um objeto desequipa a ferramenta. O botão esquerdo continua reservado à interação existente.

Esponja e rodo foram configurados com 100 usos. Uma tentativa em superfície incompatível não deve gastar usos. Ainda não existe interface de conserto ou reposição; para desenvolvimento, o serviço `IToolBelt.TryRepair(slot)` restaura a ferramenta. A barra visual mostra seleção, bloqueio e desgaste; arte/ícones finais ainda podem ser substituídos.

Configuração: `Assets/JapanMarket/Setup/ToolBeltLayout.asset`, `Esponja.asset`, `Rodo.asset` e `Superficie_*.asset`. ToolUser fica no jogador; Tool Hand fica sob a câmera.

## Separar lixo e receber pela reciclagem

1. Com clientes na loja, aguarde o TrashSpawner gerar lixo. Há chance por tentativa; não é imediato nem garantido a cada intervalo.
2. Pegue o resíduo usando a interação normal.
3. Solte-o na região de entrada da lixeira nova. O primeiro resíduo determina a categoria do saco; misturar categorias é recusado.
4. Use a lixeira para retirar o saco, mesmo que ainda não esteja cheio.
5. Pegue o saco e leve à `— Doca de Reciclagem —`.
6. Feche o dia. O serviço contabiliza os sacos entregues no relatório daquele dia.

Foram criadas as categorias `plastico`, `metal`, `papel` e `organico`. Cada resíduo vale ¥5; o saco comporta 10 itens; um saco cheio da mesma categoria recebe bônus de ¥50. São valores iniciais de teste.

Use a lixeira nova com `JapanMarket.Gameplay.TrashBin` e `TrashBinInteraction`. As lixeiras decorativas/legadas ainda existentes na Main não foram todas convertidas. O TrashSystem antigo foi desativado na montagem para evitar geração simultânea dos dois tipos de lixo.

As chaves de categorias, resíduos e empréstimos participam do save: mantenha-as estáveis ao renomear assets.

## Objetivos

| Objetivo | Condição |
|---|---|
| Primeira venda | Vender 1 item; libera a flag `primeira_venda`. |
| Atenda cinco clientes | Atender 5 clientes após a liberação pela primeira venda. |
| Receba uma caixa | Receber 1 caixa de estoque. |
| Separe dez resíduos | Colocar 10 resíduos em lixeiras; a condição atual conta a separação, não a entrega na doca. |
| Conclua o primeiro dia | Encerrar 1 dia. |
| Fature mil ienes | Faturar ¥1.000 após a liberação pela primeira venda. |
| Chegue ao nível dois | Chegar ao nível 2 da loja. |

Cada objetivo foi configurado com recompensa de ¥100 e 25 XP. O GameContext limita a quantidade simultaneamente ativa; por padrão, 3. Progresso e recompensas aparecem na aba **Objetivos** do computador.

Assets: `Assets/JapanMarket/Setup/ObjectiveCatalog.asset` e os pares de objetivo/condição na mesma pasta. Ao criar novos objetivos, use o menu de criação em vez de duplicar IDs existentes.

## Banco

| Faixa | Recebe | Parcela diária | Prazo | Liberação |
|---|---:|---:|---:|---|
| Inicial | ¥2.000 | ¥250 | 10 dias | Desde o início |
| Médio | ¥5.000 | ¥600 | 10 dias | Nível 3 |
| Alto | ¥10.000 | ¥1.200 | 10 dias | Nível 6 |

Os assets estão em `Assets/JapanMarket/Setup/Emprestimo_*.asset`. O limite inicial é um contrato simultâneo. A aba **Banco** permite contratar as faixas disponíveis, mostra dívida/parcelas e permite quitar antes. Os valores são de balanceamento do jogo.

## Relógio e save no Editor

Durante Play, clique com o botão direito no componente GameClockRunner para usar `Debug/Open the store`, `Debug/Close the store` e `Debug/End the day now`. Encerrar o dia processa receitas de reciclagem, despesas e relatórios.

No GameContext:

- **Save On Day End**: ligado na configuração normal.
- **Load On Start**: desligado; ligue para continuar um save ao entrar em Play.
- Menu de contexto **Save/Salvar agora** e **Save/Carregar agora**: operações manuais.
- **Save/Apagar o arquivo**: apaga a partida salva; use somente quando quiser descartá-la.

O arquivo é `japanmarket.save.json`, dentro de `Application.persistentDataPath`. No Windows, normalmente fica em `%USERPROFILE%\AppData\LocalLow\<Empresa>\<Jogo>`. Faça backup antes de testes: Main e Sandbox podem compartilhar o mesmo caminho. A execução automática corrigida de SetupValidation desativa o salvamento na cópia de teste.

Neste projeto, a pasta configurada é `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Daichi Market`. Um teste anterior ficou rodando com save automático ligado; existe um arquivo modificado durante esse período. Confira seu conteúdo e faça uma cópia antes de ativar Load On Start. Ele foi preservado; não foi possível confirmar se havia uma partida anterior nele.

O save cobre estado econômico, relógio, nível/XP, flags, preços do serviço novo, pedidos, contratos, objetivos, sacos já depositados e desgaste. Não representa uma cópia completa da cena: posição de móveis, conteúdo/estado dos sistemas legados e resíduos físicos fora da doca não devem ser considerados persistidos sem implementação adicional.

## Sandbox e manutenção

Abra a Sandbox para observar o cliente simples navegando, comprando do estoque inicial e usando o caixa automático. Ela é um ambiente técnico e ainda não tem o mesmo fluxo completo de jogador e computador da Main.

`MainSetupBuilder.Run()` foi usado para gerar a montagem. Não é necessário executá-lo para jogar e não deve ser usado como inicializador a cada abertura: ele reaplica balanceamento e referências, podendo sobrescrever ajustes posteriores. Faça backup/commit antes de uma regeneração intencional.

Para validar regras, use os testes EditMode do assembly `JapanMarket.Tests`. O script de Editor `SetupValidation.Run()` testa componentes e serviços nas duas cenas; consulte `Logs/setup-validation.txt` e `Logs/setup-play.log`. Uma compilação bem-sucedida não substitui o teste manual de compra, abastecimento, pagamento e pickup.

Execute SetupValidation com gráficos ativos, sem `-nographics`: a Main usa URP e o caixa precisa de Camera.main. O relatório da suíte de regras está em `Logs/setup-editmode.xml`. O teste de save feito em Play foi de captura/restauração em memória; o arquivo da partida não foi regravado pelos testes corrigidos.

## Pendências conhecidas

Ações de tablet/engradado/taco, arte final das interfaces, migração integral das prateleiras/NPCs, colocação e save de móveis continuam pendentes. A porta, as cinco abas, a barra de ferramentas e o Price Display estão automatizados e testados; ainda faça uma passada manual completa na Main. Os detalhes estão em [handoff.md](handoff.md).
