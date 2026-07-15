# ShopHub

Web application (front-end + back-end) used by end users to create, configure, and delete
their Shop deployments running in a Kubernetes cluster.

## Structure

```
shophub-app/
├── backend/                     # ASP.NET Core Web API solution
│   ├── ShopHub.sln
│   ├── src/ShopHub.Api/
│   └── tests/
│       ├── ShopHub.Api.Tests/              # unit tests
│       └── ShopHub.Api.IntegrationTests/   # integration tests (Testcontainers)
├── frontend/                    # React (Vite + TypeScript) app
├── docker-compose.yml           # local dev / integration test infrastructure
└── .github/workflows/ci.yml     # CI pipeline (build, test, image publish)
```

## Responsibilities

- User authentication & registration (optionally Web3 wallet auth)
- CRUD for Shop sites: name, availability (`standard`/`high` replicas), payout wallet address, database choice (`standard` = PostgreSQL, `light` = Redis)
- Triggers deployment of Shop sites via the Shop operator CRDs (see the `shophub-shop-operator` repo)
- Links out to each Shop's running site

## Related repositories

- `shophub-shop` — the Shop storefront application deployed per-tenant
- `shophub-shop-operator` — Kubernetes operator providing the `Shop`, `DiscordChannel`, and `Wallet` CRDs
- `shophub-helm-charts` — Helm charts for ShopHub and the Shop operator
- `shophub-kube-state` — desired-state configuration for the Kubernetes cluster
