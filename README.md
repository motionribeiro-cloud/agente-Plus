# Soma Agente

Agente para softwares criativos, MCP por aplicativo e bibliotecas de conhecimento conectadas. Versão atual: **1.9.0 preview**, destinada a testes.

## Instalador principal

[Baixar instalador Windows 1.9.0 preview](https://github.com/motionribeiro-cloud/agente-Plus/releases/download/v1.9.0-preview/Soma-Agente-Instalador-1.9.0-preview.exe)

Selecione os componentes e confira os destinos. O instalador mantém backups, oferece diagnóstico e permite escolher uma pasta de complemento e uma pasta privada de conhecimento.

Inclui Adobe CEP (After Effects, Photoshop, Illustrator e Premiere Pro), Figma/Hub, Blender e Cinema 4D. A presença de código não comprova compatibilidade funcional com toda versão desses aplicativos.

## Complementares

[Baixar Soma Conhecimento Essencial 1.0.0](https://github.com/motionribeiro-cloud/agente-Plus/releases/download/v1.9.0-preview/Soma-Conhecimento-Essencial-1.0.0.zip) · [Ver os materiais](complementares/Soma-Conhecimento-1.0.0/README.md)

Pacote inicial com oito guias e três expressões: direção, SaaS, explicativo, institucional, timing/curvas, After Effects, revisão, bounce, overshoot e easing. Os exemplos têm testes numéricos JavaScript; não há validação nativa AE. É uma seleção editorial própria, não uma exportação de notas privadas ou arquivos de cursos.

## Como conectar

1. Extraia o complemento em um local permanente.
2. Selecione a pasta no instalador ou abra **engrenagem → Conhecimento → Escolher pasta → Conectar pasta** no painel atualizado.
3. Escolha a pasta que contém `soma-knowledge.json`, não o ZIP.
4. Peça, por exemplo: “planeje uma animação SaaS” ou “busque uma expressão de bounce, sem aplicá-la”.

A mesma seção permite conectar outras bibliotecas. Os arquivos são consultados em modo de leitura e trechos pertinentes são enviados à IA escolhida como contexto. Os MCPs oferecem busca e leitura para os chats. Desconectar preserva os arquivos; mudanças autorizadas entram nas próximas consultas.

Um Brain local usa uma seleção técnica explícita. Os caminhos e documentos privados do estúdio não estão neste repositório nem no complemento. Bibliotecas privadas não são publicadas automaticamente.

## Código e validação

- [Código atual e compilação](principal/README.md)
- [Verificações e limites](principal/VALIDACAO.md)
- [Release: instalador, complemento e fontes](https://github.com/motionribeiro-cloud/agente-Plus/releases/tag/v1.9.0-preview)

`source/` preserva o código histórico da release 1.3.0. O desenvolvimento atual está em `principal/`. Releases anteriores foram mantidas.

A preview tem testes de biblioteca, MCP e instalação em pastas temporárias, incluindo o Python embarcado. O instalador foi conferido visualmente. A nova aba precisa de validação dentro do CEP atualizado; não há promessa de funcionamento universal dos hosts. O EXE não tem assinatura digital. A licença do código Soma permanece a definir; disponibilidade pública não equivale a licença open source. O Python embarcado mantém sua licença própria.
