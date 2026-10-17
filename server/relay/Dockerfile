# Built on the VPS by deploy/install.sh. Runs on the chiseled ASP.NET image: no shell, no
# package manager, a non-root user.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Relay/Relay.csproj Relay/
RUN dotnet restore Relay/Relay.csproj
COPY Relay/ Relay/
RUN dotnet publish Relay/Relay.csproj -c Release --no-restore -p:DebugType=None -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
LABEL org.opencontainers.image.title=demolition-relay
WORKDIR /app
COPY --from=build /out .
# The image listens on 8080 by default. No diagnostics pipes, so the root filesystem can be read-only.
ENV ASPNETCORE_HTTP_PORTS=5080 DOTNET_EnableDiagnostics=0
EXPOSE 5080
ENTRYPOINT ["dotnet", "Relay.dll"]
