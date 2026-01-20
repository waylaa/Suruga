# Suruga
A multi-functional Discord bot containg moderation, music and large language model related commands.

## Overview
- Moderation features (kick/ban/prune)
- Server features (channel create, channel delete)
- Music features (Youtube Music, Local files, Playlists & Queues)
- LLM features (Chat with a large language model of your liking)

## Usage
1. Get the latest build from the [Releases](https://github.com/waylaa/Suruga/releases) page.
2. Open your preferred terminal and run whichever command you want below.
   
### Windows

#### Include Moderation and Server Commands
```
.\Suruga.exe --token="..."
```

#### Include Music Commands
```
.\Suruga.exe --token="..." --enable-music-commands
```

⚠️ Before running the bot with music commands enabled, make sure to open [Lavalink](https://github.com/lavalink-devs/Lavalink) in your terminal first!

##### Optional Lavalink Commands
```
--lavalink-rest-hostname (Default: http://localhost:2333)
--lavalink-websocket-hostname (Default: ws://localhost:2333/v4/websocket)
--lavalink-password (Default: youshallnotpass)
```

⚠️ Make sure to transfer the new rest/websocket hostname or password to the appropriate arguments above if you changed them in Lavalink's application.yml file.

#### Include LLM Commands
```
.\Suruga.exe --token="..." --llm-model="path/to/gguf/model"
```

##### Optional LLM Commands
```
--llm-instructions="..."
```

### MacOS/Linux
Same command usage as above without the .exe extension.

## Building/Contributing
### 1. Prerequisites (Visual Studio Installer)
  - .NET desktop development
    
### 2. Install .NET 8
- Make sure you have .NET 8 installed on your machine.
- If not installed, download and install it from [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

### 3. Clone
- Open your terminal or command prompt.
- Navigate to the directory where you want to clone the repository:
```
cd <dir>
```

- Clone the repository
```
git clone https://github.com/waylaa/OsuCollectionDownloader.git
```

- Open the .sln file, restore the nuget packages and you're done.

## License
[MIT](https://choosealicense.com/licenses/mit/)
