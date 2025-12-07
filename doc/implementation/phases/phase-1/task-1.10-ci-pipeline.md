# Task 1.10: CI Pipeline Foundation

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P2 (Should-have)
> **Status:** Pending

## Description

Set up initial CI pipeline for build and test.

---

## Subtasks

### 1.10.1 Create GitHub Actions Workflow

- [ ] Create `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

env:
  DOTNET_VERSION: '9.0.x'
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  # ===========================================
  # Build Job
  # ===========================================
  build:
    name: Build
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore src/NovaTuneApp/NovaTuneApp.sln

      - name: Build
        run: dotnet build src/NovaTuneApp/NovaTuneApp.sln --no-restore /p:TreatWarningsAsErrors=true

      - name: Upload build artifacts
        uses: actions/upload-artifact@v4
        with:
          name: build-output
          path: |
            src/NovaTuneApp/**/bin/
            src/NovaTuneApp/**/obj/
          retention-days: 1

  # ===========================================
  # Format Check Job
  # ===========================================
  format:
    name: Format Check
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Verify formatting
        run: dotnet format src/NovaTuneApp/NovaTuneApp.sln --verify-no-changes --verbosity diagnostic

  # ===========================================
  # Unit Tests Job
  # ===========================================
  test:
    name: Unit Tests
    runs-on: ubuntu-latest
    needs: build
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore src/NovaTuneApp/NovaTuneApp.sln

      - name: Run tests
        run: |
          dotnet test src/NovaTuneApp/NovaTuneApp.sln \
            --no-restore \
            --filter "Category!=Integration" \
            --logger "trx;LogFileName=test-results.trx" \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: TestResults/

      - name: Upload coverage reports
        uses: codecov/codecov-action@v4
        with:
          directory: ./TestResults
          fail_ci_if_error: false
          verbose: true

  # ===========================================
  # Security Scanning Job
  # ===========================================
  security:
    name: Security Scan
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Secret scanning (gitleaks)
        uses: gitleaks/gitleaks-action@v2
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore src/NovaTuneApp/NovaTuneApp.sln

      - name: Run security audit
        run: dotnet list src/NovaTuneApp/NovaTuneApp.sln package --vulnerable --include-transitive

  # ===========================================
  # Docker Build Job
  # ===========================================
  docker:
    name: Docker Build
    runs-on: ubuntu-latest
    needs: [build, test]
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Build Docker image
        uses: docker/build-push-action@v5
        with:
          context: ./src/NovaTuneApp
          file: ./src/NovaTuneApp/NovaTuneApp.ApiService/Dockerfile
          push: false
          tags: novatune-api:${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

      - name: Verify FFmpeg in image
        run: |
          docker run --rm novatune-api:${{ github.sha }} ffmpeg -version
          docker run --rm novatune-api:${{ github.sha }} ffprobe -version
```

---

### 1.10.2 Add Secret Scanning Configuration

- [ ] Create `.gitleaks.toml`:

```toml
title = "NovaTune Gitleaks Configuration"

[allowlist]
description = "Global allowlist"
paths = [
    '''\.gitleaks\.toml$''',
    '''secrets\.json\.example$''',
    '''\.env\.example$''',
    '''doc/.*\.md$''',
]

[[rules]]
id = "jwt-secret"
description = "JWT Secret"
regex = '''(?i)(jwt|token).*[=:]\s*['"][a-zA-Z0-9_\-]{32,}['"]'''
tags = ["secret", "jwt"]

[[rules]]
id = "minio-secret"
description = "MinIO Secret Key"
regex = '''(?i)minio.*secret.*[=:]\s*['"][a-zA-Z0-9_\-]{20,}['"]'''
tags = ["secret", "minio"]

[[rules]]
id = "connection-string"
description = "Database Connection String"
regex = '''(?i)(connection.*string|data.*source|server=).*password=[^;]+'''
tags = ["secret", "database"]

[[rules]]
id = "private-key"
description = "Private Key"
regex = '''-----BEGIN (RSA |EC )?PRIVATE KEY-----'''
tags = ["secret", "key"]

[rules.allowlist]
paths = [
    '''test/.*''',
    '''.*_test\.go$''',
    '''.*Tests\.cs$''',
]
```

---

### 1.10.3 Configure Branch Protection Rules

- [ ] Document branch protection rules (configure in GitHub settings)

