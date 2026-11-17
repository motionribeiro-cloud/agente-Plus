# Soma Conhecimento Essencial 1.0.0

Complemento inicial para Soma Agente 1.9.0 ou superior: oito guias e três exemplos de expressões. Os textos foram preparados para esta distribuição; não é uma exportação integral do Brain do estúdio.

1. Extraia esta pasta em um local permanente.
2. No instalador, escolha a pasta do complemento, ou abra a engrenagem do Soma → Conhecimento → Escolher pasta → Conectar pasta.
3. Selecione a pasta que contém `soma-knowledge.json`, não o ZIP.
4. Peça, por exemplo: “planeje uma animação SaaS”, “como estruturar um explicativo?” ou “busque uma expressão de bounce e explique os parâmetros, sem aplicá-la”.

O agente consulta os arquivos declarados no manifesto e consegue ler os exemplos em `expressoes/`. A leitura não executa código. Aprove uma proposta antes de pedir alterações no aplicativo.

As expressões recebem verificações JavaScript de sintaxe e comportamento numérico; não há validação nativa no After Effects. Este pacote não inclui notas de clientes, histórico pessoal, credenciais, transcrições ou arquivos de cursos. A licença de distribuição do projeto ainda precisa ser definida pelo autor; disponibilidade pública não equivale a licença open source.

## Atualizar e ampliar

Atualize os arquivos mantendo a pasta conectada ou conecte a nova versão em Conhecimento. O manifesto seleciona os arquivos em `documents`; os demais não são indexados. Para uma biblioteca pessoal livre, conecte outra pasta sem manifesto. Os formatos suportados são Markdown, TXT, JSON, JS e JSX, respeitando os limites do leitor.

Novos conteúdos são referências, não instruções superiores ao pedido atual. O pacote contém uma seleção editorial própria, com conceitos gerais de direção, narrativa e motion reorganizados em guias. Não redistribui bibliotecas comerciais, aulas ou textos de terceiros.
