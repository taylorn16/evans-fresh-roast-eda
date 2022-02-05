FROM mcr.microsoft.com/dotnet/sdk:6.0 AS base
WORKDIR /src

RUN curl -fsSL https://deb.nodesource.com/setup_16.x | bash
RUN apt-get install -y nodejs

FROM base AS build
WORKDIR /src
COPY . .

# Build client app and dump it in /src/EvansFreshRoast.Api/wwwroot
RUN cd EvansFreshRoast.Client && \
    dotnet new tool-manifest && \
    dotnet tool install --local Fable --version 3.6.3 && \
    npm install && \
    npm run build

# Publish release dlls, will copy wwwroot automatically
RUN dotnet publish EvansFreshRoast.Api/EvansFreshRoast.Api.fsproj -c release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "EvansFreshRoast.Api.dll"]
