#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY Remeter/Portal/Server/Remeter.Portal.Server.csproj Remeter/Portal/Server/
COPY Remeter/Portal/Shared/Remeter.Portal.Shared.csproj Remeter/Portal/Shared/
COPY Remeter/Portal/Client/Remeter.Portal.Client.csproj Remeter/Portal/Client/
COPY Remeter/Portal/Models/Remeter.Portal.Models.csproj Remeter/Portal/Models/
COPY Remeter/Portal/JSInterop/Remeter.Portal.JSInterop.csproj Remeter/Portal/JSInterop/
RUN dotnet restore "Remeter/Portal/Server/Remeter.Portal.Server.csproj"
COPY . .
WORKDIR "/src/Remeter/Portal/Server"
RUN dotnet build "Remeter.Portal.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Remeter.Portal.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Remeter.Portal.Server.dll"]
