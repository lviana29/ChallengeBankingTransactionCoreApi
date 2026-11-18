#!/bin/bash

# Cores para o terminal
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# Configurações básicas
GH_NAMESPACE="challengebanking"
GH_IMAGE_NAME="challengebanking.api.transaction"
GH_REGISTRY="ghcr.io/${GH_NAMESPACE}/${GH_IMAGE_NAME}"

BASE_COMPOSE="docker-compose.yml"
MYSQL_CONTAINER_NAME="ChallengeBankingTransactionCore_MySQL_Dev"

usage() {
    echo -e "${YELLOW}Uso: $0 {dev} {build|up|down|logs|loadtest} [versao]{NC}"
    echo -e "${YELLOW}Exemplo DEV:  $0 dev up${NC}"
    echo -e "${YELLOW}Exemplo LOAD: $0 dev loadtest${NC}"
    exit 1
}

if [ $# -lt 2 ]; then usage; fi

ENV=$1
ACTION=$2

# --- LÓGICA DE AMBIENTE E REDE ---
if [ "$ENV" == "dev" ]; then
    VERSION="dev"
    SPECIFIC_COMPOSE="docker-compose-dev.yml"
    NETWORK_NAME="ChallengeBanking_network_dev"
else
    echo -e "${RED}❌ Ambiente inválido para este escopo de teste local. Use 'dev'.${NC}"
    usage
fi

SERVICE_NAME_BASE="ChallengeBankingTransactionCore.Api.Transaction.${ENV}"
GH_REGISTRY_LOWER=$(echo "$GH_REGISTRY" | tr '[:upper:]' '[:lower:]')

export ENV=$ENV
export TAG_NAME=$VERSION
export NETWORK_NAME=$NETWORK_NAME
export IMAGE_URL="${GH_REGISTRY_LOWER}:${VERSION}"

case "$ACTION" in
    build)
        echo -e "${YELLOW}🔨 Efetuando Build local para $ENV (.NET 10)...${NC}"
        docker build -t ${IMAGE_URL} .

        if [ $? -eq 0 ]; then
            echo -e "${GREEN}✅ Build concluído com sucesso!${NC}"
        else
            echo -e "${RED}❌ Falha no build.${NC}"
            exit 1
        fi
        ;;

    up)
        # Garante que a rede Docker externa exista
        if ! docker network ls | grep -q "$NETWORK_NAME"; then
            echo -e "${YELLOW}🌐 Criando rede $NETWORK_NAME...${NC}"
            docker network create $NETWORK_NAME
        fi

        # Verifica se o container do MySQL já existe (rodando ou parado)
        if [ "$(docker ps -a -q -f name=^/${MYSQL_CONTAINER_NAME}$)" ]; then
            if [ "$(docker ps -q -f name=^/${MYSQL_CONTAINER_NAME}$)" ]; then
                echo -e "${GREEN}ℹ️  Container do MySQL já está rodando. Mantendo base de dados existente.${NC}"
            else
                echo -e "${YELLOW}📦 Container do MySQL existe mas está parado. Iniciando...${NC}"
                docker start ${MYSQL_CONTAINER_NAME}
            fi
        else
            echo -e "${YELLOW}🚀 MySQL não encontrado. O Docker Compose irá criá-lo e inicializar a base nova...${NC}"
        fi

        echo -e "${YELLOW}🚀 Subindo microsserviço [$ENV] e dependências...${NC}"
        docker compose -f $BASE_COMPOSE -f $SPECIFIC_COMPOSE up -d --build

        echo -e "${GREEN}✅ Ambiente local rodando com sucesso!${NC}"
        echo -e "${GREEN}📍 API disponível em: http://localhost:3050/scalar/v1${NC}"
        ;;

    loadtest)
        echo -e "${YELLOW}⚡ Executando teste de carga (55 req/s) no endpoint de consolidado diário...${NC}"
        if command -v k6 &> /dev/null; then
            k6 run tests/load/k6-consolidated-balance.js
        else
            docker run --rm -i --network ${NETWORK_NAME} -e BASE_URL="http://api:8080" -v "$(pwd)/tests/load:/scripts" grafana/k6 run /scripts/k6-consolidated-balance.js
        fi
        ;;

    down)
        echo -e "${YELLOW}🛑 Parando aplicação (mantendo os dados do MySQL salvos no volume)...${NC}"
        docker compose -f $BASE_COMPOSE -f $SPECIFIC_COMPOSE down
        ;;

    logs)
        docker logs -f ${SERVICE_NAME_BASE}
        ;;
    *) usage ;;
esac
