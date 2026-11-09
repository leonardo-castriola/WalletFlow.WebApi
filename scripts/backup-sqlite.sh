#!/bin/sh
set -eu

MODE="${1:-scheduled}"
DATA_DIR="${DATA_DIR:-/data}"
DB_PATH="${DB_PATH:-$DATA_DIR/Wallet.db}"
DPKEYS_DIR="${DPKEYS_DIR:-$DATA_DIR/dpkeys}"
BACKUP_DIR="${BACKUP_DIR:-/backups}"
OFFSITE_DIR="${BACKUP_OFFSITE_DIR:-/backups-offsite}"
OFFSITE_CMD="${BACKUP_OFFSITE_CMD:-}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-30}"

if [ -z "${BACKUP_ENCRYPTION_KEY:-}" ] || [ "${#BACKUP_ENCRYPTION_KEY}" -lt 16 ]; then
    echo "[backup] ERRO: defina BACKUP_ENCRYPTION_KEY com pelo menos 16 caracteres." >&2
    exit 1
fi

if [ ! -f "$DB_PATH" ]; then
    echo "[backup] Banco de dados ainda nao encontrado em $DB_PATH ($MODE); nada para copiar."
    exit 0
fi

TIMESTAMP="$(date -u +%Y%m%d-%H%M%SZ)"
PREFIX="walletflow-backup"
if [ "$MODE" = "--pre-migrate" ]; then
    PREFIX="walletflow-pre-migrate"
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

SNAPSHOT_DB="$WORK_DIR/Wallet.db"

# Backup online transacional consistente com modo WAL ativo
sqlite3 "$DB_PATH" "VACUUM INTO '$SNAPSHOT_DB';"

# Verifica a integridade do snapshot antes de empacotar e criptografar
INTEGRITY="$(sqlite3 "$SNAPSHOT_DB" "PRAGMA integrity_check;")"
if [ "$INTEGRITY" != "ok" ]; then
    echo "[backup] ERRO: falha na verificacao de integridade do snapshot: $INTEGRITY" >&2
    exit 1
fi

if [ -d "$DPKEYS_DIR" ]; then
    cp -R "$DPKEYS_DIR" "$WORK_DIR/dpkeys"
else
    mkdir -p "$WORK_DIR/dpkeys"
fi

mkdir -p "$BACKUP_DIR"
ARCHIVE_NAME="$PREFIX-$TIMESTAMP.tar.gz.enc"
ARCHIVE_PATH="$BACKUP_DIR/$ARCHIVE_NAME"

tar -C "$WORK_DIR" -czf "$WORK_DIR/payload.tar.gz" Wallet.db dpkeys
openssl enc -aes-256-cbc -pbkdf2 -iter 600000 -salt \
    -in "$WORK_DIR/payload.tar.gz" \
    -out "$ARCHIVE_PATH" \
    -pass env:BACKUP_ENCRYPTION_KEY

(cd "$BACKUP_DIR" && sha256sum "$ARCHIVE_NAME" > "$ARCHIVE_NAME.sha256")

# Copia criptografada para destino externo / fora do volume principal
if [ -n "$OFFSITE_DIR" ] && [ -d "$OFFSITE_DIR" ]; then
    cp "$ARCHIVE_PATH" "$ARCHIVE_PATH.sha256" "$OFFSITE_DIR/"
    find "$OFFSITE_DIR" -maxdepth 1 -type f -name "walletflow-*" -mtime +"$RETENTION_DAYS" -delete
fi

if [ -n "$OFFSITE_CMD" ]; then
    sh -c "$OFFSITE_CMD \"$ARCHIVE_PATH\" \"$ARCHIVE_PATH.sha256\""
fi

# Politica de retencao
find "$BACKUP_DIR" -maxdepth 1 -type f -name "walletflow-*" -mtime +"$RETENTION_DAYS" -delete

echo "[backup] Backup criptografado ($MODE) concluido e validado: $ARCHIVE_PATH"
