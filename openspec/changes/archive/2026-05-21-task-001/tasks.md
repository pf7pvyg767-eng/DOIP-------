# Implementation Tasks: 建立解决方案骨架和开发入口

**Change ID:** `task-001`

---

## Phase 1: Backend Solution Skeleton

- [x] 1.1 Create `DoipSimulator.sln`.
- [x] 1.2 Create `src/DoipSimulator.Host/` as the command-line entry project.
- [x] 1.3 Create `src/DoipSimulator.Core/` as the shared core project.
- [x] 1.4 Create `src/DoipSimulator.WebApi/` as the future control-plane API project shell.
- [x] 1.5 Wire project references only where needed for the skeleton.

**Quality Gate:**
- [x] Backend solution builds successfully.
- [x] No DoIP, UDS, configuration loading, database, or external-service implementation is added.

---

## Phase 2: CLI Placeholder Entry

- [x] 2.1 Add `doip-simulator --help` behavior.
- [x] 2.2 Add `doip-simulator run` behavior.
- [x] 2.3 Ensure `run` prints placeholder startup information only.
- [x] 2.4 Ensure unknown or invalid commands fail with clear command-line feedback.

**Quality Gate:**
- [x] `doip-simulator --help` can be executed.
- [x] `doip-simulator run` can be executed and prints placeholder startup information.

---

## Phase 3: Frontend Shell

- [x] 3.1 Create `src/DoipSimulator.WebConsole/` as a Vue/Vite project shell.
- [x] 3.2 Keep the initial page minimal and non-business-specific.
- [x] 3.3 Ensure frontend dependency installation and dev-server startup are documented.
- [x] 3.4 Ensure frontend build can be run.

**Quality Gate:**
- [x] Frontend dependencies can be installed.
- [x] Frontend dev server can start.
- [x] Frontend build can run.

---

## Phase 4: Tests And Repo Developer Entrypoints

- [x] 4.1 Create `tests/DoipSimulator.Core.Tests/`.
- [x] 4.2 Add at least one placeholder unit test.
- [x] 4.3 Add unified build, test, and run scripts or equivalent documented commands.
- [x] 4.4 Add `.gitignore`.
- [x] 4.5 Add baseline `README.md` with directory and command guidance.

**Quality Gate:**
- [x] Backend tests run successfully.
- [x] README documents backend build/test, frontend build/dev, and CLI help/run commands.
- [x] Repository ignores generated build artifacts and dependency folders.

---

## Completion Checklist

- [x] `DoipSimulator.sln` exists and builds.
- [x] `src/DoipSimulator.Host/` exists and provides CLI placeholder entry.
- [x] `src/DoipSimulator.Core/` exists.
- [x] `src/DoipSimulator.WebApi/` exists.
- [x] `src/DoipSimulator.WebConsole/` exists as a Vue/Vite shell.
- [x] `tests/DoipSimulator.Core.Tests/` exists and test framework runs.
- [x] `.gitignore` exists.
- [x] `README.md` exists.
- [x] Backend build executed.
- [x] Backend test executed.
- [x] Frontend build executed.
- [x] `doip-simulator --help` executed.
- [x] `doip-simulator run` executed.
- [x] Scope check confirms no DoIP, UDS, configuration loading, real Web console page, database, or external service implementation was added.
