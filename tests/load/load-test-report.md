# Relatório de Teste de Carga — Posição Consolidada Diária (50+ req/s)

**Requisito Oficial:** `RNF-02` e Critério `6.3` ([ESCOPO_GLOBAL_CONSOLIDADO.md](../../../ESCOPO_GLOBAL_CONSOLIDADO.md))  
**Meta Oficial:** Sustentar pico de **50 requisições por segundo** no serviço de consolidado diário com **no máximo 5% de perda de requisições** (`http_req_failed < 0.05`).  
**Script de Teste:** [k6-consolidated-balance.js](k6-consolidated-balance.js)

---

## 1. Configuração do Cenário (`k6`)

| Parâmetro | Valor Configurado | Racional Arquitetural |
| :--- | :--- | :--- |
| **Executor** | `constant-arrival-rate` | Garante vazão constante de chegada independente do tempo de resposta. |
| **Taxa Sustentada (`rate`)** | **55 req/s** (`timeUnit: '1s'`) | **10% acima** do pico mínimo exigido pelo negócio (50 req/s). |
| **Duração (`duration`)** | `30s` (1.653 requisições) | Valida estabilidade sustentada sob pressão contínua. |
| **VUs (`preAllocatedVUs` / `maxVUs`)** | `50` / `100` | Pool de conexões HTTP concorrentes sem gargalo no gerador de carga. |
| **Threshold 1 (Perda Máxima)** | `http_req_failed: ['rate<0.05']` | Limite estrito de perda $< 5\%$ (**resultado obtido: $0.00\%$**). |
| **Threshold 2 (Latência)** | `http_req_duration: ['p(95)<200', 'p(99)<500']` | Garante resposta sub-10ms via Cache-Aside (L1 Memory + L2 Redis). |

---

## 2. Como Executar o Teste de Carga

### Opção A — Automática via `devops.sh` (1 Comando)
```bash
cd Api
./devops.sh dev up
./devops.sh dev loadtest
```

### Opção B — Direta via CLI do `k6` ou Docker `grafana/k6`
```bash
# Se possuir k6 instalado localmente:
k6 run Api/tests/load/k6-consolidated-balance.js

# Ou via contêiner Docker oficial do k6 na rede da aplicação:
docker run --rm -i --network ChallengeBanking_network_dev -e BASE_URL="http://api:8080" -v "$(pwd)/Api/tests/load:/scripts" grafana/k6 run /scripts/k6-consolidated-balance.js
```

---

## 3. Resultados Reais Obtidos na Execução (`grafana/k6`)

Com a camada de **Cache Multi-Nível (`ConsolidatedCacheAdapter`: L1 `IMemoryCache` + L2 `Redis`)** combinada com o **Read Model pré-agregado (`DailyConsolidatedBalance`)** alimentado assincronamente pelo `ConsolidationWorkerHostedService`, o teste atingiu **100% de sucesso em todos os checks e thresholds**:

| Métrica Aferida | Requisito Mínimo | Resultado Real Obtido (`k6`) | Status |
| :--- | :---: | :---: | :---: |
| **Taxa de Chegada Sustentada (`scenarios`)** | $\ge 50\text{ req/s}$ | **55.00 iters/s** (`51.53 req/s` c/ setup sleep) | **Aprovado** |
| **Total de Requisições HTTP (30s)** | $\ge 1.500$ | **1.653 reqs** (`3.304 checks`) | **Aprovado** |
| **Taxa de Falha / Perda (`http_req_failed`)** | $\le 5.00\%$ | **0.00% (0 falhas em 1.653 reqs)** | **Aprovado** |
| **Checks de Validação (`checks_succeeded`)** | $100\%$ | **100.00% (3.304 / 3.304)** | **Aprovado** |
| **Latência Mediana (`med` / `p50`)** | N/A | **1.94 ms** (mín: `650.7 µs`, avg: `2.92 ms`) | **Excelente** |
| **Latência `p90`** | N/A | **3.23 ms** | **Excelente** |
| **Latência `p95`** | $< 200\text{ ms}$ | **4.01 ms** | **Aprovado** |
| **Latência `p99`** | $< 500\text{ ms}$ | **17.53 ms** | **Aprovado** |

---

## 4. Saída Oficial do Terminal (`grafana/k6`)

```text
     execution: local
        script: /scripts/k6-consolidated-balance.js
        output: -

     scenarios: (100.00%) 1 scenario, 100 max VUs, 1m0s max duration (incl. graceful stop):
              * consolidated_balance_peak: 55.00 iterations/s for 30s (maxVUs: 50-100, gracefulStop: 30s)

  █ THRESHOLDS 

    http_req_duration
    ✓ 'p(95)<200' p(95)=4.01ms
    ✓ 'p(99)<500' p(99)=17.53ms

    http_req_failed
    ✓ 'rate<0.05' rate=0.00%

  █ TOTAL RESULTS 

    checks_total.......: 3304    102.991472/s
    checks_succeeded...: 100.00% 3304 out of 3304
    checks_failed......: 0.00%   0 out of 3304

    ✓ setup transaction created (201/200)
    ✓ setup consolidated warmup (200)
    ✓ status is 200 OK
    ✓ contains consolidatedBalance

    HTTP
    http_req_duration..............: avg=2.92ms min=650.7µs med=1.94ms max=566.5ms  p(90)=3.23ms p(95)=4.01ms
      { expected_response:true }...: avg=2.92ms min=650.7µs med=1.94ms max=566.5ms  p(90)=3.23ms p(95)=4.01ms
    http_req_failed................: 0.00%  0 out of 1653
    http_reqs......................: 1653   51.526908/s

    EXECUTION
    iteration_duration.............: avg=2.95ms min=903.2µs med=2.32ms max=100.97ms p(90)=3.74ms p(95)=4.63ms
    iterations.....................: 1651   51.464564/s
    vus............................: 1      min=0         max=1 
    vus_max........................: 50     min=50        max=50

    NETWORK
    data_received..................: 1.3 MB 41 kB/s
    data_sent......................: 240 kB 7.5 kB/s

running (0m32.1s), 000/050 VUs, 1651 complete and 0 interrupted iterations
consolidated_balance_peak ✓ [ 100% ] 000/050 VUs  30s  55.00 iters/s
```
