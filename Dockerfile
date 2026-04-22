FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY NotificationService.csproj ./
RUN dotnet restore NotificationService.csproj

COPY . ./
RUN dotnet publish NotificationService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "NotificationService.dll"]
