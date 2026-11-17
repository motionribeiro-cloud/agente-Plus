# Validação da preview 1.9.0

- 18 testes Python do adaptador MCP, autenticação e descoberta da CLI.
- 15 casos Python da biblioteca: busca, complementos, atualização, manifesto, seleção do Brain, contexto, origem/hash, desconexão e preservação. O caso que exige criar symlink foi marcado como não disponível no Windows de teste.
- Oito verificações JavaScript de autenticação e escolha de binário.
- Testes numéricos das três expressões, com estados anteriores à entrada, keyframes ausentes e preservação de eixos 3D. Sem execução nativa AE.
- 12 cenários C# de instalação, reversão, integridade, perfis e conexão de pasta pelo Python embarcado 3.12.10.
- O teste C# inicia o adaptador MCP real no Python embarcado isolado e consulta a biblioteca de teste.
- Janela do instalador renderizada e inspecionada, com os dois seletores de pastas.
- Sintaxe dos componentes Python/PYP e JavaScript conferida.
- Teste local no MCP do After Effects encontrou o guia SaaS público e referências privadas selecionadas, sem editar o projeto.

## Validação nativa pendente

A aba Conhecimento precisa ser aberta no CEP atualizado para conferir escolher, conectar e consultar uma pasta. Edição em Photoshop, Illustrator, Premiere, Blender, Cinema 4D e Figma não foi validada nesta rodada. ChatGPT web não foi configurado.

Importação de arquivos, presença de ferramentas, resposta MCP e resultado visual são etapas diferentes e precisam de verificação própria.
