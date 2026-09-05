# The API, built and run from source.
#
# Two stages so the runtime image carries no SDK: the build image is ~800 MB,
# the runtime one a fraction of that, and a smaller image is a faster deploy
# and a smaller attack surface on a box that faces the internet.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The project file alone first, so `restore` is cached and only re-runs when a
# dependency actually changes — editing a controller should not re-download
# ML.NET on every build.
COPY BullEvents.Api.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# QuestPDF renders the quotation with real font metrics; the runtime image ships
# no fonts at all, so text would fall back to boxes without these. fontconfig is
# what lets it find them.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

# Not root. A container that is compromised should not also be privileged.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin api \
    && chown -R api:api /app
USER api

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "BullEvents.Api.dll"]
