# Manual do sistema de grid procedural (explicado pra um macaco)

Esquece "Transform placeholder", esquece arrastar 40 objetos pro Inspector.
Agora você só preenche **4 números por eixo** e o código faz o trabalho sujo.
Se um macaco souber contar até 3 e usar o mouse, ele configura um item novo.

---

## 1. A ideia em uma frase de macaco

Item = banana. Caixa e prateleira = caixote com buraquinhos organizados em fileiras.
Você não precisa mais cavar cada buraquinho na mão — você só diz **"quero 3 fileiras,
2 andares, 3 filas de profundidade, e cada banana fica a 12cm de distância uma da
outra"**. O jogo cava os buraquinhos sozinho, na hora, pra você.

Isso é configurado no `ScriptableObject` de cada item (`AllIThingsData`, o mesmo
asset que já tinha nome/preço/prefab). Agora ele tem duas caixinhas novas de
configuração: `boxGrid` (grid dentro da caixa) e `shelfGrid` (grid dentro da
prateleira).

---

## 2. Onde eu mexo? (o único lugar que interessa pro dia a dia)

Abre o asset `AllIThingsData` do item (aquele `.asset` que já existia, com
`itemName`, `itemPrefab`, etc.) e agora tem duas seções novas no Inspector:

```
Grid procedural - Caixa
    Offset e Spacing
        Origin Offset      (Vector3)
        Spacing X           (float)
        Spacing Y           (float)
        Spacing Z           (float)
    Quantidade no grid (Largura x Altura x Profundidade)
        Count Width         (int)
        Count Height        (int)
        Count Depth         (int)
    Transform do item dentro do slot
        Item Scale          (Vector3)
        Item Rotation Euler (Vector3)

Grid procedural - Prateleira
    (os mesmos campos de novo, só que pra prateleira)
```

É só isso. Não existe mais nada pra arrastar de Transform pra Transform.

### O que cada campo faz, em linguagem de macaco

| Campo | O que é | Analogia |
|---|---|---|
| `Count Width` (X) | quantos itens numa fileira, de lado a lado | quantas bananas cabem numa prateleira de lado a lado |
| `Count Height` (Y) | quantos andares empilhados | quantos andares de bananas empilhados |
| `Count Depth` (Z) | quantos itens de fundo (da frente pra trás) | quantas bananas cabem "enfiando" pra dentro |
| `Spacing X/Y/Z` | distância entre um item e o próximo, em cada eixo | o tamanho do espaço vazio entre uma banana e outra |
| `Origin Offset` | onde fica a primeira banana (canto 0,0,0 do grid), relativo ao objeto pai | onde você planta a primeira banana antes de começar a enfileirar as outras |
| `Item Scale` | tamanho do item quando ele é colocado no slot | se a banana encolhe/cresce pra caber |
| `Item Rotation Euler` | rotação (em graus) do item quando ele é colocado no slot | de que jeito a banana fica deitada |

`X = largura, Y = altura, Z = profundidade`. Sempre. Não inventa outro eixo.

---

## 3. Como o código enche o grid (importante saber, mas você não escreve isso)

Enche **andar por andar**: primeiro enche toda a base (largura x profundidade)
do andar 0, só depois sobe pro andar 1. Tipo empilhar caixas de banana no chão
todo antes de começar a segunda camada.

Se você usa isso pra saber a ORDEM em que os itens vão sumir/aparecer (por
exemplo: o último item colocado é o primeiro que o jogador tira), lembra
disso: enche de baixo pra cima, e dentro de cada andar, linha por linha em Z,
e dentro de cada linha, item por item em X.

---

## 4. Passo a passo pra criar um item novo (o fluxo real, do zero)

1. Cria o `AllIThingsData` do item, normal, do jeito que já fazia
   (`itemPrefab`, `itemBoxPrefab`, preço, sprite, etc.).
