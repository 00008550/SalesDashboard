# AI_NOTES

This is the short reflection requested by the assignment; detailed prompts remain in `AI_PROMPTS.md`.

## Agents and ownership

- **Claude Code (Opus 4.8)** was the primary implementation agent for the backend, migrations/seed, analytical SQL, React UI, Docker setup, and tests.
- **OpenAI Codex** independently reviewed and verified the published PR in read-only mode; after it found the corrective issues, an explicitly authorized Codex pass implemented and re-verified the fixes.
- I delegated mechanical scaffolding and implementation, but manually froze the business/date semantics, one-sale aggregation grain, module boundaries, and scope.
- I also challenged the generated work with SQL ground truth, real database upgrades/restarts, measured browser states at 1440x900, accessibility scans, and container scans.

## Where AI helped

- AI accelerated repetitive EF configuration, seed generation, Dapper queries, component construction, and focused regression-test creation.
- Separate implementation and review roles were valuable: green tests alone had hidden data-preservation, layout, retry, accessibility, and image-package defects.

## Where I changed or rejected AI output

- I rejected SignalR/sample-sale simulation, an outbox, CQRS/MediatR, generic repositories, microservices, and extra deployment work because none solved a timeboxed requirement.
- Genuine AI errors included a stale migration, incorrect cross-schema keys, non-UTC Npgsql parameters, an ambiguous trend bucket, and an unsafe broad same-version seed repair.
- The UI also regressed when a ranking-list height fix let intrinsic grid sizing stretch neighboring cards; browser dimensions and keyboard scrolling exposed it.
- The first container review incorrectly called known OpenSSL/libexpat findings unfixable even though patched Alpine packages were available; the corrective build upgrades them and rescans rather than assuming.

## Verification discipline

- I do not accept generated code because it compiles: API aggregates are reconciled independently with PostgreSQL, including Revenue, Cost, Gross Profit, Paid Sales, trend, and categories.
- Migration and seed checks use fresh, stale, lost/recovered, and real `origin/master` databases, comparing row counts, IDs, and post-seed timestamps across restarts.
- Frontend checks cover presets, custom-range validation, ranking, loading/updating/error/retry/empty states, keyboard use, console/network behavior, and axe—not only the default view.
- Final evidence is reported in the PR after rerunning builds, tests, Docker startup, Docker Scout, formatting, and clean-worktree checks; this note intentionally does not preserve stale interim totals.
