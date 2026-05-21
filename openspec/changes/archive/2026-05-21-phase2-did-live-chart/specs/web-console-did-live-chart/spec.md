## ADDED Requirements

### Requirement: DID Live Chart Panel
The WebConsole SHALL provide a DID live chart panel for numeric DID samples.

#### Scenario: Numeric DIDs are selectable
- **GIVEN** WebConsole receives static and dynamic DID samples
- **WHEN** the chart panel renders
- **THEN** only DIDs with numeric sample values SHALL be selectable for charting
- **AND** selected DIDs SHALL show current numeric values

#### Scenario: Multiple DID chart series
- **GIVEN** multiple numeric DIDs are selected
- **WHEN** sample data is available for each selected DID
- **THEN** the chart SHALL render one series per selected DID
- **AND** the legend SHALL identify each DID

### Requirement: DID Chart Data Updates
The DID live chart SHALL update from runtime events and polling.

#### Scenario: Update from DID read event
- **GIVEN** the runtime event stream receives a `uds.did.read` event with `did`, `numericValue`, and `sampledAt`
- **WHEN** the DID is selected for charting
- **THEN** the chart SHALL append the event sample to that DID series

#### Scenario: Poll samples without diagnostic traffic
- **GIVEN** no diagnostic tester is reading DIDs
- **WHEN** the chart polling interval elapses
- **THEN** WebConsole SHALL call `GET /api/dids/samples`
- **AND** numeric samples SHALL update chart series for selected DIDs

### Requirement: DID Chart Retention And Switching
The DID live chart SHALL keep bounded recent data and avoid stale series confusion when selection changes.

#### Scenario: Retain recent samples only
- **GIVEN** a selected DID receives more than 300 samples or samples older than 60 seconds
- **WHEN** the chart updates
- **THEN** the series SHALL retain no more than 300 points
- **AND** the series SHALL drop points older than 60 seconds

#### Scenario: Switch selected DID
- **GIVEN** a user changes selected DIDs
- **WHEN** the chart updates
- **THEN** unselected DID series SHALL NOT be rendered
- **AND** stale values from previously selected DIDs SHALL NOT be shown as the current selected value
