export CONNECTIONSTRING="Server=localhost;Port=5432;Database=test;User Id=postgres;Password=password;maximum pool size=30"
export DATABASE=PostgreSql
export APIKEY=myapikey
# export PORT=81
dotnet run -p ./src/Web/Web.csproj 