# AI_PROMPTS

A running log of the actual instructions I (the developer) sent to the coding agent during this
project, preserved as the real user-facing text rather than summaries. Newest entries are appended
at the bottom. This log intentionally excludes the model's hidden reasoning, tool traces, provider
system messages, and any secrets — only the messages I sent.

Tools used: **Claude Code (Opus 4.8)** as the primary coding agent.

---

## 2026-09-02 08:00 — Claude Code / Opus 4.8

_(Delivered as the `@"E:\Work\DIJ-Market.md"` file reference — this is that file's full text, the
engineering-lead brief that wraps the DJI-Market assignment.)_

> DJI-Market Take-Home — Engineering Lead Instructions
> We need to build the DJI-Market.ru take-home assignment: Sales Performance Dashboard.
> Read the entire task specification first. It is available in the project context / attached task document. Do not start implementing until you have analyzed the requirements and produced a concise implementation plan.
> The assignment is explicitly timeboxed to approximately 8 hours. The goal is not to build an over-engineered production platform. The goal is to demonstrate strong engineering judgment, a polished working product, correct business logic, good PostgreSQL usage, sensible architecture, tests, Dockerized startup, and an effective AI-assisted development process.
> 1. Existing engineering context — USE THIS
> I have already built two relevant projects:
> WM — Workforce Management Platform
> This is my existing .NET/Angular project.
> Technology and architectural context includes:
>
> * .NET 10 / ASP.NET Core
> * Angular + TypeScript
> * PostgreSQL
> * EF Core
> * Kafka
> * RabbitMQ / MassTransit
> * Redis
> * Docker
> * GitHub Actions
> * modular monolith architecture
> * explicit module contracts/events
> * plugin architecture
> * SignalR
> * RBAC and scoped authorization
> * automated testing
> * CI
> * AI-assisted development workflow
>
> The WM project should be inspected carefully for reusable engineering conventions and lessons.
> Aperture
> This is my AI-native B2B project.
> It is especially relevant because it uses:
>
> * .NET 10
> * ASP.NET Core
> * PostgreSQL
> * React + TypeScript
> * EF Core
> * Dapper
> * RabbitMQ
> * SignalR
> * multi-tenancy
> * RBAC / ABAC-style data scopes
> * optimistic concurrency
> * pessimistic locking where appropriate
> * idempotency
> * transactional outbox
> * API contracts
> * AI assistant
> * AI survey → implementation → review workflow
> * architecture invariants enforced through CI
>
> Inspect Aperture for patterns, decisions, mistakes, trade-offs and conventions that are useful for this assignment.
> IMPORTANT:
> Do NOT blindly copy architecture from WM or Aperture.
> Use them as accumulated engineering context. Reuse proven patterns where they genuinely fit, but independently evaluate every decision against the DJI-Market assignment and its 8-hour timebox.
> The resulting project should be simpler than Aperture where simplicity is appropriate.
> 2. First task: understand before coding
> Before writing implementation code:
>
> 1. Read the complete DJI-Market assignment.
> 2. Inspect WM.
> 3. Inspect Aperture.
> 4. Identify the requirements and acceptance criteria.
> 5. Identify ambiguities in the assignment.
> 6. Make explicit reasonable assumptions.
> 7. Design the minimal architecture that satisfies the requirements.
> 8. Identify what is mandatory versus optional.
> 9. Create a prioritized implementation plan optimized for the 8-hour timebox.
>
> Do not implement speculative features before the mandatory requirements are complete.
> 3. Core product
> Build a desktop-first Sales Performance Dashboard.
> Required stack:
> Backend:
>
> * C#
> * .NET 8+
> * ASP.NET Core
> * EF Core
> * REST API
> * async/await
>
> Frontend:
>
> * React
> * TypeScript
>
> Database:
>
> * PostgreSQL
> * EF Core migrations
>
> Infrastructure:
>
> * Docker
> * Docker Compose
>
> The application must start with:
> docker compose up --build
> After startup, migrations and seed data must be applied automatically and the browser must show a populated dashboard.
> No authentication is required.
> 4. Required domain model
> Implement:
> Manager
>
> * id
> * name
> * team/position
> * active status
> * avatar/initials data
>
> Customer
>
> * id
> * name
> * company
> * segment
>
> Category
>
> * id
> * name
>
> Product
>
> * id
> * name
> * category
> * appropriate product attributes
>
> Sale
>
> * id
> * manager
> * customer
> * date
> * status
>
> SaleItem
>
> * id
> * sale
> * product
> * quantity
> * sale price
> * cost
>
> Sale statuses:
>
> * Paid
> * Cancelled
> * Refunded
>
> Do not over-model the domain.
> 5. Business rules
> We need:
> Revenue
> = sum of paid sales
> Gross Profit
> = Revenue - Cost
> Margin
> = Gross Profit / Revenue
> Average Check
> = Revenue / number of sales
> The assignment explicitly leaves the exact interpretation of Refunded open.
> Choose one consistent business rule.
> Document the decision clearly in README.md.
> My preference is to define a clear financial interpretation rather than silently treating Refunded as Paid.
> For example, consider whether a refunded sale should contribute zero net revenue/profit while remaining visible in operational sales data.
> Whatever rule you choose:
>
> * use it consistently in every API calculation;
> * use it consistently in rankings;
> * use it consistently in charts;
> * use it consistently in previous-period comparisons;
> * test it explicitly;
> * document it.
>
> Cancelled sales must not contribute to revenue/profit.
> Avoid duplicated business logic between frontend and backend.
> The backend must be the source of truth for calculations.
> 6. Seed data
> Automatically create realistic deterministic seed data.
> Target:
>
> * 15–25 managers
> * 50–100 customers
> * several categories
> * several dozen products
> * 2,000–5,000 sales
> * at least 6 months, preferably 12 months
>
> Seed data must be intentionally non-uniform.
> Include:
>
> * strong managers
> * weak managers
> * different average checks
> * different margins
> * seasonality
> * cancellations
> * refunds
> * very large transactions
> * many small transactions
> * managers with periods containing no sales
>
> Use a deterministic seed so recreating the database produces the same dataset.
> Do not generate random data on every application startup.
> 7. Dashboard
> The target viewport is approximately 1440x900.
> Build a polished modern B2B/SaaS analytics dashboard.
> Mandatory:
> KPI cards
>
> * Revenue
> * Gross Profit
> * Margin
> * Number of Sales
> * Average Check
> * Best Manager
>
> Where practical, show comparison with the previous comparable period.
> Date filtering
> Support:
>
> * Today
> * Last 7 days
> * Last 30 days
> * This month
> * Previous month
> * Custom from → to
>
> Date filtering must be performed server-side.
> Be precise about inclusive/exclusive date boundaries.
> Manager ranking
> At minimum:
>
> * Gross Profit
> * Average Check
>
> Each manager row should ideally show:
>
> * rank
> * manager
> * number of sales
> * revenue
> * gross profit
> * average check
> * margin
> * change versus previous period
>
> Time-series chart
> Show at least:
>
> * Revenue
> * Gross Profit
> * Sales count
>
> over time.
> Categories/products
> Show:
>
> * sales/profit by category
> * top products
>
> Recent sales
> Show:
>
> * date
> * manager
> * customer
> * products
> * status
> * amount
> * gross profit
>
> 8. Product/UX quality
> The visual quality is important.
> Prefer a modern professional B2B/FinTech/CRM visual language.
> Use a sensible component library if it materially improves the result.
> A reasonable stack could be:
>
> * React
> * TypeScript
> * TanStack Query
> * Tailwind
> * shadcn/ui
> * Recharts or another appropriate chart library
>
> Do not add libraries just because they are available.
> Prioritize:
>
> * hierarchy
> * readability
> * information density
> * spacing
> * typography
> * consistent components
> * professional empty states
> * useful hover states
> * responsive transitions inside the desktop viewport
>
> Animations should be subtle.
> Good examples:
>
> * KPI count animation
> * chart transitions
> * ranking reorder
> * hover states
> * skeleton loading
> * smooth period changes
>
> Avoid excessive animation.
> 9. Required UI states
> Every major async section should have meaningful states:
>
> * initial loading
> * loading while changing period
> * API error
> * empty period
> * empty individual block
>
> Never leave:
>
> * blank white screens
> * unexplained empty containers
> * infinite spinners
>
> 10. Backend architecture
> Keep architecture intentionally simple.
> Do NOT automatically introduce:
>
> * Clean Architecture
> * CQRS
> * MediatR
> * generic repositories
> * microservices
> * event sourcing
>
> unless a specific requirement justifies them.
> A reasonable structure might be:
> API
> Application/services
> Domain/models
> Infrastructure/data
> But choose the simplest structure that keeps responsibilities clear.
> The assignment explicitly says that an additional abstraction layer must solve a concrete problem.
> Prioritize:
>
> * clear API contracts
> * separation of concerns
> * testability
> * readable code
> * predictable dependency flow
>
> 11. API design
> Design clear REST endpoints.
> Potential shape:
> GET /api/dashboard/summary
> GET /api/dashboard/manager-ranking
> GET /api/dashboard/sales-trend
> GET /api/dashboard/categories
> GET /api/dashboard/products
> GET /api/dashboard/recent-sales
> The exact API structure is your decision.
> Avoid making one gigantic endpoint returning thousands of raw records.
> The frontend must receive already aggregated data.
> The backend owns:
>
> * filtering
> * aggregation
> * KPI calculations
> * ranking
> * period comparison
>
> Use DTOs rather than exposing EF entities directly.
> Handle:
>
> * invalid dates
> * invalid ranges
> * unexpected failures
>
> with consistent API error responses.
> 12. PostgreSQL
> Use EF Core migrations.
> Think explicitly about:
>
> * indexes
> * aggregate queries
> * date filtering
> * joins
> * grouping
> * generated SQL
> * query performance
>
> Do not optimize everything prematurely.
> But avoid obvious problems such as:
>
> * N+1 queries
> * loading all sales into memory
> * calculating the dashboard in C#
> over thousands of records
> * calculating dashboard metrics in React
>
> If a specific analytical query is better implemented with raw SQL/Dapper, that is allowed.
> Do not use Dapper just to demonstrate that we know Dapper.
> If using raw SQL, document why.
> 13. Previous-period comparison
> Implement a clear previous-period calculation.
> For a selected range:
> current = selected date range
> previous = immediately preceding period of equal duration
> Example:
> June 1–June 30
> → previous period May 2–May 31
> Be explicit about date boundary semantics.
> For "today", use an appropriate previous comparable day.
> For "this month", compare against the previous equivalent period according to a clearly documented rule.
> Avoid inconsistent comparison logic between metrics.
> The README must explain this.
> 14. Testing
> Do not chase coverage percentage.
> Test business-critical behavior.
> Backend tests should cover at minimum:
>
> * KPI calculations
> * period filtering
> * previous-period comparison
> * manager ranking
> * status handling
> * zero/empty data
> * refunded behavior
> * cancelled behavior
> * date boundary behavior
>
> Frontend tests should cover at minimum:
>
> * period switching
> * ranking mode switching
> * loading state
> * error state
>
> Use the testing stack that best fits the implementation.
> Keep tests meaningful.
> 15. Edge cases
> Explicitly account for:
>
> * Cancelled sales
> * Refunded sales
> * manager with no sales
> * equal manager results
> * sale exactly on date boundary
> * one huge sale
> * many tiny sales
> * different costs/margins
> * zero revenue
> * zero sales
> * empty custom period
>
> Margin must never produce NaN/Infinity in the UI or API.
> 16. Docker
> This is critical.
> The evaluator must be able to clone the repository and run:
> docker compose up --build
> No manual:
>
> * database creation
> * SQL scripts
> * PgAdmin
> * npm install
> * migration command
> * seed command
>
> should be required.
> Containers:
>
> * frontend
> * backend
> * PostgreSQL
>
> The application must automatically:
>
> 1. start PostgreSQL
> 2. wait until it is available
> 3. apply migrations
> 4. seed deterministic data
> 5. start API
> 6. make frontend available
>
> Do not rely on fragile startup ordering.
> Implement health checks or a robust retry strategy where appropriate.
> 17. AI workflow
> This is an explicit part of the evaluation.
> They want to see how I work with AI.
> Use AI heavily, but preserve engineering ownership.
> The workflow should resemble:
>
> 1. Survey / analysis
> 2. Architecture / planning
> 3. Implementation
> 4. Verification
> 5. Independent review
> 6. Fixes
> 7. Final validation
>
> Do not let one huge prompt generate the entire project without review.
> Use the existing WM/Aperture AI workflow as inspiration.
> However, this project is smaller, so keep the workflow practical.
> 18. AI_PROMPTS.md
> Create this file immediately.
> Every user prompt/instruction sent to AI during this project must be recorded.
> Do NOT fabricate prompts after the fact.
> Do NOT include:
>
> * hidden chain-of-thought
> * provider system messages
> * tool traces
> * API keys
> * credentials
> * secrets
>
> The log should contain the actual prompts I send.
> Example:
> 14:32 — Claude Code / Opus
> [actual user prompt]
> Keep adding entries throughout the project.
> IMPORTANT:
> Whenever I give you a substantial implementation instruction, update AI_PROMPTS.md with the exact user-facing instruction before proceeding, if possible.
> 19. AI_NOTES.md
> Create it early and maintain it.
> At the end it should contain 10–30 meaningful lines explaining:
>
> * models/tools used
> * what AI implemented
> * what I designed myself
> * where AI accelerated development
> * where AI made mistakes
> * which AI suggestion I rejected or changed
> * how generated code was verified
>
> Do not invent an AI mistake purely for the document.
> If you encounter a genuine mistake, record it.
> 20. README.md
> README must include:
> Overview
> What the application does.
> Stack
> Frontend/backend/database/infrastructure.
> Running
> Exact:
> docker compose up --build
> Business rules
> Explicitly document:
>
> * Revenue
> * Gross Profit
> * Margin
> * Average Check
> * Cancelled
> * Refunded
> * previous-period comparison
>
> Architecture
> Explain the chosen structure briefly.
> API
> List the important endpoints.
> Database
> Explain the model and important indexes.
> AI workflow
> Briefly explain how AI was used.
> Trade-offs
> Explain important engineering decisions.
> What was not completed
> Be honest.
> What I would improve in production
> Examples:
>
> * pagination
> * caching
> * observability
> * richer filtering
> * authorization
> * materialized analytical views
> * background aggregation
> * deployment architecture
>
> Only mention improvements that genuinely make sense.
> 21. Git history
> Do not make one giant commit.
> Use meaningful milestones such as:
>
> 1. initial project structure
> 2. add domain model and migrations
> 3. add deterministic seed
> 4. implement analytics API
> 5. add dashboard UI
> 6. add charts/ranking
> 7. add loading/error/empty states
> 8. add tests
> 9. dockerize and validate
> 10. polish/review
>
> Do not create fake commits just to inflate history.
> 22. 8-hour priority strategy
> Priority order:
> P0 — Must work
>
> * Docker startup
> * PostgreSQL
> * migrations
> * deterministic seed
> * backend API
> * correct calculations
> * period filtering
> * dashboard
> * manager ranking
> * chart
> * recent sales
> * basic tests
>
> P1 — High value
>
> * previous-period comparison
> * polished UX
> * loading/error/empty states
> * animations
> * useful category/product analytics
> * strong seed distribution
>
> P2 — Nice to have
>
> * additional analytics
> * advanced interactions
> * extra visualizations
> * deeper optimization
>
> Never sacrifice P0 for P2.
> If time becomes limited, stop adding features and make the existing system reliable.
> 23. Engineering principles
> Throughout implementation:
>
> * prefer simple solutions
> * avoid premature abstraction
> * keep business logic explicit
> * keep calculations server-side
> * avoid N+1
> * use async I/O
> * validate inputs
> * make edge cases explicit
> * write tests around business invariants
> * keep frontend state predictable
> * don't duplicate backend calculations in React
> * don't over-engineer
>
> Most importantly:
> Do not write code just because the assignment mentions a technology or pattern.
> Every architectural decision should have a reason.
> 24. Review phase
> After the implementation appears complete, stop and perform an independent review.
> Review the project as if you were the DJI-Market technical interviewer.
> Check:
> Requirements
> Did we satisfy every mandatory requirement?
> Backend
>
> * API correctness
> * async
> * validation
> * error handling
> * query efficiency
> * N+1
> * DTOs
> * business logic
>
> PostgreSQL
>
> * schema
> * indexes
> * migrations
> * aggregates
> * date filtering
>
> Frontend
>
> * visual quality
> * UX
> * loading
> * errors
> * empty states
> * ranking
> * charts
> * date selection
>
> Business correctness
>
> * Paid
> * Cancelled
> * Refunded
> * margin
> * average check
> * previous period
> * date boundaries
>
> Docker
> Run the exact evaluator flow:
> docker compose up --build
> Then verify from a clean environment as much as possible.
> Tests
> Run all tests.
> AI
> Verify AI_PROMPTS.md and AI_NOTES.md are honest and useful.
> README
> Verify a stranger can understand and run the project.
> Then fix all high-impact issues you find.
> 25. Final requirement
> Do not tell me "looks good" without actually verifying it.
> The final project should be something I can confidently submit and defend in a technical interview.
> If you encounter ambiguity, first choose the most reasonable engineering interpretation based on the assignment and document it.
> If there is a conflict between an existing WM/Aperture pattern and the DJI-Market requirements, DJI-Market requirements win.
> Start with:
>
> 1. inspect the assignment
> 2. inspect WM
> 3. inspect Aperture
> 4. produce the architecture/implementation plan
> 5. create AI_PROMPTS.md
> 6. wait for my confirmation before beginning large-scale implementation

