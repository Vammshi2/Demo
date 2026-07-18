#!/bin/sh
set -eu

: "${PORT:=8080}"
: "${Storage__DataPath:=/tmp/hostelpro-data}"
: "${DataProtection__KeysPath:=${Storage__DataPath}/DataProtectionKeys}"

export ASPNETCORE_URLS="http://+:${PORT}"
export Storage__DataPath
export DataProtection__KeysPath

mkdir -p "${Storage__DataPath}" "${DataProtection__KeysPath}" "${Storage__DataPath}/Uploads/Kyc"

exec dotnet "$@"
