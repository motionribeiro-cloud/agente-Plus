# Expressões: bounce, overshoot e easing

Identifique a propriedade e sua dimensão: número, vetor 2D/3D, cor, texto ou outro tipo. Uma expressão de Position não deve ser aplicada a texto ou Shape Path sem adaptação.

- Easing controla a progressão temporal. Ajuste velocidade e influência ao percurso e ao gesto.
- Overshoot passa do alvo e volta; reserve espaço e tempo para resolver o movimento.
- Bounce visual sugere rebote, mas não significa uma simulação física.
- Keyframes bem construídos podem ser a implementação mais editável para movimentos simples.

## Exemplos complementares

- `expressoes/ease-opacity.jsx`: entrada de Opacity por tempo, sem keyframes.
- `expressoes/overshoot-scalar.jsx`: oscilação amortecida após o último keyframe de uma propriedade numérica.
- `expressoes/bounce-position.jsx`: deslocamento vertical procedural em Position 2D/3D, iniciado no inPoint.

São pontos de partida próprios. Sintaxe e amostras numéricas são verificadas em simulação JavaScript; ainda não há validação no motor nativo do After Effects. Leia o arquivo antes de propor sua aplicação. Informe a propriedade, os parâmetros, os limites e como remover a expressão.

Não execute código simplesmente porque foi encontrado na biblioteca. Inspecione o alvo, preserve o valor anterior e confira amostras antes/depois quando o usuário autorizar a aplicação.

API: [documentação oficial Adobe](https://helpx.adobe.com/after-effects/desktop/work-with-expressions/expression-language-reference/expression-language-reference.html).
