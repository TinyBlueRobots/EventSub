FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
ADD /build build
WORKDIR /build
EXPOSE 80
ENTRYPOINT ["dotnet", "Web.dll"]