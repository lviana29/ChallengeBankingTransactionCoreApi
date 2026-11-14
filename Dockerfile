FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /app

COPY . .
WORKDIR /app/src/${API_PROJ}
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runner
WORKDIR /app

LABEL maintainer="<leo@exemplo.com>"
LABEL project="${PROJECT_BASE}"
LABEL module="${SERVICE_NAME}-Service"

COPY --from=builder /app/publish .

EXPOSE 8080
EXPOSE 8081

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "${API_PROJ}.dll"]
