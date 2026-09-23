# Suruga
A Lavalink-less Discord music bot written in C#.

## Usage

### Local Setup

#### Requirements

Before running Suruga locally, make sure the following services are installed and running:
* [Invidious Companion](https://github.com/iv-org/invidious-companion)

1. Start Invidious Companion.
2. Download the latest release from the [Releases](https://github.com/waylaa/Suruga/releases) page.
3. Rename `.env.example` to `.env`.
4. Open `.env` and set your configuration. Make sure to provide your `BOT_TOKEN`.
5. Run the executable.

### Docker/Podman Setup

#### Requirements

* Docker with Docker Compose or Podman

> [!IMPORTANT]  
> If using Podman, replace ```docker compose``` with ```podman compose``` in the commands below.

1. Clone the repository:

```bash
git clone https://github.com/waylaa/Suruga
cd Suruga
```

2. Copy/Rename `.env.example` to `.env` and configure `BOT_TOKEN`.

3. Start the container:

```bash
docker compose up -d
```

You should see the `suruga-bot` container running.

To shut down the container, run:
```bash
docker compose down
```

## Building / Contributing

### Prerequisites

* .NET 10 SDK

1. Install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0
2. Clone the repository:

```bash
git clone https://github.com/waylaa/Suruga
cd Suruga
```

3. Restore dependencies and build:

```bash
dotnet restore
dotnet build
```

Alternatively, open `Suruga.slnx` in Visual Studio/Rider and build the solution.

> [!WARNING]
> If you plan to update the bundled runtimes on Windows, you may need to enable **Developer Mode**.
The Linux FFmpeg builds contain symlinks, which can cause issues when extracting or copying them on
Windows without the required permissions.

## Third-Party Software

- **FFmpeg** — used for audio decoding. The bundled LGPLv2.1 libraries are sourced from
  [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## License
This project is licensed under the [MIT](https://choosealicense.com/licenses/mit/) License.
