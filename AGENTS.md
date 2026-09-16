# Comvy — Agent Instructions

## Product

Comvy is an operational condominium management and communication platform.

The product prioritizes:
- operational clarity;
- communication flow;
- traceability;
- simple workflows;
- reliable behavior over unnecessary complexity.

Existing product behavior is authoritative unless the task explicitly changes it.

## Stack

Backend:
- .NET 10
- ASP.NET Core Minimal API
- EF Core
- PostgreSQL
- Identity + JWT

Frontend:
- React 19
- TypeScript
- Vite
- Material UI
- PWA

Infrastructure:
- Docker
- Coolify
- Hetzner
- PostgreSQL production database

## General implementation rules

Before changing behavior:
1. inspect the existing implementation;
2. identify current authorization and scope rules;
3. inspect relevant tests;
4. determine whether an existing abstraction/component already solves the problem.

Prefer the smallest coherent change.

Do not:
- perform unrelated refactors;
- introduce parallel architectures;
- duplicate existing abstractions;
- change API contracts unnecessarily;
- create migrations unless persistence actually changes;
- silently change existing behavior outside the requested scope.

When mockups/prompts conflict with working product behavior, preserve working behavior unless the task explicitly requires changing it.

## Authorization and condominium scope

Authorization must be enforced server-side.

Never rely on:
- hidden frontend controls;
- model prompts;
- client-provided scope;
- persisted references;
- route visibility

as authorization.

Always respect:
- current user;
- selected condominium;
- role;
- module permissions;
- entity scope.

IDs supplied by the client must never expand the user's authorized scope.

"Todos os condomínios" and a specific condominium are distinct contexts.
Modules that require one condominium must not silently operate across all condominiums.

SubManager permissions must be respected wherever applicable.

## Frontend

Use the existing design system and shared components before creating new ones.

Avoid arbitrary local styling when theme/shared primitives can express the same rule.

Preserve:
- loading states;
- error states;
- empty states;
- stale-request protection;
- responsive behavior;
- accessibility;
- mobile safe areas.

## Comvy UI 2.0

Visual direction:

> operational software that feels premium, human, and extremely clean.

Principles:
- hierarchy before cards;
- neutral surfaces;
- restrained use of Comvy blue;
- compact typography;
- comfortable operational density;
- subtle borders and dividers;
- coherent spacing and radii.

Avoid:
- gradients;
- glow;
- glassmorphism;
- decorative AI styling;
- excessive cards;
- oversized titles;
- excessive empty space;
- unnecessary pills;
- generic SaaS visual patterns.

Blue is primarily for:
- interaction;
- selection;
- focus;
- identity.

Semantic colors retain their semantic meaning.

## AI Assistant

The Assistant is:

> a natural-language interface to the current condominium.

It is not a generic chatbot.

Operational facts should come from authorized structured tools.

Examples:
- residents and units;
- requests;
- reminders;
- service providers;
- management company;
- management-company requests.

Document knowledge should come from RAG.

Examples:
- regulations;
- bylaws;
- meeting minutes;
- rules;
- decisions recorded in documents.

Hybrid questions may use both.

Never treat absence of documentary evidence as absence of an operational fact when an authorized structured source exists.

Operational references never grant authorization.

Do not expose:
- internal tool arguments;
- storage keys;
- chain of thought;
- infrastructure identifiers.

## Production safety

Production is real and must not be treated as disposable.

Avoid destructive operations.

Do not:
- reset production data;
- recreate databases;
- remove persistent volumes;
- alter production secrets;
- perform irreversible migrations without explicit need.

Changes involving authentication, authorization, condominium scope, WhatsApp, storage, migrations, or background workers require extra care.

## Tests

For every implementation:
- run focused tests for changed behavior;
- add regression tests for bugs;
- run relevant backend/frontend builds;
- run lint for changed frontend files;
- run `git diff --check`.

Do not fix unrelated pre-existing failures unless explicitly requested.

Report pre-existing failures separately.

Tests must validate backend authorization when security is involved; frontend hiding is not sufficient.

## Git

Do NOT commit automatically.

Do NOT push automatically.

Leave changes uncommitted and unpushed unless the user explicitly requests commit or push.

At completion report:
- what changed;
- why;
- files changed;
- tests executed and results;
- migrations created, if any;
- known limitations or pre-existing failures.