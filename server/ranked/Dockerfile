# Built on the VPS by deploy/install.sh, from a copy of the parts of the repository it needs: this
# folder, the game's model code and the campaign's JSON (the server replays logs with the game's
# own rules, docs/ranked.md §4). Runs on the chiseled ASP.NET image: no shell, a non-root user.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY server/ranked/Ranked/Ranked.csproj server/ranked/Ranked/
RUN dotnet restore server/ranked/Ranked/Ranked.csproj
COPY . .
RUN dotnet publish server/ranked/Ranked/Ranked.csproj -c Release --no-restore -p:DebugType=None -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
LABEL org.opencontainers.image.title=demolition-ranked
WORKDIR /app
COPY --from=build /out .
# The database lives in /data, a folder on the host (compose.yaml).
ENV ASPNETCORE_HTTP_PORTS=5090 DOTNET_EnableDiagnostics=0
EXPOSE 5090
ENTRYPOINT ["dotnet", "Ranked.dll"]
