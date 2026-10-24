# WalletFlow.WebApi

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4?logo=dotnet&logoColor=white)

> Uma API REST em ASP.NET Core, preparada como ponto de partida para os serviços do WalletFlow.

## Sobre o projeto

O WalletFlow.WebApi é a aplicação web do projeto WalletFlow. Ela usa controllers do ASP.NET Core e disponibiliza a infraestrutura de OpenAPI no ambiente de desenvolvimento. Neste estágio, o projeto contém a estrutura inicial da API; ainda não há endpoints de negócio implementados.

## Funcionalidades

- Inicialização de uma aplicação ASP.NET Core Web API.
- Suporte a controllers.
- Documento OpenAPI disponível em desenvolvimento.
- Redirecionamento de requisições HTTP para HTTPS quando configurado.

## Tecnologias

- C#
- .NET 10
- ASP.NET Core
- OpenAPI (`Microsoft.AspNetCore.OpenApi`)

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git, para clonar o repositório

Confira a instalação do SDK com:

```bash

dotnet --version
```

## Instalação e execução

Clone o repositório e entre na pasta da solução:

```bash
git clone <URL_DO_REPOSITORIO>
cd WalletFlow.WebApi
```

Restaure as dependências e inicie a API:

```bash
dotnet restore WalletFlow/WalletFlow.csproj
dotnet run --project WalletFlow/WalletFlow.csproj
```

O perfil HTTP de desenvolvimento usa `http://localhost:5250`. A URL efetiva também é exibida no terminal ao iniciar a aplicação.

## Como usar

No estado atual, a aplicação ainda não possui rotas de negócio. Em desenvolvimento, o documento OpenAPI pode ser consultado em:

```text
http://localhost:5250/openapi/v1.json
```

Para testar a API com `curl` quando houver endpoints implementados, use este formato:

```bash
curl -i http://localhost:5250/<rota>
```

## Como contribuir

1. Crie uma branch para sua alteração.
2. Faça uma mudança pequena e focada, com commits atômicos usando mensagens no padrão Conventional Commits, por exemplo `feat: adiciona endpoint de carteiras` ou `docs: atualiza instruções de execução`.
3. Abra um Pull Request descrevendo o objetivo e como a alteração foi verificada.

## Licença

Este repositório não contém um arquivo de licença no momento. Até que uma licença seja definida e incluída, todos os direitos permanecem reservados aos titulares do projeto.

