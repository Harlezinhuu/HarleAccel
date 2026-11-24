# Segurança

## O que o HarleAccel faz (e o que não faz)

- **Roda sem administrador.** Só o instalador **oficial** do Raw Accel pede admin, e só quando você clica em "Instalar".
- **Não mexe no kernel.** Quem acelera o mouse é o driver oficial e assinado do Raw Accel.
- **Confere cada arquivo do Raw Accel antes de usar.** São 5 arquivos, checados pelo SHA-256: o zip do release, o `installer.exe`, o `writer.exe`, o `wrapper.dll`, o `Newtonsoft.Json.dll` e o `driver\rawaccel.sys`. Se algum não bater, o app não executa nada.
- **Só LÊ o mouse** (Raw Input) durante o Teste de Perfil e no diagnóstico. Não lê o teclado, não lê a tela e não injeta nada em jogos.
- **Os dados ficam no seu PC** (`%AppData%\HarleAccel`). Só três coisas saem da sua máquina:
  - a checagem de versão nova (GitHub);
  - o download das rotinas de treino (GitHub);
  - o chat do Harle IA, **e só se você colocar a sua chave do Gemini**. Nesse caso vão a sua mensagem e um resumo do setup.
- **A chave do Gemini fica criptografada** com o DPAPI do Windows. Só o seu usuário consegue abrir, e ela só é enviada para o Google.
- **A IA não aplica nada sozinha.** Mudanças na curva só acontecem quando você clica em **Aplicar**, e todos os valores passam pelos limites do app.
- **Links externos só abrem se forem https de sites conhecidos.** Isso vale para o GitHub, o Discord, o Google AI Studio, o Aim Lab, o KovaaK's e o Voltaic.

## Reportar um problema

Achou uma falha de segurança? **Não abra uma issue pública.** Mande mensagem no Discord da comunidade: https://discord.gg/rDb9raznQ7
