<p align="center"><img src="src/HarleAccel/Assets/logo.png" width="120" alt="HarleAccel"></p>

# HarleAccel

[![Build](https://github.com/Harlezinhuu/HarleAccel/actions/workflows/build.yml/badge.svg)](https://github.com/Harlezinhuu/HarleAccel/actions/workflows/build.yml)

Aceleração de mouse para Windows, feita para quem joga. **Criado por Harle.**

<!-- Prints: coloque as imagens em docs/screenshots/ e descomente as linhas abaixo
![Curva](docs/screenshots/curva.png)
![Teste de Perfil](docs/screenshots/teste-de-perfil.png)
![Harle IA](docs/screenshots/harle-ia.png)
-->

## Baixar

Pegue o `HarleAccel.exe` na página de [Releases](https://github.com/Harlezinhuu/HarleAccel/releases). É um arquivo só, e não precisa instalar o .NET.

**Aviso do Windows (SmartScreen):** como o .exe não tem assinatura digital paga, o Windows pode mostrar "O Windows protegeu o computador". Isso é normal em projetos independentes. Clique em **Mais informações → Executar assim mesmo**. O código está todo aqui para quem quiser conferir, e o app não pede administrador (só o instalador oficial do Raw Accel pede).

O HarleAccel usa o **driver oficial e assinado do [Raw Accel](https://github.com/RawAccelOfficial/rawaccel)**, o mesmo que funciona com Vanguard e FACEIT. Em cima dele, traz uma interface nova e ferramentas que o Raw Accel não tem: um teste que lê como você joga e recomenda a curva, um teste cego A/B, diagnóstico do mouse e treino de adaptação.

## Funcionalidades (v0.4)

**Harle IA**
- **Chat dentro do app.** Ele entende pedidos como "8% fortnite 800 dpi pro valorant" **sem internet e sem chave**, e mostra o resultado num cartão com botão de copiar.
- **Gemini opcional.** Com a chave grátis do Gemini (que cada pessoa cola em Configurações, criptografada com DPAPI), o chat entende perguntas livres. A IA só *entende* o pedido: as contas são feitas pelo conversor do app (function calling).
- **Permissão por botão.** Quando a IA sugere mudar a curva, aparece um cartão com o antes e depois no gráfico e os botões **Aplicar**/**Recusar**. Depois de aplicar, dá para desfazer. Nada é aplicado sem clique.


**Configurar**
- **Assistente de primeiro uso.** Baixa o Raw Accel oficial, confere o SHA-256, instala o driver, checa o Windows e aplica a primeira curva.
- **Calculadora e conversor de sensibilidade.** Mostra cm/360, eDPI e a faixa da sua sens. Converte entre 9 jogos e calcula a sens para um cm/360 alvo.
- **Base intocada.** Em movimento lento a curva fica em 1x, então a sens do jogo continua a mesma.
- **Editor de curva.** Estilos Linear, Natural e Personalizada. Tem curva explicada ao passar o mouse, estilos prontos (Flicker, Tracker, Híbrido), splitter X/Y, rotação e angle snapping do driver em "Avançado".
- **Histórico de curvas** e **importar/exportar** o `settings.json` do Raw Accel.

**Descobrir e treinar**
- **Teste de Perfil.** Você joga 2 ou 3 partidas e o app só *lê* o mouse por Raw Input, sem tocar no jogo.
  - No fim ele mostra o seu estilo (Flicker, Tracker ou Híbrido) e o mapa de onde a sua mão passa o tempo.
  - Recomenda uma curva já com o split X/Y e a rotação.
  - Só conta o tempo com o jogo em foco e o cursor escondido. A curva fica desligada durante o teste (opcional) e volta sozinha.
- **Teste cego A/B.** São 4 rodadas de 30 s alternando duas curvas sem dizer qual é qual. Só depois que você escolhe o app revela qual era cada uma e mostra os números.
- **Antes e depois.** Aim test de 45 s, com o histórico agrupado por curva.
- **Diagnóstico do mouse.** Medidor de DPI real (com régua) e detector de angle snapping do sensor.
- **Treino de adaptação.** Rotinas de 36 minutos por estilo e nível, no **Aim Lab (grátis)** ou no KovaaK's.
  - Os cenários vêm das rotinas públicas do Voltaic.
  - Tem cronômetro por cima do jogo e registro de score com a evolução.

**App**
- **Aviso de nova versão**, pelo GitHub Releases deste repositório.
- **Discord Rich Presence** (precisa de um Application ID, veja em Configurações).
- **Rotinas atualizáveis.** O app baixa `routines/routines.json` deste repositório ao abrir, então dá para atualizar as playlists sem lançar versão nova.

## Como o driver é usado

1. O app baixa `RawAccel_v1.7.1.zip` do release oficial e confere o hash
   `770fe3ae0919ca3c4d412f58c985eb27f5434decad809f7e8206de4e8852eec4`. Se não bater, o arquivo é apagado e nada é instalado.
2. O `installer.exe` oficial roda como administrador.
3. Para aplicar uma curva, o app gera um `settings.json` no formato do Raw Accel e chama o `writer.exe` oficial.

O HarleAccel **nunca** mexe no kernel por conta própria, nem lê ou escreve na memória de jogos.

## Compilar

Precisa do .NET 8 SDK.

```
dotnet publish src/HarleAccel/HarleAccel.csproj -c Release -o out
```

Sai um `HarleAccel.exe` único, que roda sem instalar o .NET. O GitHub Actions (`.github/workflows/build.yml`) faz isso sozinho e cria um release quando você manda uma tag, por exemplo `v0.3.0`.

**Para o aviso de versão funcionar:** o repositório precisa se chamar `Harlezinhuu/HarleAccel`, e as releases precisam de tags no formato `vX.Y.Z`.


## Testes

Os testes cobrem a parte que não depende de tela:
- **Matemática das curvas.** São 70 valores comparados com o C++ original do Raw Accel (`tests/reference/ref.cpp`).
- **Conversão de sensibilidade.**
- **Leitura e escrita do `settings.json`.**
- **Chat sem IA:** entender frases como "8% fortnite 800 dpi pro valorant".
- **Teste de Perfil**, com partidas simuladas.
- **Biblioteca de treinos.**

```
dotnet test tests/HarleAccel.Tests/HarleAccel.Tests.csproj
```

Eles rodam no GitHub Actions a cada push, antes de gerar o .exe.

## Segurança

Veja o `SECURITY.md`: o que o app faz e o que não faz, quais dados saem do PC (quase nada) e como reportar um problema.

## Licença

© 2026 Harle. Você **pode** estudar, modificar e redistribuir o HarleAccel, inclusive versões melhoradas, **desde que** mantenha os créditos a Harle, deixe claro que a sua versão é baseada no HarleAccel (com link para este repositório) e avise que modificou. Você **não pode** se passar por criador original, esconder os créditos nem usar o nome "HarleAccel"/"Harle" como se a sua versão fosse a oficial. Detalhes no `LICENSE`.

Partes de terceiros, como o driver do Raw Accel (MIT), estão em `THIRD_PARTY_NOTICES.md`.
