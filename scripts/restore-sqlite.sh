#!/bin/sh
set -eu

if [ $# -lt 1 ]; then
    echo "Uso: $0 <caminho-do-arquivo-tar.gz.enc> [diretorio-de-dados]" >&2
    exit 1
fi

if [ -z "${BACKUP_ENCRYPTION_KEY:-}" ]; then
    echo "[restore] ERRO: defina BACKUP_ENCRYPTION_KEY para descriptografar o backup." >&2
    exit 1
fi

ARCHIVE_PATH="$1"
DATA_DIR="${2:-${DATA_DIR:-/data}}"
DB_PATH="$DATA_DIR/Wallet.db"
DPKEYS_DIR="$DATA_DIR/dpkeys"

if [ ! -f "$ARCHIVE_PATH" ]; then
    echo "[restore] ERRO: arquivo de backup nao encontrado: $ARCHIVE_PATH" >&2
    exit 1
fi

if [ -f "$ARCHIVE_PATH.sha256" ]; then
    ARCHIVE_DIR="$(dirname "$ARCHIVE_PATH")"
    ARCHIVE_FILE="$(basename "$ARCHIVE_PATH")"
    (cd "$ARCHIVE_DIR" && sha256sum -c "$ARCHIVE_FILE.sha256")
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 \
    -in "$ARCHIVE_PATH" \
    -out "$WORK_DIR/payload.tar.gz" \
    -pass env:BACKUP_ENCRYPTION_KEY

tar -C "$WORK_DIR" -xzf "$WORK_DIR/payload.tar.gz"

if [ ! -f "$WORK_DIR/Wallet.db" ]; then
    echo "[restore] ERRO: Wallet.db ausente no arquivo de backup." >&2
    exit 1
fi

INTEGRITY="$(sqlite3 "$WORK_DIR/Wallet.db" "PRAGMA integrity_check;")"
if [ "$INTEGRITY" != "ok" ]; then
    echo "[restore] ERRO: backup reprovado no PRAGMA integrity_check: $INTEGRITY" >&2
    exit 1
fi

mkdir -p "$DATA_DIR"
rm -f "$DB_PATH-wal" "$DB_PATH-shm"
cp "$WORK_DIR/Wallet.db" "$DB_PATH"

FINAL_INTEGRITY="$(sqlite3 "$DB_PATH" "PRAGMA integrity_check;")"
if [ "$FINAL_INTEGRITY" != "ok" ]; then
    echo "[restore] ERRO: banco restaurado reprovado no PRAGMA integrity_check: $FINAL_INTEGRITY" >&2
    exit 1
fi

if [ -d "$WORK_DIR/dpkeys" ]; then
    mkdir -p "$DPKEYS_DIR"
    cp -R "$WORK_DIR/dpkeys/." "$DPKEYS_DIR/"
fi

echo "[restore] Restauracao criptografada concluida e verificada com sucesso em $DATA_DIR"
