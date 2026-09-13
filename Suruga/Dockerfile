ARG TARGETARCH

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH

WORKDIR /src

# NativeAOT setup
RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

COPY ["Suruga.Common/Suruga.Common.csproj", "Suruga.Common/"]
COPY ["Suruga/Suruga.csproj", "Suruga/"]
COPY ["Suruga.FFmpeg/Suruga.FFmpeg.csproj", "Suruga.FFmpeg/"]
COPY ["Suruga.PostProcessing/Suruga.PostProcessing.csproj", "Suruga.PostProcessing/"]

# Map Docker's TARGETARCH (amd64/arm64) to a .NET RID
RUN RID=$(if [ "$TARGETARCH" = "arm64" ]; then echo "linux-arm64"; else echo "linux-x64"; fi) \
    && echo "$RID" > /tmp/rid.txt

RUN dotnet restore "Suruga/Suruga.csproj" -r $(cat /tmp/rid.txt)

COPY . .

RUN dotnet publish "Suruga/Suruga.csproj" \
    -c $BUILD_CONFIGURATION \
    -r $(cat /tmp/rid.txt) \
    -p:PublishDir=/app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble AS runtime
WORKDIR /app

COPY --from=publish /app/publish .

ENV LD_LIBRARY_PATH=/app
ENTRYPOINT ["./Suruga"]
