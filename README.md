# XtremeIdiots Portal - Repository Func
[![Build and Test](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/build-and-test.yml)
[![Code Quality](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/codequality.yml/badge.svg)](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/codequality.yml)
[![Copilot Setup Steps](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/copilot-setup-steps.yml/badge.svg)](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/copilot-setup-steps.yml)
[![Dependabot Auto-Merge](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/dependabot-automerge.yml/badge.svg)](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/dependabot-automerge.yml)
[![Deploy Dev](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/deploy-dev.yml/badge.svg)](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/deploy-dev.yml)
[![Deploy Prd](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/deploy-prd.yml/badge.svg)](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/deploy-prd.yml)
[![Destroy Development](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/destroy-development.yml/badge.svg)](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/destroy-development.yml)
[![Destroy Environment](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/destroy-environment.yml/badge.svg)](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/destroy-environment.yml)
[![PR Verify](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/frasermolyneux/portal-repository-func/actions/workflows/pr-verify.yml)

## Documentation
* [Development Workflows](/docs/development-workflows.md) - Branch strategy, CI/CD triggers, and deployment flows

## Overview
Azure Functions isolated app (.NET 9) that keeps XtremeIdiots Portal repository data fresh. Timer and HTTP-triggered jobs prune historical chat/events/stats, rebuild map popularity, snapshot live server telemetry, and synchronize ban/log file metadata across Call of Duty and Insurgency servers. Integrates the Repository, Servers Integration, and GeoLocation API clients, enriching live player data with geo lookups while emitting Application Insights telemetry and caching frequent lookups.

## Contributing
Please read the [contributing](CONTRIBUTING.md) guidance; this is a learning and development project.

## Security
Please read the [security](SECURITY.md) guidance; I am always open to security feedback through email or opening an issue.

## Local dev: MCP wire-up
Shared org conventions are served by the `frasermolyneux-copilot` MCP server (catalog in [`frasermolyneux/.github-copilot`](https://github.com/frasermolyneux/.github-copilot)). For client config (`.vscode/mcp.json`, Copilot CLI, Claude Desktop) and the tool surface, see [`mcp-server/README.md`](https://github.com/frasermolyneux/.github-copilot/blob/main/mcp-server/README.md). Cloud agents pick it up automatically via [`.github/copilot/mcp_config.json`](.github/copilot/mcp_config.json) plus the `Build MCP server` step in [`.github/workflows/copilot-setup-steps.yml`](.github/workflows/copilot-setup-steps.yml).
