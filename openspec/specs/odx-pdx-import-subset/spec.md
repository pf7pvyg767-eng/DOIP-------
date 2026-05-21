# odx-pdx-import-subset Specification

## Purpose
TBD - created by archiving change task-025. Update Purpose after archive.
## Requirements
### Requirement: ODX Import Report

The system SHALL produce a structured import report for every ODX or PDX import attempt.

#### Scenario: 生成成功导入报告
- **GIVEN** a supported `.odx` or `.pdx` file is imported
- **WHEN** the import finishes
- **THEN** the system SHALL return an import report with `success` set to `true`
- **AND** the report SHALL include `imported.entityInfo`
- **AND** the report SHALL include `imported.dids`
- **AND** the report SHALL include `imported.dtcs` and `imported.routines` counts as zero unless a later validated change supports them
- **AND** the report SHALL include `skipped` and `errors` arrays.

#### Scenario: 生成失败导入报告
- **GIVEN** an uploaded file is malformed, unsupported, unsafe, or missing a required ODX entry
- **WHEN** the import is attempted
- **THEN** the system SHALL return an import report with `success` set to `false`
- **AND** the report SHALL include one or more clear errors
- **AND** the service process SHALL remain available for later requests.

### Requirement: ODX File Upload API

The WebApi SHALL accept `.odx` uploads for the supported import subset.

#### Scenario: 上传 `.odx`
- **GIVEN** the WebApi is running
- **WHEN** a caller sends `POST /api/import/odx` with a `.odx` file
- **THEN** the API SHALL parse the file using the ODX import service
- **AND** it SHALL return an import report
- **AND** it SHALL NOT require the caller to provide a PDX package.

#### Scenario: 拒绝非 ODX 上传
- **GIVEN** the WebApi is running
- **WHEN** a caller sends `POST /api/import/odx` with a missing file, wrong extension, empty body, or unreadable XML
- **THEN** the API SHALL return a failed import report or validation error
- **AND** it SHALL NOT mutate `SimulatorConfig`.

### Requirement: PDX Package Upload API

The WebApi SHALL accept `.pdx` uploads and identify an ODX entry from the package.

#### Scenario: 上传 `.pdx` 并识别入口
- **GIVEN** the WebApi is running
- **AND** a `.pdx` package contains a supported ODX entry
- **WHEN** a caller sends `POST /api/import/pdx`
- **THEN** the API SHALL read the package through the PDX package reader
- **AND** it SHALL identify the ODX entry
- **AND** it SHALL import that entry using the ODX import service
- **AND** it SHALL return an import report.

#### Scenario: PDX 包入口无效
- **GIVEN** a `.pdx` package has no ODX entry, ambiguous ODX entries, illegal zip content, or unsafe paths
- **WHEN** the package is imported
- **THEN** the system SHALL return a failed import report
- **AND** it SHALL include an error describing the package or entry problem
- **AND** it SHALL NOT write files outside the controlled import workspace.

### Requirement: ECU Basic Information Import

The ODX import subset SHALL map supported ECU basic information into `SimulatorConfig`.

#### Scenario: 导入 ECU 基本信息
- **GIVEN** a supported ODX entry contains ECU name and basic identity fields that can be mapped to simulator identity settings
- **WHEN** the ODX import service parses the entry
- **THEN** the import result SHALL expose the supported ECU basic information
- **AND** the configuration merge SHALL update only valid supported `SimulatorConfig` fields
- **AND** unsupported ECU fields SHALL be listed in `skipped`.

#### Scenario: 非法 ECU 字段不破坏配置
- **GIVEN** an ODX entry contains an ECU basic information field that cannot pass existing configuration validation
- **WHEN** the import result is merged
- **THEN** the merge SHALL fail or skip the invalid field with a clear report entry
- **AND** the previously valid `SimulatorConfig` SHALL remain usable.

### Requirement: DID Basic Information Import

The ODX import subset SHALL parse supported DID basic information into the existing DID configuration model.

#### Scenario: 样例 `.odx` 导入 DID
- **GIVEN** a sample `.odx` file contains supported DID definitions
- **WHEN** the ODX import service parses the file
- **THEN** each supported DID SHALL be mapped to the existing DID configuration structure
- **AND** DID identifiers SHALL be represented as 16-bit hex identifiers
- **AND** DID names SHALL be preserved when available
- **AND** supported fixed values SHALL be represented using the existing fixed hex value convention.

#### Scenario: 跳过不支持 DID 字段
- **GIVEN** a DID definition contains unsupported data types, scaling formulas, dynamic expressions, complex computation methods, or unsupported metadata
- **WHEN** the ODX import service parses the DID
- **THEN** the unsupported field SHALL be added to the `skipped` list with a path and reason
- **AND** supported fields from the same DID MAY still be imported when they are valid and sufficient.

