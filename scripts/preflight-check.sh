#!/bin/sh
set -eu

ENV_FILE="${1:-.env}"

if [ ! -f "$ENV_FILE" ]; then
    echo "[preflight] ERRO: arquivo $ENV_FILE nao encontrado. Copie .env.example para .env e preencha os valores reais." >&2
    exit 1
fi

if grep -Eq "substitua-por|exemplo\.com|seu-email@|sua-senha" "$ENV_FILE"; then
    echo "[preflight] ERRO: o arquivo $ENV_FILE ainda contem valores ficticios de exemplo." >&2
    exit 1
fi

docker compose --env-file "$ENV_FILE" config -q
echo "[preflight] Configuracao do Docker Compose e variaveis de producao validadas com sucesso."
