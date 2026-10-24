# WalletFlow.WebApi

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4?logo=dotnet&logoColor=white)

> API REST para registrar receitas e despesas, consultar movimentações e calcular o saldo consolidado de uma carteira.

## Sobre o projeto

O WalletFlow.WebApi é uma API de exemplo para controle de movimentações financeiras. Os registros são persistidos em SQLite, e o Entity Framework Core aplica as migrações durante a inicialização.

## Funcionalidades

- Cadastro de despesas e receitas.
- Consulta de uma movimentação pelo ID.
- Listagem paginada, com até 100 itens por página.
- Cálculo do saldo consolidado.
- Autenticação por chave de API fora do ambiente de desenvolvimento.
- Documentação interativa com Swagger em desenvolvimento.

## Tecnologias

- C# e .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQLite
- Swashbuckle / Swagger

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

Confira a instalação do SDK:

```bash
dotnet --version
```

## Instalação e execução

```bash
git clone https://github.com/leonardo-castriola/WalletFlow.WebApi.git
cd WalletFlow.WebApi
dotnet restore WalletFlow/WalletFlow.csproj
dotnet run --project WalletFlow/WalletFlow.csproj
```

No perfil local de desenvolvimento, a aplicação usa `http://localhost:5250` e abre o Swagger no navegador. A interface está em `/swagger` e só fica disponível em desenvolvimento. O perfil HTTPS usa `https://localhost:7013`.

O banco SQLite `Wallet.db` fica na pasta `WalletFlow/` e é criado ou atualizado pelas migrações do Entity Framework na inicialização.

## Autenticação

Em desenvolvimento, as rotas podem ser usadas sem chave. Fora de `Development`, configure uma chave forte pela variável de ambiente `Authentication__ApiKey`. Não inclua a chave no repositório.

Exemplo para iniciar em produção no Bash:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export Authentication__ApiKey='substitua-por-uma-chave-forte'
dotnet run --project WalletFlow/WalletFlow.csproj --no-launch-profile
```

Envie a chave no cabeçalho `X-API-Key` em cada requisição. Sem uma chave configurada, a aplicação não inicia fora de desenvolvimento.

## Como usar

Com a API em execução localmente, abra [http://localhost:5250/swagger](http://localhost:5250/swagger) para explorar as rotas.

| Método | Rota | Descrição |
| --- | --- | --- |
| `POST` | `/api/transacao` | Cadastra uma movimentação |
| `GET` | `/api/transacao?page=1&pageSize=50` | Lista movimentações paginadas |
| `GET` | `/api/transacao/{id}` | Consulta uma movimentação pelo ID |
| `GET` | `/api/transacao/saldo` | Retorna o saldo consolidado |

No cadastro, use `tipo: 0` para despesa e `tipo: 1` para receita:

```bash
curl -X POST http://localhost:5250/api/transacao \
  -H "Content-Type: application/json" \
  -d '{"descricao":"Salário","valor":3500.00,"tipo":1}'
```

A listagem aceita `page` (padrão `1`) e `pageSize` (padrão `50`, máximo `100`). A resposta inclui `items`, `page`, `pageSize`, `totalCount` e `totalPages`.

Em produção, acrescente a chave à requisição:

```bash
curl http://localhost:5250/api/transacao/saldo \
  -H "X-API-Key: sua-chave-forte"
```

## Como contribuir

1. Crie uma branch para sua alteração.
2. Faça uma mudança pequena e focada, usando commits atômicos com mensagens no padrão Conventional Commits, por exemplo `feat: adiciona filtro por período`.
3. Abra um Pull Request explicando a alteração e como ela foi verificada.

## Licença

Este repositório ainda não define uma licença. Até que uma licença seja adicionada, todos os direitos permanecem reservados aos titulares do projeto.
