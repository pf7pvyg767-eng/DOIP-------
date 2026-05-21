## ADDED Requirements

### Requirement: Runtime Cockpit Workflow
The Web Console SHALL provide a runtime cockpit workflow for diagnostic tester connection progress.

#### Scenario: Display four diagnostic connection steps
- **GIVEN** the Web Console Overview has loaded
- **WHEN** the runtime cockpit renders
- **THEN** it SHALL display `UDP Discovery`
- **AND** it SHALL display `TCP Connect`
- **AND** it SHALL display `Routing Activation`
- **AND** it SHALL display `UDS Read DID`

#### Scenario: Derive step state from real runtime data
- **GIVEN** runtime summary, connection snapshots, and recent runtime events are available
- **WHEN** the runtime cockpit evaluates the workflow
- **THEN** it SHALL derive each step state from those runtime data sources
- **AND** it SHALL NOT use hard-coded or mock step completion values

#### Scenario: Select the active or waiting step
- **GIVEN** one workflow step is active, waiting, or failed
- **WHEN** the runtime cockpit renders
- **THEN** the active, waiting, or failed step SHALL be selected by default
- **AND** the user SHALL be able to manually select another step for inspection

### Requirement: Runtime Cockpit Step Detail
The runtime cockpit SHALL display a detail panel for the selected workflow step.

#### Scenario: Show parameter-first detail for normal states
- **GIVEN** the selected step is not failed
- **WHEN** the detail panel renders
- **THEN** it SHALL prioritize relevant connection parameters
- **AND** it SHALL include Web API, DoIP TCP or UDP, tester source address, ECU logical address, or UDS request information when applicable

#### Scenario: Show troubleshooting-first detail for failed states
- **GIVEN** the selected step has failed
- **WHEN** the detail panel renders
- **THEN** it SHALL prioritize the failure state
- **AND** it SHALL display a suggested next action based on the selected step

#### Scenario: Show evidence for selected step
- **GIVEN** recent runtime events are available
- **WHEN** the selected step detail renders
- **THEN** it SHALL include recent DoIP or UDS evidence when available
- **AND** it SHALL show a clear unavailable state when evidence is missing

### Requirement: Runtime Cockpit Copy Actions
The runtime cockpit SHALL provide copy actions for diagnostic connection and first-read workflow data.

#### Scenario: Copy connection parameters
- **GIVEN** runtime summary data is available
- **WHEN** the user activates a copy action for a connection step
- **THEN** the copied text SHALL include real runtime values for the relevant endpoint or logical address

#### Scenario: Copy first UDS read action
- **GIVEN** runtime summary data is available
- **WHEN** the user activates the UDS read copy action
- **THEN** the copied text SHALL include the ECU logical address
- **AND** it SHALL include a `ReadDataByIdentifier` example for DID `0xF190`

#### Scenario: Show copy feedback
- **GIVEN** a copy action succeeds
- **WHEN** the clipboard write completes
- **THEN** the runtime cockpit SHALL show a short-lived success indication

### Requirement: Runtime Cockpit Evidence Summary
The runtime cockpit SHALL provide compact evidence summaries without replacing full diagnostic workspaces.

#### Scenario: Show latest traffic evidence
- **GIVEN** recent runtime events contain DoIP or UDS events
- **WHEN** the runtime cockpit renders
- **THEN** it SHALL display the latest available DoIP event summary
- **AND** it SHALL display the latest available UDS request or response summary

#### Scenario: Show PCAP and event evidence state
- **GIVEN** PCAP status and recent event data are available
- **WHEN** the runtime cockpit renders
- **THEN** it SHALL display whether PCAP is recording
- **AND** it SHALL display recent event availability

#### Scenario: Show DID preview
- **GIVEN** DID sample data is available
- **WHEN** the runtime cockpit renders
- **THEN** it SHALL display at least one current DID preview value
- **AND** it SHALL indicate that the Data workspace contains dynamic DID editing and live charts

### Requirement: Runtime Cockpit Availability
The runtime cockpit SHALL remain usable when optional evidence sources fail.

#### Scenario: Evidence API failure does not blank the cockpit
- **GIVEN** dashboard health and configuration data load successfully
- **AND** one optional cockpit evidence request fails
- **WHEN** the runtime cockpit renders
- **THEN** it SHALL keep the cockpit shell visible
- **AND** it SHALL mark the affected evidence area unavailable

#### Scenario: Shutdown state remains clear
- **GIVEN** a runtime shutdown request is in progress or complete
- **WHEN** the runtime cockpit renders
- **THEN** it SHALL display the shutdown state clearly
- **AND** it SHALL prevent repeated shutdown submissions while stopping or stopped
