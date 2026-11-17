# --- Estágio de Build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "src/ChallengeBankingTransactionCore.Service.Api.Transaction/ChallengeBankingTransactionCore.Service.Api.Transaction.csproj" -c Release -o /app/publish

# --- Estágio Final / Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl unzip

# Copia os binários compilados da aplicação do estágio anterior
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ChallengeBankingTransactionCore.Service.Api.Transaction.dll"]
