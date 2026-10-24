# WalletFlow.WebApi

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4?logo=dotnet&logoColor=white)

> API REST para registrar movimentações financeiras e consultar o saldo consolidado de uma carteira.

## Sobre o projeto

O WalletFlow.WebApi é uma API de exemplo para controle de receitas e despesas. As movimentações são persistidas em SQLite e podem ser consultadas individualmente, listadas em conjunto ou usadas para calcular o saldo.

## Funcionalidades

- Cadastro de transações como despesa ou receita.
- Consulta de todas as transações ou de uma transação pelo ID.
- Cálculo do saldo (receitas menos despesas).
- Documentação interativa com Swagger em ambiente de desenvolvimento.
- Criação do banco SQLite e das tabelas na primeira inicialização.

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

O perfil HTTP de desenvolvimento usa `http://localhost:5250` e abre o Swagger no navegador. O perfil HTTPS usa `https://localhost:7013` (e também disponibiliza HTTP na porta 5250). Para escolher o perfil explicitamente:

```bash
dotnet run --project WalletFlow/WalletFlow.csproj --launch-profile http
```

O banco `Wallet.db` é criado na pasta de execução da aplicação na primeira inicialização.

## Como usar

Com a API em execução, acesse o Swagger em [http://localhost:5250/swagger](http://localhost:5250/swagger) para explorar e executar as rotas.

### Rotas

| Método | Rota | Descrição |
| --- | --- | --- |
| `POST` | `/api/transacao` | Cadastra uma transação |
| `GET` | `/api/transacao` | Lista as transações |
| `GET` | `/api/transacao/{id}` | Consulta uma transação pelo ID |
| `GET` | `/api/transacao/saldo` | Retorna o saldo consolidado |

Exemplo de cadastro (tipo `0` para despesa e `1` para receita):

```bash
curl -X POST http://localhost:5250/api/transacao \
  -H "Content-Type: application/json" \
  -d '{"descricao":"Salário","valor":3500.00,"tipo":1}'
```

O cadastro retorna `201 Created` com a transação criada, incluindo o ID e a data de cadastro em UTC. A resposta de saldo tem o formato `{"saldoConsolidado":3500.00}`.

Exemplo de consulta do saldo:

```bash
curl http://localhost:5250/api/transacao/saldo
```

## Como contribuir

1. Crie uma branch para sua alteração.
2. Faça uma mudança pequena e focada, usando commits atômicos com mensagens no padrão Conventional Commits (por exemplo, `feat: adiciona filtro por período`).
3. Abra um Pull Request explicando a alteração e como ela foi verificada.

## Licença

Este repositório ainda não define uma licença. Até que uma licença seja adicionada, todos os direitos permanecem reservados aos titulares do projeto.
