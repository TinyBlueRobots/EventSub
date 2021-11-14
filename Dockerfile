FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine
ADD /build build
WORKDIR /build
ENTRYPOINT ["dotnet", "Web.dll"]