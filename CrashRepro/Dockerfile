FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CrashRepro/CrashRepro.csproj", "CrashRepro/"]
COPY ["nldksample/nldksample.csproj", "nldksample/"]
COPY ["NLDK/NLDK.csproj", "NLDK/"]
RUN dotnet restore "CrashRepro/CrashRepro.csproj"
COPY . .
WORKDIR "/src/CrashRepro"
RUN dotnet build "CrashRepro.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "CrashRepro.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CrashRepro.dll"]
