# Suruga
A Lavalink-less Discord music bot written in C#.

## Usage

### Local Setup

#### Requirements

Before running Suruga locally, make sure the following services are installed and running:

* [MongoDB](https://www.mongodb.com/)
* [Invidious Companion](https://github.com/iv-org/invidious-companion)

1. Start your MongoDB instance.
2. Start Invidious Companion.
3. Download the latest release from the [Releases](https://github.com/waylaa/Suruga/releases) page.
4. Rename `.env.example` to `.env`.
5. Open `.env` and adjust any settings to suit your environment, most importantly `BOT_TOKEN`.
6. Run the executable.

### Docker Setup

#### Requirements

* Docker with Docker Compose

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

To shutdown the container, run:
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

## License
This project is licensed under the [MIT](https://choosealicense.com/licenses/mit/) License.

### Third-Party Components

This repository redistributes prebuilt FFmpeg binaries for audio decoding under the GNU Lesser General Public License v2.1 (LGPL-2.1).

* FFmpeg Copyright © the FFmpeg developers
* FFmpeg website: https://ffmpeg.org/
* FFmpeg source code: https://git.ffmpeg.org/ffmpeg.git

A copy of the LGPL v2.1 license is included at `runtimes/licenses/ffmpeg/COPYING.LGPLv2.1`
