## MODIFIED Requirements

### Requirement: DID Runtime Value Store
The system SHALL maintain current runtime values for configured static and dynamic DIDs and expose them consistently to Web API, WebConsole, `0x22`, and `0x2E` where applicable.

#### Scenario: 静态运行时值覆盖配置初始值
- **GIVEN** DID `0xF190` is configured with an initial fixed hex value
- **WHEN** the simulator runtime starts
- **THEN** the DID runtime store SHALL expose DID `0xF190` with that current value
- **AND** `0x22` reads SHALL use the runtime store value rather than a stale copy

#### Scenario: Web 写入后读取使用新静态值
- **GIVEN** DID `0xF190` is configured as a writable static DID
- **WHEN** the DID runtime value is updated through the API
- **THEN** the runtime store SHALL replace the current value for DID `0xF190`
- **AND** subsequent `0x22` reads SHALL return the updated value

#### Scenario: `0x2E` 写入后 Web 使用新静态值
- **GIVEN** DID `0xF190` is configured as a writable static DID
- **WHEN** UDS `0x2E` writes a new value for DID `0xF190`
- **THEN** the runtime store SHALL replace the current value for DID `0xF190`
- **AND** `GET /api/dids` SHALL return the updated value

#### Scenario: 动态 DID 读取使用 provider 当前值
- **GIVEN** DID `0xF192` is configured with a valid dynamic value provider
- **WHEN** the DID runtime store reads DID `0xF192`
- **THEN** the store SHALL return the current generated provider bytes
- **AND** `GET /api/dids` and UDS `0x22` SHALL observe values generated from the same runtime store behavior

### Requirement: Scope Boundaries
The implementation SHALL remain limited to fixed hex DID runtime editing, read-only dynamic DID providers, DID APIs, JSON persistence, and UDS `0x2E` WriteDataByIdentifier for static writable DIDs.

#### Scenario: 涓嶆敮鎸佸鏉傜紪鐮佽浆鎹?
- **GIVEN** task-015 is implemented
- **WHEN** DID write API and WebConsole controls are inspected
- **THEN** the change SHALL NOT add VIN-specific string conversion
- **AND** it SHALL NOT add decimal, base64, endian conversion, scaling, script, or business semantic encoding

#### Scenario: 涓嶆敮鎸?ODX 鍐欏叆瀹氫箟
- **GIVEN** task-015 is implemented
- **WHEN** configuration and import code are inspected
- **THEN** it SHALL NOT add ODX write definition parsing
- **AND** it SHALL NOT add PDX import or diagnostic database conversion

#### Scenario: 动态 DID 不支持写入覆盖
- **GIVEN** a DID is configured with `valueProvider.type` set to `random`, `sine`, or `linear`
- **WHEN** DID write API or UDS `0x2E` write behavior is inspected
- **THEN** the change SHALL NOT persist generated provider samples as fixed DID values
- **AND** it SHALL NOT allow API or UDS writes to replace provider configuration

#### Scenario: 涓嶆墿澶у埌鍏朵粬璇婃柇涓氬姟
- **GIVEN** task-015 is implemented
- **WHEN** changed files are inspected
- **THEN** the change SHALL NOT implement DTC services
- **AND** it SHALL NOT implement Routine services
- **AND** it SHALL NOT implement Flash services
- **AND** it SHALL NOT implement SecurityAccess seed/key or unlock behavior
- **AND** SecurityAccess usage SHALL be limited to reading existing security state for DID write permission checks
