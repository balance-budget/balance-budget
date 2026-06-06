# Contributing to Balance Budget

Thanks for your interest in Balance Budget.

## Scope: code pull requests only

This is a personal project shared as open source. To keep it maintainable:

- **Bug reports and feature requests are not accepted.** GitHub Issues are disabled, and there is no support channel.
- **Only pull requests that contain code are welcome.** If you want a change, build it and open a PR.

There is no roadmap and no guarantee that any PR will be reviewed or merged. PRs that are out of scope, or that I simply don't want to maintain, may be closed without detailed feedback. That's not personal — it's the cost of keeping a side project sustainable.

## Contributor License Agreement (CLA)

Before any pull request can be merged, you must sign the project's [Contributor License Agreement](CLA.md). This is automated: the **CLA Assistant** bot comments on your first PR with a one-click sign-off link (GitHub OAuth). You only sign once.

The CLA lets the project relicense contributions if needed; you retain copyright to your contribution. See [CLA.md](CLA.md) for the full text.

## How to contribute

1. **Fork** the repository and create a branch off `main`.
2. **Build and test locally** — see [docs/getting-started.md](docs/getting-started.md). In short: `dotnet tool restore && dotnet restore && npm install`, then `dotnet build` and `dotnet test`.
3. **Match the conventions** in [CLAUDE.md](CLAUDE.md) and [docs/conventions.md](docs/conventions.md). Run `dotnet csharpier format .` (C#) and `npm run lint` (SPA) before pushing — CI fails on formatting and lint deviations, and the build treats warnings as errors.
4. **Open a pull request** against `main`. CI must be green and the CLA signed.

## Commit messages: Conventional Commits

This repository uses [Conventional Commits](https://www.conventionalcommits.org/). Each commit subject is `type(scope): summary` in the imperative mood:

```
feat(reports): add money-flow Sankey export
fix(ing): handle savings-account rows starting with a non-D letter

Refs: #42
```

- **Types:** `feat`, `fix`, `docs`, `chore`, `refactor`, `style`, `test`, `ci`, `build`, `perf`, `revert`.
- **Scopes** (optional, lowercase) name the area, e.g. `reports`, `ing`, `client`/`spa`, `data`, `web`, `accounts`, `bank-accounts`, `bank-transactions`, `dashboard`, `sidebar`, `auth`, `settings`.
- Reference issues/PRs in a footer (`Refs: #NN`, `Closes #NN`), not the subject line.
- Breaking changes use `type(scope)!:` or a `BREAKING CHANGE:` footer.

## Built with AI

This project is developed with heavy use of AI agents (see [CLAUDE.md](CLAUDE.md) and [.sandcastle/](.sandcastle/)). You're welcome to use AI tools on your contributions too — but you are responsible for every line you submit: it must be correct, tested, and yours to license under the CLA.

## License

By contributing, you agree that your contributions are licensed under the project's [AGPL-3.0](LICENSE) license, subject to the terms of the [CLA](CLA.md).
