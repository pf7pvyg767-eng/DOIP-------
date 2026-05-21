# project-skeleton Specification

## Purpose
TBD - created by archiving change task-001. Update Purpose after archive.
## Requirements
### Requirement: Backend Solution Skeleton

The repository SHALL provide a .NET backend solution skeleton that establishes stable project boundaries for later tasks.

#### Scenario: Build backend solution
- **GIVEN** a developer has checked out the repository
- **WHEN** the developer runs the documented backend build command
- **THEN** the backend solution SHALL compile successfully

#### Scenario: Provide default backend project layout
- **GIVEN** the task is implemented under the default engineering assumptions
- **WHEN** the developer inspects the repository
- **THEN** the repository SHALL include `DoipSimulator.sln`
- **AND** the repository SHALL include `src/DoipSimulator.Host/`
- **AND** the repository SHALL include `src/DoipSimulator.Core/`
- **AND** the repository SHALL include `src/DoipSimulator.WebApi/`

### Requirement: CLI Placeholder Entrypoint

The Host project SHALL provide a minimal `doip-simulator` command-line entrypoint for development validation.

#### Scenario: Show CLI help
- **GIVEN** the Host project is built
- **WHEN** a developer runs `doip-simulator --help`
- **THEN** the command SHALL print help text describing available placeholder commands

#### Scenario: Run placeholder host
- **GIVEN** the Host project is built
- **WHEN** a developer runs `doip-simulator run`
- **THEN** the command SHALL start the placeholder host path
- **AND** the command SHALL print placeholder startup information
- **AND** the command SHALL NOT start DoIP, UDS, configuration loading, database, or external-service behavior

### Requirement: Vue Vite Frontend Shell

The repository SHALL provide a Vue/Vite frontend shell for the future Web console.

#### Scenario: Start frontend development server
- **GIVEN** frontend dependencies are installed
- **WHEN** a developer runs the documented frontend dev command
- **THEN** the Vue/Vite development server SHALL start

#### Scenario: Build frontend shell
- **GIVEN** frontend dependencies are installed
- **WHEN** a developer runs the documented frontend build command
- **THEN** the frontend shell SHALL build successfully
- **AND** the frontend SHALL NOT implement real Web control-console business pages

### Requirement: Test Framework Skeleton

The repository SHALL provide a runnable unit test skeleton.

#### Scenario: Run backend tests
- **GIVEN** the repository includes the initial test project
- **WHEN** a developer runs the documented backend test command
- **THEN** the test framework SHALL execute successfully
- **AND** at least one placeholder test SHALL pass

### Requirement: Developer Documentation And Ignore Rules

The repository SHALL include baseline developer documentation and ignore rules for generated artifacts.

#### Scenario: Read development commands
- **GIVEN** a developer opens `README.md`
- **WHEN** the developer follows the setup guidance
- **THEN** the README SHALL describe backend build/test commands
- **AND** the README SHALL describe frontend build/dev commands
- **AND** the README SHALL describe `doip-simulator --help` and `doip-simulator run`

#### Scenario: Ignore generated files
- **GIVEN** build outputs or dependency folders are generated
- **WHEN** Git status is inspected
- **THEN** common generated artifacts SHALL be ignored by `.gitignore`

