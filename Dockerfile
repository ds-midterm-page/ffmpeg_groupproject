# MediaTool (.NET) container.
#   App:   docker compose -f compose.mediatool.yml up --build      -> http://localhost:5080
#   Tests: docker build --target test -t mediatool-test . && docker run --rm mediatool-test

ARG DOTNET_VERSION=10.0
ARG YTDLP_VERSION=2026.10.26

# ---- build: restore (cached by project files) and publish ----
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY global.json Directory.Build.props MediaTool.sln ./
COPY src/MediaTool.Core/MediaTool.Core.csproj src/MediaTool.Core/
COPY src/MediaTool.Media/MediaTool.Media.csproj src/MediaTool.Media/
COPY src/MediaTool.App/MediaTool.App.csproj src/MediaTool.App/
COPY tests/MediaTool.Core.Tests/MediaTool.Core.Tests.csproj tests/MediaTool.Core.Tests/
COPY tests/MediaTool.Integration.Tests/MediaTool.Integration.Tests.csproj tests/MediaTool.Integration.Tests/
RUN dotnet restore MediaTool.sln
COPY src/ src/
COPY tests/ tests/
# Restore again now that the .razor files are present: Blazor's framework script (blazor.web.js) package is
# only added when the SDK can see them. The earlier restore still caches every other package.
RUN dotnet restore MediaTool.sln \
 && dotnet publish src/MediaTool.App -c Release -o /app --no-restore \
 && test -f /app/wwwroot/_framework/blazor.web.js

# ---- tools: official yt-dlp Linux binary, checked against the release's published checksums ----
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS tools
ARG YTDLP_VERSION
WORKDIR /dl
RUN curl -fsSL -o yt-dlp_linux "https://github.com/yt-dlp/yt-dlp/releases/download/${YTDLP_VERSION}/yt-dlp_linux" \
 && curl -fsSL -o SHA2-256SUMS "https://github.com/yt-dlp/yt-dlp/releases/download/${YTDLP_VERSION}/SHA2-256SUMS" \
 && grep " yt-dlp_linux$" SHA2-256SUMS | sha256sum -c - \
 && install -m 0755 yt-dlp_linux /usr/local/bin/yt-dlp

# ---- test: full test suite with real yt-dlp and ffmpeg (integration tests need network at run time) ----
FROM build AS test
RUN apt-get update \
 && apt-get install -y --no-install-recommends ffmpeg \
 && rm -rf /var/lib/apt/lists/*
COPY --from=tools /usr/local/bin/yt-dlp /usr/local/bin/yt-dlp
ENV MEDIATOOL_YTDLP=/usr/local/bin/yt-dlp
RUN dotnet build MediaTool.sln -c Release --no-restore
ENTRYPOINT ["dotnet", "test", "MediaTool.sln", "-c", "Release", "--no-build", "--logger", "console;verbosity=normal"]

# ---- final: small runtime image, non-root ----
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
RUN apt-get update \
 && apt-get install -y --no-install-recommends ffmpeg \
 && rm -rf /var/lib/apt/lists/*
COPY --from=tools /usr/local/bin/yt-dlp /usr/local/bin/yt-dlp
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:5080 \
    YtDlp__ExecutablePath=/usr/local/bin/yt-dlp \
    Ffmpeg__ExecutablePath=/usr/bin/ffmpeg
EXPOSE 5080
USER app
ENTRYPOINT ["dotnet", "MediaTool.App.dll"]
