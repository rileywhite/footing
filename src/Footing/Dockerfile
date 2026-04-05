FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
ARG version=0.0.0-local
ARG assembly_version=0.0.0.0
ARG configuration=Release
WORKDIR /src
COPY ["Footing/Footing.csproj", "Footing/"]
COPY ["Footing.Client/Footing.Client.csproj", "Footing.Client/"]
RUN dotnet restore "Footing/Footing.csproj"
COPY . .
WORKDIR "/src/Footing"
RUN dotnet publish "Footing.csproj" -c ${configuration} -o /app/publish \
    /p:Version=${version} \
    /p:AssemblyVersion=${assembly_version} \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Footing.dll"]
