# syntax=docker/dockerfile:1
ARG TARGETARCH

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH

WORKDIR /src

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
    --self-contained true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -p:PublishDir=/app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime-amd64
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl xz-utils \
    && curl -fL -o /tmp/ffmpeg.tar.xz "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n9.0-latest-linux64-lgpl-shared-9.0.tar.xz" \
    && mkdir -p /opt/ffmpeg \
    && tar -xf /tmp/ffmpeg.tar.xz -C /opt/ffmpeg --strip-components=1 \
    && rm -f /tmp/ffmpeg.tar.xz \
    && apt-get purge -y curl xz-utils \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*
ENV BOT_FFMPEGPATH=/opt/ffmpeg/lib
ENV LD_LIBRARY_PATH=/opt/ffmpeg/lib
ENV PATH="/opt/ffmpeg/bin:${PATH}"

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime-arm64
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl xz-utils \
    && curl -fL -o /tmp/ffmpeg.tar.xz "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n9.0-latest-linuxarm64-lgpl-shared-9.0.tar.xz" \
    && mkdir -p /opt/ffmpeg \
    && tar -xf /tmp/ffmpeg.tar.xz -C /opt/ffmpeg --strip-components=1 \
    && rm -f /tmp/ffmpeg.tar.xz \
    && apt-get purge -y curl xz-utils \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*
ENV BOT_FFMPEGPATH=/opt/ffmpeg/lib
ENV LD_LIBRARY_PATH=/opt/ffmpeg/lib
ENV PATH="/opt/ffmpeg/bin:${PATH}"

FROM runtime-${TARGETARCH} AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Suruga"]