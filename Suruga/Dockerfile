FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Suruga.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c $BUILD_CONFIGURATION -p:DebugType=None -p:DebugSymbols=false -p:PublishDir=/app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Suruga"]