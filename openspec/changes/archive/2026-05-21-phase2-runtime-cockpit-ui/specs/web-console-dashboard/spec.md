## ADDED Requirements

### Requirement: Overview Hosts Runtime Cockpit
The WebConsole Overview SHALL host the runtime cockpit as the primary first-screen connection experience.

#### Scenario: Render runtime cockpit in Overview
- **GIVEN** the WebConsole dashboard has loaded successfully
- **WHEN** the user views the Overview workspace
- **THEN** the Overview SHALL display the runtime cockpit workflow
- **AND** the Overview SHALL keep the existing workspace navigation visible
- **AND** the Overview SHALL keep the top telemetry strip visible
- **AND** the Overview SHALL keep the realtime observation rail visible when the rail is available

#### Scenario: Preserve controlled shutdown from cockpit
- **GIVEN** the runtime cockpit is visible
- **WHEN** the user activates the runtime shutdown action
- **THEN** the WebConsole SHALL use the existing controlled shutdown confirmation flow
- **AND** it SHALL call `POST /api/runtime/shutdown` only after confirmation

#### Scenario: Preserve real dashboard data sources
- **GIVEN** the runtime cockpit renders connection and evidence information
- **WHEN** the WebConsole implementation is inspected
- **THEN** it SHALL use backend API responses and runtime events as the data source
- **AND** it SHALL NOT introduce production mock data for cockpit state

### Requirement: Overview Cockpit Visual Fit
The Overview runtime cockpit SHALL match the existing WebConsole visual system.

#### Scenario: Match existing control desk layout
- **GIVEN** the runtime cockpit is rendered
- **WHEN** the user views the Overview at desktop width
- **THEN** the cockpit SHALL use the existing dark control-desk palette
- **AND** it SHALL use compact panel spacing consistent with existing WebConsole sections
- **AND** it SHALL avoid replacing the existing app shell with a separate landing-page style layout

#### Scenario: Avoid unusable overflow
- **GIVEN** the runtime cockpit is rendered at common desktop widths
- **WHEN** the user views the Overview
- **THEN** cockpit text and controls SHALL remain readable
- **AND** step list, detail panel, and evidence summary SHALL NOT overlap each other

### Requirement: Runtime Cockpit Lightweight Smoke
The project SHALL include a lightweight smoke check for runtime cockpit UI integration.

#### Scenario: Smoke verifies cockpit integration
- **GIVEN** the runtime cockpit UI has been implemented
- **WHEN** the runtime cockpit smoke command runs
- **THEN** it SHALL verify the expected cockpit source files exist
- **AND** it SHALL verify the Overview references the runtime cockpit
- **AND** it SHALL run the frontend production build

#### Scenario: Smoke excludes heavyweight release checks
- **GIVEN** the runtime cockpit smoke command runs during daily development
- **WHEN** the smoke completes
- **THEN** it SHALL NOT require MSI installation validation
- **AND** it SHALL NOT require a full browser E2E suite
