# Otimizacao visual - pendencias manuais

Os ajustes automaticos e de baixo risco ja foram aplicados. Os itens abaixo precisam de avaliacao visual, conhecimento da movimentacao dos objetos ou ferramentas do Unity Editor.

## LOD e vegetacao

- Criar `LODGroup` para as arvores grandes, medias e pequenas.
- Usar os modelos `MediumLod.fbx`, `LowLod.fbx`, `MidLeafsLod.fbx` e `LowLeafsLod.fbx` ja existentes no projeto.
- Configurar uma versao sem sombras para o LOD mais distante.
- Conferir a transicao visual e, se necessario, criar billboard para arvores distantes.
- Revisar a grama e os vasos repetidos, compartilhando materiais e habilitando GPU Instancing nos materiais compativeis.

## Objetos estaticos e occlusion culling

- Marcar como estaticos somente muros, pisos, cercas, predios e decoracoes que nunca sao movidos ou substituidos durante as expansoes da loja.
- Nao marcar itens compraveis, caixas, produtos, portas, carrinhos ou objetos movimentados pela gameplay.
- Depois da classificacao, gerar os dados de Occlusion Culling no Unity Editor.

## Texturas e modelos

- Habilitar `Streaming Mip Maps` nos importadores das texturas grandes de terreno, vegetacao e cenario. A opcao global ja esta ativa.
- Limitar texturas secundarias a 1024 ou 2048, conforme a importancia visual.
- Conferir as texturas grandes do pacote `Toby Fredson/The Toby Foliage Engine`, removendo da pasta de producao demos e arquivos sem uso.
- Desabilitar `Read/Write` nos modelos e texturas somente depois de confirmar que nenhum sistema os acessa ou modifica em runtime.
- Aplicar Mesh Compression moderada por modelo e conferir deformacoes/sombras antes de continuar em lote.

## Particulas

- Consolidar os 35 sistemas `SakuraParticle` em menos emissores, preservando a distribuicao artistica.
- Reduzir `Max Particles`, taxa de emissao e tamanho do bounds conforme cada area.
- Desativar iluminacao e sombras das particulas onde nao forem perceptiveis.

## URP e pos-processamento

- Confirmar se `Opaque Texture` e usada pela agua, distorcao ou shaders transparentes antes de desativa-la no perfil PC.
- Confirmar se o fullscreen dithering e essencial ao estilo; ele faz uma copia adicional do color buffer.
- Comparar a nevoa volumetrica por area e desativa-la em interiores ou em um nivel de qualidade inferior.
- Revisar Bloom, Film Grain, Depth of Field, Motion Blur e lens flare nos Volumes realmente usados pela cena. Nao remover componentes do `DefaultVolumeProfile`: ele tambem armazena defaults globais do URP.

## Validacao futura

- Quando testes forem autorizados, validar os ajustes com Game View, Frame Debugger e Profiler em uma area interna e outra externa.
- Observar principalmente GPU frame time, batches, SetPass calls, shadow casters, overdraw e memoria de texturas.
