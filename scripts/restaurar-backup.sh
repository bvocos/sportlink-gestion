#!/usr/bin/env bash
# Descifra un backup de producción descargado de GitHub Actions y, opcionalmente, lo restaura.
#
# Uso:
#   scripts/restaurar-backup.sh <carpeta-del-backup> <llave-privada.pem>
#       -> deja sportlink.dump descifrado en esa carpeta
#   scripts/restaurar-backup.sh <carpeta-del-backup> <llave-privada.pem> "<postgresql://...destino>"
#       -> además lo restaura en esa base (¡reemplaza su contenido!)
#
# Requiere openssl y, para restaurar, Docker (usa la imagen postgres:18).
set -euo pipefail
dir="${1:?carpeta del backup}"; key="${2:?llave privada .pem}"; dest="${3:-}"
cd "$dir"
sha256sum -c SHA256SUMS
openssl pkeyutl -decrypt -inkey "$key" -pkeyopt rsa_padding_mode:oaep -in clave.bin.enc -out clave.bin
openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 -in sportlink.dump.enc -out sportlink.dump -pass file:clave.bin
rm -f clave.bin
echo "Descifrado: $dir/sportlink.dump"
if [ -n "$dest" ]; then
  read -r -p "Esto REEMPLAZA los datos de la base destino. Escribí SI para continuar: " ok
  [ "$ok" = "SI" ] || { echo "Cancelado."; exit 1; }
  docker run --rm -e PGSSLMODE=prefer -v "$PWD:/work" postgres:18 \
    pg_restore --dbname="$dest" --clean --if-exists --no-owner --no-privileges /work/sportlink.dump
  echo "Restaurado."
fi