**Required Branch Protection Rules for `main`:**

| Rule | Setting |
|------|---------|
| Require pull request reviews | Yes |
| Required approvals | 1 |
| Dismiss stale reviews | Yes |
| Require status checks | Yes |
| Required checks | `Build`, `Format Check`, `Unit Tests`, `Security Scan` |
| Require branches to be up to date | Yes |
| Require conversation resolution | Yes |
| Require signed commits | Optional |
| Include administrators | Yes |
| Allow force pushes | No |
| Allow deletions | No |

**GitHub CLI commands to configure:**
```bash
# Set branch protection rules
gh api repos/{owner}/{repo}/branches/main/protection \
  -X PUT \
  -F required_status_checks='{"strict":true,"contexts":["Build","Format Check","Unit Tests","Security Scan"]}' \
  -F enforce_admins=true \
  -F required_pull_request_reviews='{"required_approving_review_count":1,"dismiss_stale_reviews":true}' \
  -F restrictions=null
```

---

### 1.10.4 Add Build Status Badge

- [ ] Add build status badge to README.md

**Badge markdown:**
```markdown
# NovaTune

[![CI](https://github.com/{owner}/NovaTune/actions/workflows/ci.yml/badge.svg)](https://github.com/{owner}/NovaTune/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/{owner}/NovaTune/branch/main/graph/badge.svg)](https://codecov.io/gh/{owner}/NovaTune)

Audio management platform built on .NET 9 and Aspire.
```

**Additional badges to consider:**
```markdown
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-blue)
![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker)
```

---

## PR Template

- [ ] Create `.github/pull_request_template.md`:

```markdown
## Description

Brief description of changes.

## Type of Change

- [ ] Bug fix (non-breaking change fixing an issue)
- [ ] New feature (non-breaking change adding functionality)
- [ ] Breaking change (fix or feature causing existing functionality to change)
- [ ] Documentation update

## Requirement References

- FR/NFR ID: (if applicable)

## Checklist

- [ ] I have read the [AGENTS.md](AGENTS.md) guidelines
- [ ] My code follows the project's code style
- [ ] I have added tests that prove my fix/feature works
- [ ] New and existing tests pass locally with my changes
- [ ] I have updated documentation as needed
- [ ] My changes generate no new warnings

## Testing

Describe how this was tested:

```bash
# Commands run
dotnet test
```

## Screenshots (if applicable)

Add screenshots for UI changes.
```

---

## Issue Templates

- [ ] Create `.github/ISSUE_TEMPLATE/bug_report.md`:

```markdown
---
name: Bug Report
about: Report a bug to help us improve
title: '[BUG] '
labels: bug
assignees: ''
---

## Describe the Bug

A clear description of the bug.

## Steps to Reproduce

1. Go to '...'
2. Click on '...'
3. See error

## Expected Behavior

What you expected to happen.

## Actual Behavior

What actually happened.

## Environment

- OS: [e.g., macOS 14.0]
- .NET Version: [e.g., 9.0.0]
- Docker Version: [e.g., 24.0.0]

## Additional Context

Any other context, logs, or screenshots.
```

---

## Acceptance Criteria

- [ ] CI runs on every push
- [ ] Build fails on warnings
- [ ] Format check enforced
- [ ] Secret scanning enabled
- [ ] Docker build included
- [ ] Branch protection documented

---

## Verification

```bash
# Trigger CI locally with act (optional)
act push

# Or verify workflow syntax
gh workflow view ci.yml

# Check workflow runs
gh run list --workflow=ci.yml
```

---

## File Checklist

- [ ] `.github/workflows/ci.yml`
- [ ] `.gitleaks.toml`
- [ ] `.github/pull_request_template.md`
- [ ] `.github/ISSUE_TEMPLATE/bug_report.md`
- [ ] `README.md` (updated with badges)

---

## Future Additions (Later Phases)

| Phase | CI Addition |
|-------|-------------|
| 3 | Integration tests with Testcontainers |
| 4 | NCache/MinIO integration tests |
| 5 | E2E streaming tests |
| 6 | Performance benchmarks |
| 8 | SAST, DAST, OpenAPI diff check |

---

## Navigation

[Task 1.9: FFmpeg Image](task-1.9-ffmpeg-image.md) | [Phase 1 Overview](overview.md) | [Phase 2: User Management](../phase-2-user-management.md)
