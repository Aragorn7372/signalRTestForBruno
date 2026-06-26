FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["signalRTestForBruno/signalRTestForBruno.csproj", "signalRTestForBruno/"]
RUN dotnet restore "signalRTestForBruno/signalRTestForBruno.csproj"
COPY . .
WORKDIR "/src/signalRTestForBruno"
RUN dotnet build "./signalRTestForBruno.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./signalRTestForBruno.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "signalRTestForBruno.dll"]