---

## 2026-09-02 08:14 — Claude Code / Opus 4.8

_(Delivered as my reply to the agent's plan-confirmation question. Verbatim.)_

> Proceed, but revise the architecture plan before implementation.
>
> I agree with the simplicity and 8-hour timebox, but I want to make one important adjustment.
>
> Do NOT turn this into a flat CRUD application just because the assignment is small.
>
> Use a lightweight **modular monolith**.
>
> The goal is still one deployable API, one PostgreSQL database and one frontend. No microservices, no distributed deployment, no Clean Architecture ceremony, no CQRS framework, no generic repositories.
>
> However, establish explicit domain/module boundaries:
>
> Sales — Sale, SaleItem, sale status and sales-related business rules
> Catalog — Product, Category
> People/CRM — Manager, Customer
> Analytics — dashboard queries, aggregations, rankings and reporting projections
>
> Keep module internals lightweight. Do not create abstractions unless they solve a real problem.
>
> The important point is that Analytics should depend on stable data/contracts from the domain rather than becoming a giant collection of unrelated queries in the API layer.
>
> Also reconsider SignalR.
>
> I want to include a small but genuine real-time feature because the product is a sales dashboard. For example:
>
> Sale/event → application/domain event → dashboard notification → SignalR → React update.
>
> Keep this intentionally simple. Do not introduce RabbitMQ or distributed messaging just for the sake of demonstrating technology.
>
> RabbitMQ may be considered later as an optional enhancement if there is a genuine asynchronous use case and enough time remains.
>
> Likewise, do NOT introduce an Outbox unless there is a real distributed message boundary that requires it.
>
> The architectural principle should be:
>
> "Use the simplest architecture that preserves good boundaries and leaves a credible path for evolution."
>
> ## MCP / AI workflow
>
> I also want the AI workflow to demonstrate something closer to a real AI-native engineering process.
>
> If available, use MCP tools where they provide genuine verification value rather than novelty.
>
> Priority:
>
> 1. PostgreSQL/database MCP — inspect schema/data and validate analytical queries/results.
> 2. Browser/Playwright MCP — run the application, inspect the dashboard, interact with filters/states and verify the UI.
>
> Do not create a custom MCP server unless there is a clear reason.
>
> The AI workflow should become:
>
> requirements → survey → architecture → implementation → database verification → browser verification → independent review → fixes → final validation.
>
> Do not treat generated code as correct simply because it compiles or tests pass.
>
> Update docs/PLAN.md with these architectural decisions and explain the trade-offs.
>
> Before implementation, show me the revised module structure and the final list of technologies/components you intend to use, separated into:
>
> Required
> Justified additions
> Optional if time remains
> Explicitly rejected as overengineering
>
> Then stop for confirmation.

---

## 2026-09-02 08:20 — Claude Code / Opus 4.8

_(This instruction, verbatim — it is itself part of the evaluated AI workflow.)_

> I noticed that AI_PROMPTS.md currently summarizes my first prompt instead of preserving the actual prompt.
> Please fix this.
> For the first entry, replace the summary with the complete exact user-facing prompt I originally sent you, preserving its wording and structure as closely as possible.
> Do the same for the second entry: replace the summarized version with the complete actual user-facing instruction I sent when I asked you to revise the architecture.
> The purpose of AI_PROMPTS.md is to document the real AI-assisted development process, so I want the file to contain the actual prompts rather than retrospective summaries.
> Important:
>
> * Do not invent or reconstruct wording that you cannot verify.
> * If you cannot access the exact original prompt from the conversation/context, explicitly tell me rather than fabricating it.
> * Keep timestamps and model/tool information.
> * Continue appending future prompts as their actual user-facing text, not summaries.
> * Keep excluding hidden reasoning, tool traces, system prompts, credentials, and secrets.
>
> After updating the file, show me the resulting first two entries.
>
> Before continuing large-scale implementation, I want to formalize the AI-agent workflow for this project.
> This is not for adding ceremony or creating many agents. The DJI-Market assignment explicitly evaluates how I decompose work with AI, what context I provide, and how I independently verify generated code.
> Inspect the agent/skill conventions already used in my Aperture and WM projects and reuse the good ideas, but simplify them for this 8-hour take-home.
> Create a small `.claude` agent/skill structure appropriate for this project.
> I expect approximately:
> `.claude/agents/`
>
> * `survey-agent.md`
> * `implementation-agent.md`
> * `review-agent.md`
> * `ui-review-agent.md`
>
> `.claude/skills/`
>
> * `sales-domain/SKILL.md`
> * `analytics-verification/SKILL.md`
> * `docker-verification/SKILL.md`
> * `frontend-quality/SKILL.md`
>
> Adjust names or grouping if the existing Aperture/WM conventions suggest something materially better, but do not create agents/skills without a concrete responsibility.
> Agent responsibilities
> Survey agent
> Read-only investigation.
> Responsible for:
>
> * requirements analysis
> * codebase exploration
> * tracing dependencies
> * identifying affected modules
> * identifying ambiguities and risks
> * proposing implementation approaches
>
> It must NOT modify production code.
> Its output should be a concise implementation brief for the implementation agent.
> Implementation agent
> Implements an already-approved bounded task.
> Responsible for:
>
> * following the approved architecture
> * keeping changes within the intended module boundaries
> * writing/updating relevant tests
> * running focused verification
> * reporting assumptions and deviations
>
> It must not redesign the entire system while implementing a local task.
> Review agent
> Independent/adversarial review.
> Treat the implementation as potentially wrong even when it compiles and tests are green.
> Review specifically for:
>
> * requirement drift
> * business-rule correctness
> * Paid / Cancelled / Refunded semantics
> * date boundary correctness
> * previous-period correctness
> * ranking and tie behavior
> * zero/empty edge cases
> * SQL aggregation correctness
> * N+1/query inefficiency
> * inappropriate in-memory aggregation
> * duplicated business logic in React
> * module-boundary violations
> * error handling
> * missing meaningful tests
> * Docker/startup fragility
> * SignalR correctness
>
> Do NOT immediately rewrite implementation code.
> First produce findings ranked by severity and explain why each finding matters. Fixes should happen only after findings are reviewed.
> UI review agent
> Review the actually running application using the available browser tooling rather than judging the UI only from source code.
> Verify:
>
> * target 1440x900 viewport
> * visual hierarchy
> * KPI readability
> * charts
> * manager ranking
> * date filters
> * recent sales
> * loading states
> * refetch/loading transitions
> * API error states
> * empty-period states
> * block-level empty states
> * SignalR-driven refresh
> * console errors
> * failed network requests
> * obvious layout/overflow problems
>
> Prefer evidence from the running application.
> Skills
> Skills are reusable knowledge/procedures, not personas.
> sales-domain
> Capture the authoritative business rules and terminology for this take-home.
> At minimum:
>
> * Paid contributes to financial aggregates and paid-sale count.
> * Cancelled contributes nothing to financial aggregates.
> * Refunded has zero net financial contribution under our chosen rule but remains visible operationally.
> * Revenue
> * Cost
> * Gross Profit
> * Margin
> * Average Check
> * manager ranking semantics
> * previous-period semantics
> * half-open `[start,end)` date windows
> * zero-denominator behavior
>
> This skill should prevent different agents from inventing different interpretations.
> analytics-verification
> Define an independent verification procedure.
> The important principle is:
> API analytical result must be cross-checked against independent PostgreSQL ground truth where practical.
> Use `psql` against the running PostgreSQL container.
> Include checks for:
>
> * summary revenue
> * gross profit
> * sale count
> * average check
> * margin
> * manager ranking
> * current-period boundaries
> * previous-period boundaries
> * status handling
>
> Do not simply re-run the same application implementation and call that verification.
> docker-verification
> Define the evaluator-equivalent clean-start procedure.
> The critical path is:
> `docker compose up --build`
> Verify:
>
> * PostgreSQL becomes healthy
> * API waits correctly
> * migrations apply automatically
> * deterministic seed runs automatically
> * readiness becomes healthy
> * frontend starts
> * `/api` proxy works
> * `/hubs` SignalR proxy works
> * populated dashboard loads without manual commands
>
> Include clean-volume/rebuild verification where appropriate.
> frontend-quality
> Define practical dashboard-quality criteria for this assignment:
>
> * 1440x900 desktop target
> * B2B/SaaS visual hierarchy
> * information density without clutter
> * consistent spacing/typography
> * useful skeletons
> * loading/refetch distinction
> * error feedback
> * empty states
> * subtle motion only
> * chart readability
> * table/ranking readability
> * no unexplained blank regions
> * no NaN/Infinity rendering
>
> Do not turn this into a generic frontend style guide.
> Important constraints
> Keep these files concise and operational.
> Do not duplicate the entire assignment into every agent.
> Agents should reference skills where appropriate rather than each embedding its own slightly different copy of the rules.
> Do not add:
>
> * architecture agent
> * database agent
> * testing agent
> * Docker agent
> * seed agent
> * documentation agent
>
> unless you can demonstrate that a separate agent provides a real independent responsibility rather than unnecessary orchestration.
> Also update `docs/PLAN.md` to describe this AI workflow briefly.
> Because this instruction is itself part of the evaluated AI workflow, append this exact user instruction to `AI_PROMPTS.md`, not a summary.
> After creating the agent/skill files, show me:
>
> 1. the resulting `.claude` tree,
> 2. a one-line responsibility for each agent,
> 3. a one-line purpose for each skill,
> 4. any differences you deliberately made from Aperture/WM and why.
>
> Then continue implementation under the approved plan.

---

## 2026-09-02 08:50 — Claude Code / Opus 4.8

_(Final architecture correction, from an independent review. Verbatim — it is itself part of the
evaluated AI workflow.)_

> After my once again independent review of the approved plan, since plan usage already is depleted I found several substantive issues.
> Before continuing implementation, update the plan and current scaffold according to the decisions below.
> This is the final architecture correction. After this, freeze architecture and move to implementation.
> 1. Freeze the existing modular structure
> Keep the existing lightweight modular-monolith project scaffold because it already exists and changing it now has poor ROI.
> However, do NOT add further architectural layers/projects.
> Keep the single aggregated WriteDbContext and single migration history.
> Restore full relational integrity across schemas with PostgreSQL foreign keys:
>
> * Product → Category
> * Sale → Manager
> * Sale → Customer
> * SaleItem → Sale
> * SaleItem → Product
>
> Use sensible restrictive delete behavior.
> Add only genuinely useful database constraints such as positive quantity and non-negative monetary values.
> 2. Shrink Contracts
> Do not let SalesDashboard.Contracts become a shared junk drawer.
> It should contain only genuinely stable cross-module/API vocabulary such as:
>
> * SaleStatus where genuinely shared
> * dashboard/request/response contracts
> * small shared value types if needed
>
> Do not place infrastructure plumbing there.
> Move IDbConnectionFactory to an appropriate infrastructure/analytics location.
> Because SignalR is being removed from core scope, remove INotifier/event-notification abstractions unless another genuine use remains.
> 3. Remove SignalR from core scope
> I identified that SignalR currently depends on an invented sample-sale mutation path rather than a real requirement.
> Remove from the core implementation:
>
> * DashboardHub
> * sample-sale demo endpoint/button
> * notifier/event dispatcher created solely for SignalR
> * websocket/nginx complexity
> * realtime query invalidation
>
> Document SignalR as a considered but deliberately rejected enhancement for this take-home.
> Reason:
> The assignment rewards engineering judgment. Adding a realtime transport without a genuine event source introduces failure surfaces without improving mandatory functionality.
> Only reconsider SignalR after every mandatory requirement is complete, tested, Docker-verified and documented with substantial time remaining.
> 4. Freeze authoritative business semantics
> Resolve all duplicate/conflicting rules and make sales-domain/SKILL.md the single authoritative business-rule source referenced by other agents/docs.
> Use:
> Financial contribution
> Paid:
>
> * contributes revenue
> * contributes cost
> * contributes gross profit
> * contributes paid-sale count
>
> Cancelled:
>
> * zero financial contribution
> * excluded from paid-sale count
> * remains operationally visible
>
> Refunded:
>
> * zero net financial contribution
> * excluded from paid-sale count
> * remains operationally visible
>
> Explicitly document that this is a simplified current-state interpretation: without refund timestamps/ledger entries, a refund removes the sale from financial aggregates of its original sale period rather than creating a later-period reversal.
> Ratios
> Revenue = additive, default 0
> Cost = additive, default 0
> Gross Profit = Revenue - Cost
> Paid Sales = integer count, default 0
> Margin:
>
> * null when Revenue == 0
> * otherwise GrossProfit / Revenue
>
> Average Check:
>
> * null when PaidSales == 0
> * otherwise Revenue / PaidSales
>
> Percentage delta:
>
> * null when previous baseline is zero
> * UI displays an appropriate neutral value such as "New" or "—"
> * never Infinity/NaN
>
> Use "Paid Sales" in UI/API when referring to the denominator rather than ambiguous "Sales".
> 5. Define presentation semantics
> Choose and document:
>
> * currency: one fixed demo currency across the dataset
> * monetary rounding/formatting
> * percentage precision
> * margin delta in percentage points where appropriate
> * Best Manager = null when no manager has Paid sales
> * tie ordering must be deterministic
> * inactive managers with historical Paid sales remain eligible for historical rankings, unless there is a strong reason otherwise
>
> For Recent Sales, show original transaction amount/cost/profit fields clearly enough that Refunded/Cancelled rows do not appear to reconcile directly with net financial KPI totals. Status must make the distinction obvious.
> 6. Fix period semantics
> Continue using half-open server-side intervals:
> [start, end)
> Custom UI date ranges are user-friendly inclusive calendar dates but must be resolved server-side into half-open instants.
> Define one reporting timezone explicitly.
> Use date-only inputs for custom ranges.
> Use semantic preset comparison rather than forcing one arithmetic rule onto every preset:
>
> * Today → previous calendar day
> * Last 7 days → immediately preceding 7 days
> * Last 30 days → immediately preceding 30 days
> * This Month → month-to-date, compared with the same elapsed portion of the previous month
> * Previous Month → full previous calendar month, compared with the calendar month before it
> * Custom → immediately preceding equal-duration range
>
> Echo the resolved current and previous windows in the response.
> Use an injectable clock/time provider so date logic is testable.
> 7. Establish one-sale analytical grain
> This is a critical invariant.
> Never count joined SaleItem rows as sales.
> Analytical SQL must conceptually produce one financial row per Sale first:
> Sale
> → aggregate SaleItems into Sale revenue/cost
> → apply Sale status semantics
> → aggregate those sale-level rows into dashboard metrics
> Add a focused integration test containing a Paid sale with multiple SaleItems to prove Paid Sales and Average Check are not inflated by item count.
> 8. Compose the dashboard response
> Replace the six independent core dashboard queries with one bounded analytical snapshot endpoint:
> GET /api/dashboard
> It accepts the selected preset/custom period and returns one coherent response containing:
>
> * resolved current/previous periods
> * summary KPIs
> * manager rankings / required ranking data
> * trend
> * categories
> * top products
> * limited recent sales
>
> This is NOT a raw-record mega endpoint. It represents one cohesive dashboard view.
> Prefer resolving the period once per request.
> If practical, execute analytical reads using one database connection/consistent snapshot.
> Keep additional endpoints only when there is a concrete interaction that benefits from independent retrieval.
> 9. Dapper selectively, not ideologically
> Do not require every Analytics query to use Dapper.
> Choose per query.
> Use Dapper/raw PostgreSQL where explicit SQL materially improves analytical clarity, such as:
>
> * grouped financial aggregation
> * time bucketing
> * window/ranking operations
>
> Use EF Core where projection is simpler and equally clear.
> Do not introduce SQL views just to justify module decoupling.
> Acknowledge honestly in README that Analytics SQL is coupled to the database schema even though it is not coupled to EF entity types.
> 10. Seed correctness
> The seed must be deterministic and recover safely.
> Use:
>
> * fixed RNG seed
> * one captured seed anchor date
> * transactional seed execution
> * an explicit seed/version marker or equally robust idempotency mechanism
>
> Readiness must remain false until migrations and seed complete successfully.
> Running seed repeatedly must not duplicate data.
> 11. Docker becomes an early milestone
> Do not leave Docker until the final hour.
> As soon as the minimal DB/API/web vertical slice exists, verify:
> docker compose up --build
> from a clean state.
> Later repeat with clean volumes before final submission.
> Do not depend on pg_isready alone for application readiness; API readiness must reflect completed migration + seed.
> 12. Keep Testcontainers, but focused
> Use one shared PostgreSQL Testcontainer fixture.
> Build a small handcrafted analytical dataset containing:
>
> * a multi-item Paid sale
> * a Cancelled sale
> * a Refunded sale
> * a manager with no sales
> * exact start boundary
> * exact end boundary
> * ranking tie
> * zero-revenue period
>
> Highest-value assertions:
>
> * sale count is not inflated by SaleItems
> * summary financial calculations
> * status treatment
> * date boundaries
> * previous period
> * ranking/tie behavior
> * trend/category/product totals reconcile with summary where applicable
>
> Do not chase broad coverage.
> 13. Frontend simplification
> Keep:
>
> * React 19
> * TypeScript
> * Vite
> * TanStack Query
> * Tailwind
> * Recharts
>
> Remove Framer Motion from core scope.
> Use CSS/Tailwind transitions only where useful.
> The polished minimum dashboard is:
>
> * header + period controls
> * six KPI cards
> * one clear trend chart
> * manager ranking switch
> * categories
> * top products
> * recent-sales table
> * initial skeleton
> * retained previous snapshot during refetch with a clear Updating state
> * error + Retry
> * whole-period empty state
> * block-empty states
> * stable currency/percentage formatting
> * keyboard/focus basics and non-color-only status indication
>
> Do not add extra charts before this is excellent.
> 14. AI-agent workflow
> Keep AI workflow evidence, but avoid performative agent-file ceremony.
> Agents should exist only if they are actually used.
> Minimum meaningful roles:
>
> * survey
> * implementation
> * independent review
> * UI/browser review when needed
>
> Keep sales-domain and analytics-verification as authoritative/useful skills.
> Other skills may be added only when they are genuinely consumed in the workflow.
> Independent review exposed real issues. Record these honestly in AI_NOTES later, including:
>
> * conflicting null/zero ratio semantics
> * the multi-SaleItem counting risk
> * date-period ambiguity
> * SignalR being rejected after review
> * dashboard endpoint simplification
>
> Do not fabricate additional AI mistakes.
> 15. Revised priority
> P0:
>
> * frozen business/date contracts
> * relational schema integrity
> * deterministic transactional seed
> * early clean Docker vertical slice
> * coherent dashboard API
> * correct PostgreSQL analytics
> * required React dashboard
> * required states
> * focused tests
> * README / AI docs
> * final clean Docker verification
>
> P1:
>
> * visual polish
> * previous-period deltas
> * richer category/product presentation
>
> P2 only if substantial time remains:
>
> * additional analytics
> * Playwright
> * SignalR
>
> After updating docs/PLAN.md and relevant current scaffold to reflect these decisions, show me only:
>
> 1. the final architecture tree,
> 2. the authoritative business/date semantics,
> 3. the revised API contract shape,
> 4. which Codex findings were accepted/rejected and why.
>
> Then proceed with implementation without another architecture redesign.
> 16. Preserve remaining mandatory assignment requirements
> Everything not explicitly changed by this correction remains governed by the original DJI-Market specification and the previously approved plan.
> In particular:
>
> * Store Sale timestamps as UTC using PostgreSQL `timestamptz`. Resolve calendar periods in the configured reporting/business timezone and convert the resulting half-open boundaries to UTC for querying.
> * Preserve the required deterministic seed scale: 15–25 managers, 50–100 customers, several categories, several dozen products, 2,000–5,000 sales, approximately 12 months, with the required non-uniform distributions and edge cases.
> * Manager ranking must support both mandatory modes: Gross Profit and Average Check.
> * Trend data must include Revenue, Gross Profit and Paid Sales count over time. Missing time buckets should be represented consistently so charts do not silently imply missing data.
> * Recent Sales must still expose the required date, manager, customer, products, status, amount and gross profit information.
> * All mandatory frontend states from the assignment remain required: initial loading, loading/refetch on period change, API error, whole-period empty state and block-level empty state.
> * Required frontend tests remain in scope: period switching, ranking-mode switching and loading/error behavior.
> * README and Git-history requirements from the assignment remain mandatory.
> * Append this exact correction instruction and every future substantial user instruction to `AI_PROMPTS.md` verbatim. Do not replace them with summaries.
>
> No other requirements from the original assignment are relaxed unless explicitly stated above.

---

## 2026-09-02 09:01 — Claude Code / Opus 4.8

_(Final consistency corrections. Verbatim — part of the evaluated AI workflow.)_

> After one final consistency review. I have three remaining blocking corrections.
> Apply ONLY these corrections, fix the identified stale scaffold/docs while implementing, and then freeze architecture permanently. Do not start another design cycle.
> 1. Simplify Analytics dependencies
> The current description is internally inconsistent: Analytics cannot simultaneously reference Contracts only, use EF entity projections, and depend on an infrastructure connection abstraction.
> Use the simpler explicit model:
>
> * EF Core remains the write/migration/seed technology.
> * Analytics uses Dapper/Npgsql for analytical reads where appropriate.
> * Do not introduce another abstraction solely to preserve a reference diagram.
> * Analytics may depend directly on the appropriate Npgsql/Dapper primitives required to execute analytical queries.
> * Continue returning API/dashboard DTOs rather than exposing persistence entities.
>
> Be honest in README that Dapper analytics are coupled to the PostgreSQL schema.
> Do not add SQL views or adapter projects merely to make this dependency graph look purer.
> 2. Ranking remains server-owned
> The frontend must NOT recalculate ranking order or tie-breaking.
> The dashboard response should return two already-ranked server-side collections:
>
> * `rankings.grossProfit`
> * `rankings.averageCheck`
>
> Each collection contains its own correct `rank`.
> Use deterministic server-side tie-breaking and document it.
> React only switches which pre-ranked collection is displayed.
> 3. Freeze exact period semantics
> Capture `now` exactly once per dashboard request using TimeProvider.
> Store Sale timestamps as UTC/PostgreSQL `timestamptz`.
> Use one documented reporting timezone: MSK / UTC+03:00 for this demo.
> Resolve calendar boundaries in that reporting timezone and convert them to UTC for SQL filtering.
> Use these preset semantics:
> Today
> Current:
> [start of current reporting day, captured now)
> Previous:
> [start of previous reporting day, same elapsed local time on that previous day)
> Last 7 Days
> Seven reporting-calendar dates including today.
> Current:
> [start of the day six calendar days before today, captured now)
> Previous:
> the directly preceding comparable interval with the same elapsed-clock boundary.
> Last 30 Days
> Same semantics as Last 7 Days, over thirty reporting-calendar dates including today.
> This Month
> Current:
> [start of current reporting month, captured now)
> Previous:
> [start of previous month, same elapsed calendar position/time where possible), capped at the end of the previous month when the previous month is shorter.
> Add explicit tests for the February/March shorter-month case.
> Previous Month
> Current:
> [previous calendar month start, current calendar month start)
> Previous:
> [calendar month before that start, previous calendar month start)
> Custom
> Frontend accepts inclusive date-only `from` and `to`.
> Backend converts them to a half-open reporting-calendar interval:
> [from 00:00, day-after-to 00:00)
> Previous:
> the immediately preceding interval of equal duration.
> Echo resolved current and previous UTC boundaries in the API response.
> Add concrete tests for all presets and exact start/end boundaries.
> Do not require REPEATABLE READ as an architectural rule. The take-home dataset is effectively static during normal dashboard evaluation; avoid transaction/isolation complexity unless the actual implementation proves it necessary.
> 4. Summary comparison priority
> Previous-period summary KPI comparisons are P0 because the previous-period calculations already exist and provide high product value.
> Per-manager historical deltas remain P1 if time becomes constrained.
> 5. Correct current stale scaffold/docs during implementation
> Ensure:
>
> * migration/schema gets the approved cross-schema foreign keys and check constraints;
> * AI_NOTES no longer mentions triggering a live sale after SignalR removal;
> * analytics-verification does not require a limited top-N product list to equal the full dashboard summary total;
> * documentation does not claim Git is being initialized if it already exists; Use 00008550 account with email davron.yusupov.2000@gmail.com
> * agent/skill usage is described only when actually used, not promised in advance.
>
> Append this exact instruction to AI_PROMPTS.md verbatim.
> After these corrections, proceed directly to implementation under the frozen plan. Do not ask for another architecture confirmation unless implementation exposes a genuine contradiction that makes the current plan impossible.

---

## 2026-09-02 09:29 — Claude Code / Opus 4.8

_(Consistency fixes + go-ahead for the seed/startup milestone. Verbatim.)_

> Milestone 2 is confirmed: the project graph is sound, and InitialSchema contains all five RESTRICT foreign keys, all five CHECK constraints, and `ops.seed_state`.
> Before continuing, apply only these remaining consistency fixes:
>
> 1. Remove the EF Core projection option from `docs/PLAN.md` and `implementation-agent.md`. EF Core is write/migration/seed only; Analytics uses Npgsql/Dapper directly and references Contracts for DTOs.
> 2. Clarify that only trend and categories reconcile with summary totals; limited top-N products do not.
> 3. Standardize the readiness endpoint everywhere as `/api/health/ready`.
> 4. Do not describe runtime wiring, seeding, tests, or agent/skill usage as completed until they actually exist. Update premature comments where necessary.
>
> Then continue immediately, without another architecture review:
>
> * implement the deterministic transactional seed;
> * check the seed marker inside the transaction and write it only after the complete seed succeeds;
> * wire startup as migration → seed → readiness;
> * build and verify the early Docker vertical slice;
> * add focused seed/startup tests.
>
> Do not redesign the architecture or stop for confirmation unless implementation reveals a genuinely impossible requirement. Append this instruction verbatim to `AI_PROMPTS.md`, run the relevant build/tests, and commit meaningful completed milestones.

---

## 2026-09-02 ~10:15 — Claude Code / Opus 4.8

_(Back-filled: this message was not logged when sent; time is approximate, from the surrounding commit history. Verbatim text.)_

> давай ебаш дальше, я создал гитхаб репо, продолжай по плану, и когда закончишь создай пул реквест, я буду сам ревьюить и либо аппрувать, либо комменты оставлять и дальше с этим будем работать, просто сейчас поехал у другу потому что у него бабушка скончалась, и не могу полностью погрузиться в процесс с рабочего ПК, а дедлайн все ещё дедлайн, надо все доработать и сделать

---

## 2026-09-02 ~13:50 — Claude Code / Opus 4.8

_(Back-filled: not logged when sent; time is approximate. Verbatim text.)_

> продолжи работу, сессия была приостановлена из-за исчерпания лимите

---

## 2026-09-02 16:37 — Claude Code / Opus 4.8

_(PR #1 review findings. Verbatim.)_

> after finally coming back to my pc I reviewed your code and I have some issues to surface
> First of all, my prompts, even if they are small, like "давай ебаш" or any other prompt, even this one that I am writing should be documented with time as it is done for other prompts already in AI_PROMPTS.
>
> Address the verified review findings on PR #1 only. Do not start another architecture cycle and do not add unrelated functionality.
>
> Preserve the current Git history. Do not amend, squash, rebase, reset, force-push, or replace already-published commits. Add focused corrective commits to the existing PR branch. Do not merge the PR.
>
> 1. Preserve migration lineage
>
> origin/master already contains 20260902041021_InitialSchema. The PR replaces it with 20260902061909_InitialSchema, which breaks databases initialized from master: EF attempts another initial migration and fails with PostgreSQL 42P07 because categories already exists.
>
> Restore/preserve the original committed migration identity and implement the required schema/column changes through an additive forward migration. Verify both:
>
> - a completely fresh database;
> - an upgrade from an origin/master database containing data and the original migration-history row.
>
> Do not solve this by requiring clean volumes.
>
> 2. Prevent future-dated seed records
>
> DeterministicSeeder currently starts from the captured anchor instant and then adds up to 23:59:59, creating records after the anchor when dayOffset is zero.
>
> Generate timestamps within the selected reporting-calendar day while ensuring every sale satisfies occurred_at <= captured seed anchor. Preserve the fixed RNG, scale, distributions, transaction, and idempotency marker.
>
> Add a regression assertion that max(occurred_at) <= seed anchor.
>
> 3. Correct half-open trend bucketing
>
> PostgreSQL generate_series includes its stop value. Do not return a bucket whose bucketStart equals period.current.end.
>
> Add explicit tests for:
>
> - a one-day custom range;
> - a complete 30/31-day custom range;
> - Previous Month;
> - exact expected bucket count;
> - first bucket;
> - last bucket strictly before current.end.
>
> Continue reconciling trend totals with summary totals.
>
> 4. Display the reporting timezone correctly
>
> The frontend currently formats dates with timeZone: 'UTC' while displaying “UTC+03:00 (MSK).”
>
> Format trend labels, tooltips, and Recent Sales timestamps in the documented reporting timezone. Verify that:
>
> - 2026-09-02T08:35:00Z displays as 11:35 MSK;
> - an MSK-midnight bucket represented as 21:00Z displays as the following MSK calendar date.
>
> Add focused formatter/component tests.
>
> 5. Make readiness truthful
>
> /api/health/ready currently checks a permanently latched boolean and remains 200 after PostgreSQL stops.
>
> Implement a lightweight readiness check proving:
>
> - PostgreSQL is reachable;
> - migrations/startup initialization completed;
> - the expected seed version exists.
>
> Keep liveness separate. Add a test covering dependency loss or otherwise directly verify the readiness probe against an unavailable database. Avoid new infrastructure layers.
>
> 6. Restore the frozen summary contract
>
> Add Cost to the dashboard summary with current, previous, and percentage delta values. This does not require adding a seventh KPI card unless the frozen UI explicitly requires one.
>
> Return margin delta in actual percentage-point units:
>
> marginDeltaPp = (currentMargin - previousMargin) * 100
>
> Update the DTO/property naming, frontend formatting, README, API types, and tests together. The frontend must not multiply a field already expressed in percentage points.
>
> 7. Fix accessibility findings
>
> At 1440x900:
>
> - remove the app-owned serious color-contrast failures;
> - make the internally scrolling manager-ranking list keyboard-focusable and visibly focused;
> - keep status distinctions non-color-only;
> - change ranking copy from “sales” to “Paid Sales”;
> - render/announce Updating only while updating;
> - associate invalid custom date inputs with their validation state/message.
>
> Do not expand into a mobile redesign; desktop remains the frozen target.
>
> 8. Complete small contract and documentation corrections
>
> - Reject mixed preset plus from/to requests with 400 ProblemDetails.
> - Remove /api/meta/counts from the public production surface, or provide a concrete frozen-plan justification. Tests can inspect the database directly.
> - Update AI_NOTES honestly with the final 16 backend tests, 6 frontend tests, analytics SQL reconciliation, Docker/web verification, browser verification, and the actual bugs caught. Remove the false “one consistent DB snapshot” claim unless an actual transaction provides it.
> - Record the Npgsql UTC and PK-casing bugs if the PR description continues claiming they are recorded.
> - Remove the unmatched trailing README code fence.
> - Ignore generated *.tsbuildinfo and leave the worktree clean.
> - Do not add CI, code splitting, SignalR, new projects, or other unrelated scope in this correction.
>
> Verification required before reporting completion:
>
> - clean build with zero errors;
> - all backend tests;
> - all frontend tests;
> - production frontend build;
> - fresh docker compose up --build;
> - upgrade test from origin/master database to PR HEAD with data preserved;
> - seed restart/idempotency;
> - independent PostgreSQL reconciliation of Revenue, Cost, Gross Profit and Paid Sales;
> - browser verification at 1440x900 for every preset, custom range, ranking switch, retained Updating state, Error/Retry and empty states;
> - accessibility rerun for the identified contrast and keyboard issues;
> - git diff --check;
> - clean git status.
>
> Append this exact instruction verbatim to AI_PROMPTS.md. Update AI_NOTES only with verification actually performed.
>
> Commit the fixes as new, focused commits on the current PR branch. Then stop and report:
>
> 1. commit hashes;
> 2. files changed by finding;
> 3. exact test/build/Docker/upgrade results;
> 4. any finding not fixed and the concrete reason.
>
> Do not declare the PR ready merely because existing tests pass.
