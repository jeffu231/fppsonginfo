FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["FPPSongInfo/FPPSongInfo.csproj", "FPPSongInfo/"]

RUN dotnet nuget locals all -c

RUN dotnet restore "FPPSongInfo/FPPSongInfo.csproj"
COPY . .
WORKDIR "/src/FPPSongInfo"
RUN dotnet build "FPPSongInfo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FPPSongInfo.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FPPSongInfo.dll"]

LABEL org.opencontainers.image.source=https://github.com/jeffu231/FPPSongInfo
LABEL org.opencontainers.image.description="FPP Song Info - An API to consume FPP messages about the current media playing and write to a file."
LABEL org.opencontainers.image.licenses=MIT