#### Scenario: DID 冲突处理
- **GIVEN** an imported DID has the same identifier as an existing DID in `SimulatorConfig`
- **WHEN** the import result is merged
- **THEN** the merge SHALL apply a deterministic overwrite or merge policy documented by the implementation
- **AND** the import report SHALL indicate that an existing DID was updated or skipped
- **AND** duplicate handling SHALL NOT create ambiguous runtime DID entries.

### Requirement: Import Result Merge And Save

The system SHALL merge supported import results into `SimulatorConfig` and save them when requested by the import workflow.

#### Scenario: 合并并保存导入结果
- **GIVEN** an import result contains valid ECU basic information or DID entries
- **WHEN** the caller requests persistence
- **THEN** the system SHALL merge the supported data into `SimulatorConfig`
- **AND** it SHALL validate the merged configuration
- **AND** it SHALL save the configuration through the existing configuration store
- **AND** it SHALL include the saved import outcome in the report.

#### Scenario: 合并失败不保存半成品
- **GIVEN** an import result would make `SimulatorConfig` invalid
- **WHEN** persistence is requested
- **THEN** the system SHALL return a failed import report
- **AND** it SHALL NOT save a partially invalid configuration
- **AND** the report SHALL identify the invalid imported field.

### Requirement: Imported DID Runtime Availability

Imported and saved DID entries SHALL be available through the existing `0x22` ReadDataByIdentifier behavior.

#### Scenario: 导入 DID 后通过 `0x22` 读取
- **GIVEN** a supported DID has been imported and saved to `SimulatorConfig`
- **AND** the simulator loads or refreshes the resulting DID configuration
- **WHEN** a diagnostic client sends a `0x22` request for that DID after Routing Activation
- **THEN** the existing ReadDataByIdentifier service SHALL return the DID value according to the imported configuration
- **AND** the import implementation SHALL NOT add a separate DID read path outside the existing UDS dispatcher.

### Requirement: WebConsole Import Workflow

The WebConsole SHALL provide a focused ODX/PDX import workflow for the supported subset.

#### Scenario: 显示导入报告
- **GIVEN** a user uploads an `.odx` or `.pdx` file from the WebConsole
- **WHEN** the API returns an import report
- **THEN** the UI SHALL display whether the import succeeded
- **AND** it SHALL display imported ECU and DID counts
- **AND** it SHALL display skipped entries and errors
- **AND** it SHALL NOT claim full ODX compatibility.

#### Scenario: 错误上传不阻塞后续导入
- **GIVEN** a user uploads an invalid import file
- **WHEN** the API returns a failed report
- **THEN** the UI SHALL show the failure reason
- **AND** the user SHALL be able to attempt another import without reloading the application.

### Requirement: ODX/PDX Import Scope Boundaries

The task-025 implementation SHALL remain limited to the first supported ODX/PDX import subset.

#### Scenario: 不解析全量 ODX
- **GIVEN** task-025 is implemented
- **WHEN** the ODX import code is inspected
- **THEN** the change SHALL NOT implement full ODX schema coverage
- **AND** unsupported ODX branches SHALL be skipped or rejected with report entries.

#### Scenario: 不完整解析 DTC Routine Flash
- **GIVEN** task-025 is implemented
- **WHEN** import report and configuration outputs are inspected
- **THEN** the change SHALL NOT fully parse DTC definitions
- **AND** it SHALL NOT fully parse Routine definitions
- **AND** it SHALL NOT fully parse Flash data or download flows
- **AND** report counters for unsupported areas SHALL remain zero or skipped unless a later validated change expands them.

#### Scenario: 不做第三方工具链兼容认证
- **GIVEN** task-025 is implemented
- **WHEN** documentation, tests, and UI text are inspected
- **THEN** the change SHALL NOT claim certification or compatibility with any third-party ODX/PDX authoring toolchain
- **AND** tests SHALL be limited to project fixtures and explicitly supported subset behavior.

### Requirement: ODX/PDX Import Verification

The task-025 implementation SHALL include focused verification for the supported import subset.

#### Scenario: ODX 单元测试
- **GIVEN** unit tests cover the ODX import service
- **WHEN** sample `.odx` fixtures are parsed
- **THEN** the tests SHALL verify DID basic information import
- **AND** they SHALL verify unsupported fields are reported as skipped
- **AND** they SHALL verify malformed ODX returns a failed report.

#### Scenario: PDX 单元测试
- **GIVEN** unit tests cover the PDX package reader
- **WHEN** sample `.pdx` fixtures are parsed
- **THEN** the tests SHALL verify safe package reading
- **AND** they SHALL verify ODX entry identification
- **AND** they SHALL verify missing or invalid entries return failed reports.

#### Scenario: API 和集成测试
- **GIVEN** API and integration tests run for the import workflow
- **WHEN** `.odx`, `.pdx`, and invalid files are uploaded
- **THEN** the tests SHALL verify import reports
- **AND** they SHALL verify persisted DID configuration can be read through `0x22`
- **AND** they SHALL verify service availability after failed imports.

