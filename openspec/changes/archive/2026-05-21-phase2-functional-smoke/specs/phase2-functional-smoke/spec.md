## ADDED Requirements

### Requirement: Phase 2 Functional Smoke Script
The repository SHALL provide a lightweight Phase 2 smoke script for a running local simulator.

#### Scenario: Smoke checks core API and connection guide
- **GIVEN** the simulator host is running
- **WHEN** `scripts/phase2-functional-smoke.ps1` is executed
- **THEN** the script SHALL check API health
- **AND** it SHALL check runtime summary connection fields

#### Scenario: Smoke checks DoIP and UDS
- **GIVEN** the simulator host is running with UDP and TCP DoIP listeners
- **WHEN** the smoke script is executed
- **THEN** it SHALL verify UDP discovery
- **AND** it SHALL verify TCP Routing Activation
- **AND** it SHALL verify UDS `0x22` static DID reading
- **AND** it SHALL verify UDS `0x22` dynamic DID reading

#### Scenario: Smoke checks DID sampling and shutdown
- **GIVEN** the simulator host is running
- **WHEN** the smoke script is executed
- **THEN** it SHALL verify the DID sample API returns a numeric dynamic DID sample
- **AND** it SHALL verify the shutdown API accepts a shutdown request unless shutdown is explicitly skipped

### Requirement: Phase 2 Smoke Output
The smoke script SHALL print clear pass/fail output and exit non-zero if any check fails.

#### Scenario: All checks pass
- **GIVEN** every smoke check succeeds
- **WHEN** the script finishes
- **THEN** it SHALL print a summary with zero failures
- **AND** it SHALL exit with code `0`

#### Scenario: Any check fails
- **GIVEN** one or more smoke checks fail
- **WHEN** the script finishes
- **THEN** it SHALL print the failed check names and details
- **AND** it SHALL exit with a non-zero code

### Requirement: Phase 2 Smoke Scope
The Phase 2 smoke SHALL avoid heavyweight installation and full UI automation.

#### Scenario: Smoke excludes heavyweight flows
- **WHEN** the smoke script is inspected
- **THEN** it SHALL NOT build MSI installers
- **AND** it SHALL NOT run full browser UI E2E
- **AND** it SHALL NOT require a report generation system