2. Preenche `boxGrid`: quantos itens cabem dentro da caixa desse item, e o
   espaçamento entre eles (baseado no tamanho físico do `itemPrefab`).
3. Preenche `shelfGrid`: igual, mas pra quantos cabem no segmento de
   prateleira.
4. Entra em Play, compra uma caixa desse item, olha se os itens aparecem
   dentro da caixa sem grudar/atravessar uns nos outros.
5. Se tiver sobreposição: aumenta o `Spacing` do eixo que tá grudando.
6. Se sobrar espaço vazio enorme entre os itens: diminui o `Spacing`.
7. Se os itens aparecem flutuando fora da caixa ou enterrados dentro da mesa:
   ajusta o `Origin Offset` (empurra o grid inteiro pro lugar certo).
8. Repete pra `shelfGrid` no `Segment` correspondente.

**Você não cria mais nenhum objeto filho manualmente dentro do prefab da
caixa nem da prateleira.** Isso acabou. Se ainda tiver itens filhos
manuais nesses prefabs antigos, pode apagar — eles não são mais lidos por
nada.

---

## 5. Configuração de cena (uma vez só por prefab, não por item)

### `Segment` (prateleira)
- Cada `Segment` tem uma lista `groups` no Inspector. Cada entrada agora só
  tem **um campo pra preencher**: `type` (o `Items` que aquele grupo aceita).
  Não tem mais array de Transform pra arrastar.
- Tem um campo novo `itemsParent`: é o Transform que serve de "origem" do
  grid (o offset e o spacing são relativos a ele). Se deixar vazio, ele usa o
  próprio Transform do segmento — funciona, mas se quiser mais controle
  (ex: girar só os itens sem girar o segmento inteiro), cria um filho vazio e
  arrasta ali.

### `ItemBox` (caixa)
- Também tem `itemsParent`: mesma ideia, é onde os itens da caixa nascem e
  ficam pendurados.
- Não precisa mexer em mais nada aqui — o `Populate()` cuida de instanciar os
  itens sozinho quando a caixa é comprada.

---

## 6. O que NÃO existe mais (não procura, não vai achar)

- `Transform[] allItems` — morreu.
- Arrastar 12 Transforms vazios pro array de um `SegmentTypeGroup` — morreu.
- Override de prefab de caixa só pra adicionar item filho posicionado à mão —
  morreu.
- `SegmentTypeGroup[] groups` dentro do `ItemBox` — morreu (a caixa só guarda
  um tipo de item por vez, então virou uma lista simples de slots).

Se algum script antigo (ou uma cena velha) reclamar de referência quebrada
pra essas coisas, é sinal de que ainda tem prefab do sistema antigo pairando
por aí — pode limpar.

---

## 7. Exemplo prático (números pra copiar e testar)

Pra um item pequeno tipo lata/pacote:

**boxGrid**
```
Origin Offset:  (0, 0, 0)
Spacing X/Y/Z:  (0.12, 0.12, 0.12)
Count W/H/D:    (3, 2, 2)   -> 12 itens na caixa
Item Scale:     (1, 1, 1)
Item Rotation:  (0, 0, 0)
```

**shelfGrid**
```
Origin Offset:  (0, 0.05, 0)
Spacing X/Y/Z:  (0.15, 0.15, 0.15)
Count W/H/D:    (4, 1, 2)   -> 8 itens na prateleira, 1 andar só
Item Scale:     (1, 1, 1)
Item Rotation:  (0, 0, 0)
```

Testa em cena, ajusta o spacing olhando o bounding box real do prefab do
item, e pronto — item novo funcionando sem posicionar um Transform sequer.

---

## 8. Resumo pra quem só quer o TL;DR de macaco

1. Cria/edita o `AllIThingsData`.
2. Preenche `boxGrid` e `shelfGrid` com número de itens por eixo e espaço
   entre eles.
3. Testa em Play.
4. Ajusta spacing/offset até parar de grudar ou flutuar.
5. Não arrasta Transform nenhum. Nunca mais.
