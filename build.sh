set -e
rm -rf build
dotnet publish ./src/Web/Web.csproj -o $(pwd)/build -c Release