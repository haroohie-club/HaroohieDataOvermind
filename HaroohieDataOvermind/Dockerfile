FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["HaroohieDataOvermind/HaroohieDataOvermind.csproj", "HaroohieDataOvermind/"]
COPY ["NuGet.Config", "NuGet.Config"]
RUN dotnet restore "HaroohieDataOvermind/HaroohieDataOvermind.csproj"
COPY . .
WORKDIR "/src/HaroohieDataOvermind"
RUN dotnet build "./HaroohieDataOvermind.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HaroohieDataOvermind.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HaroohieDataOvermind.dll"]
