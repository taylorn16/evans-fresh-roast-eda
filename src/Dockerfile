FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish EvansFreshRoast.Api/EvansFreshRoast.Api.fsproj -c release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "EvansFreshRoast.Api.dll"]
