#!/bin/sh
set -eu

: "${PORT:=8080}"
: "${DataProtection__KeysPath:=/tmp/hostelpro-authority/DataProtectionKeys}"

export ASPNETCORE_URLS="http://+:${PORT}"
export DataProtection__KeysPath

mkdir -p "${DataProtection__KeysPath}"

exec dotnet "$@"
