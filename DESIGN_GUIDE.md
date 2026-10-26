# WalletFlow — Guia de identidade visual

## Direção visual

Uma interface financeira clara, confiável e discreta. O azul comunica segurança e orienta as ações principais; verde e vermelho aparecem apenas como sinais semânticos de receita e despesa. Superfícies claras, bordas suaves e espaço generoso mantêm o extrato fácil de ler.

## Estrutura de apresentação

- Estilos globais e tokens: `WalletFlow.Blazor/WalletFlow.Blazor/wwwroot/theme.css`.
- Base do template e estilos de compatibilidade: `WalletFlow.Blazor/WalletFlow.Blazor/wwwroot/app.css`.
- Documento HTML e ordem das folhas de estilo: `WalletFlow.Blazor/WalletFlow.Blazor/Components/App.razor`.
- Layout e navegação: `Components/Layout/MainLayout.razor` e `MainLayout.razor.css`; `NavMenu.razor` e `NavMenu.razor.css`.
- Páginas: `Components/Pages/Home.razor`, `Dashboard.razor`, `Transacoes.razor` e `NovaTransacao.razor`; os estilos particulares ficam nos arquivos correspondentes `.razor.css`.
- Login: `Components/Pages/Login.razor` (`/login`) e `Login.razor.css`, com cartão central, campos validados, foco visível e layout responsivo.
- O projeto não usa uma pasta `Shared`; os componentes reutilizáveis ficam em `Components/Layout`.

## Paleta

| Uso | Cor | Token CSS |
| --- | --- | --- |
| Primária | Azul `#2563EB` | `--wf-primary` |
| Primária escura | Azul `#1D4ED8` | `--wf-primary-hover` |
| Primária suave | Azul muito claro `#EFF6FF` | `--wf-primary-soft` |
| Secundária | Ardósia `#475569` | `--wf-secondary` |
| Sucesso | Verde `#15803D` | `--wf-success` |
| Fundo de sucesso | Verde muito claro `#ECFDF3` | `--wf-success-soft` |
| Alerta | Âmbar `#B45309` | `--wf-warning` |
| Fundo de alerta | Âmbar muito claro `#FFFBEB` | `--wf-warning-soft` |
| Texto principal | Azul ardósia `#172033` | `--wf-ink` |
| Texto secundário | Cinza `#667085` | `--wf-muted` |
| Borda | Cinza claro `#E4E7EC` | `--wf-line` |
| Superfície | Branco `#FFFFFF` | `--wf-surface` |
| Fundo da aplicação | Cinza azulado `#F5F7FB` | `--wf-canvas` |
| Despesa / erro | Vermelho `#B42318` | `--wf-danger` |

Use a primária para ações principais, links e foco. Verde indica receita ou sucesso; vermelho indica despesa ou erro. Âmbar deve sinalizar atenção. Garanta contraste suficiente e não comunique estado apenas pela cor.

## Tipografia

Use `Inter` quando instalada localmente; a pilha de fallback atual é `Segoe UI`, `Roboto`, `Helvetica Neue` e `Arial`. Evite depender de uma fonte remota para que a tela continue rápida e previsível.

| Elemento | Tamanho sugerido | Peso | Uso |
| --- | --- | --- | --- |
| H1 | `clamp(1.8rem, 3vw, 2.25rem)` | 700–750 | Título da página |
| H2 | `1.25rem` | 700 | Cabeçalho de seção ou cartão |
| H3 | `1.05rem` | 650–700 | Sub-seção |
| Corpo | `0.94rem`–`1rem` | 400 | Texto e formulários |
| Auxiliar | `0.82rem`–`0.88rem` | 400–600 | Rótulos, datas e contagens |
| Eyebrow | `0.7rem` | 700–750 | Identificador curto em caixa alta |

Use altura de linha entre `1.45` e `1.6` para texto corrido. Para valores monetários e contagens, use algarismos tabulares (`font-variant-numeric: tabular-nums`).

## Espaçamento e formas

Baseie os intervalos em múltiplos de `4px` (`0.25rem`).

| Token / uso | Valor | Aplicação |
| --- | --- | --- |
| Compacto | `4px` / `0.25rem` | Ícone e texto próximos |
| Pequeno | `8px` / `0.5rem` | Espaço interno de controles |
| Médio | `12px` / `0.75rem` | Grupos de campos e ações |
| Padrão | `16px` / `1rem` | Padding de painel compacto |
| Amplo | `24px` / `1.5rem` | Painéis, seções e respiro |
| Seção | `32px` / `2rem` | Separação entre áreas principais |

- Botões: altura mínima aproximada de `42px`, padding horizontal de `14–16px`, raio `10px` (`--wf-radius-sm`).
- Inputs: altura mínima `44px`, raio `10px`, foco visível azul e rótulo persistente.
- Cards e painéis: padding `16–24px`, raio `14px` (`--wf-radius-md`) e sombra leve.
- Blocos de destaque: raio `20px` (`--wf-radius-lg`).
- Pills de receita/despesa: formato totalmente arredondado, fundo suave e texto com contraste.
- Bordas: `1px solid var(--wf-line)`; reserve sombras mais fortes para o cartão de saldo ou superfícies de destaque.

## Responsividade e interação

- Menu lateral fixo em telas amplas e recolhível em telas estreitas.
- Grades de resumo passam a uma coluna em telas menores; filtros quebram em duas colunas e depois uma.
- Tabelas preservam rolagem horizontal em dispositivos estreitos.
- Estados de foco devem ser visíveis por teclado; respeite `prefers-reduced-motion`.
- Use mensagens de estado, erro e vazio com texto e semântica acessíveis.
- A tela de login usa os mesmos tokens e tons de ação do produto; os destinos `/esqueci-a-senha` e `/criar-conta` são pontos de integração para páginas futuras.

## Biblioteca de componentes

O projeto já inclui Bootstrap e tem poucas telas e controles simples. Não recomendo instalar MudBlazor ou Fluent UI nesta etapa: uma segunda biblioteca aumentaria o bundle, a configuração e a mistura de convenções visuais sem resolver uma necessidade atual. Os componentes nativos do Blazor, estilizados com Bootstrap e CSS isolado, cobrem os formulários, botões, navegação e tabelas existentes. Reavalie se o produto passar a precisar de grades de dados avançadas, componentes complexos ou um conjunto amplo de padrões interativos.
