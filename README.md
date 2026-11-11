# Marites — React + C# pairing

This branch is the guide for combining the React frontend and C#/.NET backend. Application code belongs on the stack branches; shared product requirements and the API contract are maintained on `main`.

## Start here

| Workstream | Branch | GitHub Project |
|---|---|---|
| React frontend | [react](https://github.com/tengtium-tech/marites/tree/react) | [Marites — React Frontend](https://github.com/users/tengtium-tech/projects/6) |
| C#/.NET backend | [csharp](https://github.com/tengtium-tech/marites/tree/csharp) | [Marites — C# Backend](https://github.com/users/tengtium-tech/projects/7) |
| Shared requirements and API contract | [main](https://github.com/tengtium-tech/marites/tree/main) | Tracked in the relevant Project issue(s) |

The [OpenAPI contract](https://github.com/tengtium-tech/marites/blob/main/docs/07-data-and-contracts/openapi/openapi.yaml) defines the HTTP boundary the frontend and backend must share. See the [shared decisions and open questions](https://github.com/tengtium-tech/marites/blob/main/docs/01-project/decisions-and-open-questions.md) for decisions that affect the product across stacks.

## Where tasks and changes go

- Track React implementation tasks in the React Frontend Project and make their code changes on `react`.
- Track C#/.NET implementation tasks in the C# Backend Project and make their code changes on `csharp`.
- Track a cross-stack task as one repository issue and add that issue to both Projects when both teams need to see it. Split its implementation between `react` and `csharp` as needed; the issue remains the shared record.
- Keep shared, technology-neutral requirements and API contract changes on `main`. Make those changes through a pull request so both implementation branches can follow the same contract.
- Keep pairing instructions and the local integration guide on `react-csharp`. This branch does not hold the application source code.

For implementation work, branch from the matching stack branch and open a pull request back to it. Use `react` for frontend changes and `csharp` for backend changes.

## Projects, milestones, and sprints

Both Projects use a Kanban flow with these statuses: **Backlog → Ready → In Progress → In Review → Done**. Milestones M0–M7 organize delivery scope. The Sprint Backlog view groups work into two-week iterations that start on Monday; Sprint 1 starts November 16, 2026.

Issues currently start in Backlog. Move an issue into a sprint when the team commits to doing it in that iteration. A milestone describes the delivery goal; a sprint is the timebox for selected work.

## Recording open decisions

Discovery may raise questions that need a decision. Record each unresolved question in the relevant issue or a dedicated decision issue, including the context, viable options, impact, and who will resolve it.

- A React-only decision belongs with the React work and React Project.
- A C#-only decision belongs with the C# work and C# Project.
- A decision that affects both stacks or the shared API should be visible from both Projects. Keep the decision record and any resulting shared requirement or contract change on `main`.

When a decision is resolved, record the choice and reason, link the affected issues, and update the relevant specification or stack guidance. Keep superseded decisions traceable instead of silently rewriting an accepted shared decision.

## Clone the implementation branches

Use separate local directories so you can check out both branches at the same time:

```bash
git clone --branch react --single-branch https://github.com/tengtium-tech/marites.git marites-react
git clone --branch csharp --single-branch https://github.com/tengtium-tech/marites.git marites-csharp
```

Follow the setup, configuration, run, and verification instructions in each stack branch. Configure the React app to use the running backend's API address as specified by the frontend setup and shared contract. Verify the pairing against the shared API contract and the applicable acceptance criteria.

The branches are planning baselines until implementation code and stack-specific setup instructions are added. Update each stack branch's README with supported tool versions and exact setup, run, and verification commands as its implementation is built. Do not guess those commands here; they depend on the code and configuration in each branch.

## Adding other stack pairings

Future pairings can reuse the React frontend with another backend when that backend implements the same API contract and passes integration verification. Create a backend Project for each backend implementation. Add a separate pairing guide only when that combination needs distinct setup or integration instructions.
