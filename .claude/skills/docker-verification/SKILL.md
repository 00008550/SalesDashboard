---
name: docker-verification
description: The evaluator-equivalent clean-start procedure for `docker compose up --build`. Load when adding/changing anything that affects startup (compose, Dockerfiles, migrations, seed, readiness, nginx proxy) and before final submission. Confirms the whole stack comes up populated with no manual steps, and that API readiness reflects completed migration + seed — not just a live Postgres port.
---

# Docker verification — the evaluator's exact path

The evaluator clones and runs one command. This procedure reproduces that from a clean state; run it
early (as soon as the DB/API/web slice exists) and again with clean volumes before submission.

## Clean-start run

```bash
docker compose down -v          # remove containers AND the db volume — a true clean state
docker compose up --build       # the exact evaluator command
```

Watch the startup ordering resolve on its own (healthchecks, not sleeps):

1. **db** becomes healthy (`pg_isready`).
2. **api** waits for db-healthy, then **applies migrations** and **runs the deterministic seed inside
   one transaction**.
3. **api readiness** flips to healthy **only after migration + seed succeed** — `pg_isready` alone is
   not sufficient; `/api/health/ready` must report the app is actually ready.
4. **web** waits for api-healthy, serves the SPA, and proxies `/api` to the api container.

## Assertions (each must hold with no manual command)

```bash
docker compose ps                                   # db & api healthy, web up
curl -fsS http://localhost:8080/api/health/ready     # 200 once migrated+seeded
curl -fsS "http://localhost:8080/api/dashboard?preset=last30" | head -c 400   # populated JSON via the web proxy
```

- No manual DB creation, SQL script, PgAdmin, `npm install`, migration, or seed command was needed.
- The browser at `http://localhost:8080` shows a **populated** dashboard.
- Seed is **idempotent**: `docker compose restart api` (volume intact) must **not** duplicate data —
  confirm counts are unchanged:
  ```bash
  docker compose exec -T db psql -U app -d salesdashboard -c "SELECT count(*) FROM sales.sales;"
  ```
- Determinism (shape): after `down -v` + `up --build`, row counts and distributions match the prior
  clean run (dates re-anchor to the new seed date by design).

## Failure triage
- api restart-looping → read `docker compose logs api`: migration or seed exception, or readiness
  never satisfied. Fix the root cause; do not add blind sleeps.
- dashboard empty but api healthy → check the nginx `/api` proxy and the web build.
- readiness green but data absent → the readiness gate is wrong: it must depend on seed completion.

## Report
Paste the real `docker compose ps`, the readiness curl, and the dashboard curl output. "It should
work" is not verification — quote the commands and their output.
