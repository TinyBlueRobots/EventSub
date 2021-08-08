#!/bin/bash
set -e
dotnet test ./tests
rm -rf build
dotnet publish ./src/Web/Web.csproj -o "$(pwd)"/build -c Release