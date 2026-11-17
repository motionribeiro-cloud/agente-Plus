# Soma Agente 1.9.0 preview

Instalador por usuário, MCP por aplicativo e conexão com pastas de conhecimento. Adobe e Hub partem da base 1.7.11; Blender/Cinema 4D partem do EXE 1.4.1, com correções pontuais. O instalador recusa downgrade quando detecta uma versão mais recente.

## Conectar instruções

O instalador tem duas entradas opcionais: **Complemento público** e **Brain / pasta privada**. Escolha as pastas extraídas, não os ZIPs. Os arquivos permanecem no local escolhido, em modo de leitura.

No painel: engrenagem → **Conhecimento** → **Escolher pasta** → **Conectar pasta**. É possível conectar várias bibliotecas e desconectar sem apagar arquivos. No macOS, cole o caminho; o seletor nativo desta preview foi implementado para Windows.

Antes de cada pedido, o agente recupera trechos pertinentes. Os MCPs expõem `soma_knowledge_status`, `soma_search_knowledge` e `soma_read_knowledge` para a mesma consulta nos chats. Arquivos complementares, inclusive JSX, são entregues como texto; a leitura não executa código.

Os trechos recuperados são enviados ao provedor de IA escolhido como contexto. Conecte somente materiais que pretende usar dessa forma. Bibliotecas privadas não são publicadas ou exportadas automaticamente.

## Seleção e atualização

- Com `soma-knowledge.json`, somente os arquivos de `documents` são lidos. O manifesto pode definir título, tags, resumo, validação e `role: "core"` para uma orientação fundamental curta.
- Sem manifesto, são indexados MD, TXT, JSON, JS e JSX, excluindo arquivos ocultos, diretórios não autorizados e possíveis credenciais detectadas. Limites: 3000 documentos por origem e 256 KiB por arquivo.
- Um Brain com `memoria/` e `conhecimento/` recebe uma seleção técnica explícita; o vault inteiro não é exposto. O padrão está em `BRAIN_FILES` no motor. Outras pastas menores podem ser conectadas separadamente.
- Novos arquivos autorizados pelo manifesto e mudanças de conteúdo aparecem na próxima consulta. Não há embeddings nem envio da base inteira a um serviço externo.
- A configuração compartilhada fica em `AE Agent/knowledge.json`, na pasta de dados do usuário. Seus caminhos não são embutidos no instalador distribuído.

## Instalação e MCP

Feche os aplicativos que pretende atualizar. Confira componentes, destinos e bibliotecas no resumo. A instalação prepara pacotes, mantém backups e tenta reverter alterações se uma etapa falhar. A desinstalação move os destinos gerenciados para pastas `.removed`, preservando os arquivos.

O botão **Perfis MCP** abre os perfis dos componentes selecionados. Mescle-os às configurações de Claude Desktop/Code ou Codex e recarregue o cliente. Os servidores existentes devem ser preservados. O Hub usa loopback e credencial local; nenhum túnel público é aberto.

Há perfis para After Effects, Photoshop, Illustrator, Premiere Pro, Blender, Cinema 4D e Figma. Cada um controla seu aplicativo; o conhecimento é compartilhado. ChatGPT web exige conexão remota ou túnel autorizado separado.

## Claude

Login salvo não comprova uma chamada autenticada. A preview diferencia token expirado/revogado, rede e limites de uso. **Check again** faz uma pequena chamada real, sem ferramentas nem persistência de sessão, usando o plano. A descoberta da CLI prefere a versão mais recente, respeitando um caminho explícito do usuário.

## Compilar e validar

Execute `build.ps1` no Windows com .NET Framework, Python 3 e Node.js. O build reconstrói os ZIPs de `components/`, verifica o runtime e executa testes antes de compilar o EXE. Não exige NuGet nem downloads. A licença do Python embarcado é preservada.

O motor canônico é `components/hub/bridge/soma_knowledge.py`; o build o replica nos cores. Os testes das expressões usam o complemento em `complementares/Soma-Conhecimento-1.0.0`, no projeto ou na pasta irmã.

Consulte `VALIDACAO.md`. A biblioteca, o protocolo e o instalador foram testados com o Python embarcado real. A aba Conhecimento ainda precisa de teste dentro do CEP atualizado. As expressões não foram validadas no motor nativo AE. Nenhum projeto de produção foi fechado ou editado para validar a preview.

A reversão cobre exceções, não interrupção abrupta ou queda de energia. A configuração CEP é opcional e não é restaurada automaticamente na desinstalação, pois outros painéis podem depender dela. O EXE não tem assinatura digital; a licença do código Soma permanece a definir.
