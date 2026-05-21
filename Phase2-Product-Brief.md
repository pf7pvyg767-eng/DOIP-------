# Phase 2 Product Brief: DoIP Tester Validation Target

## 1. Product Goal

Phase 2 turns the current DoIP Simulator MVP into a repeatable validation target for testing a custom DoIP diagnostic tester.

The primary purpose is not to emulate every OEM ECU feature. The primary purpose is to help a tester developer:

- Verify that the tester follows DoIP and UDS protocol rules.
- Reproduce normal and abnormal ECU behavior.
- Capture trustworthy PCAP evidence for debugging.
- Compare tester behavior across standard cases and failure cases.
- Improve the tester based on observable logs, traces, and packet captures.

The simulator should become a local Windows tool that can act as both:

- A standard DoIP/UDS ECU target.
- An abnormal/fault-injection target.

## 2. Target User

The main user is a diagnostic tester developer who is building or improving a DoIP upper-level tester application.

The user needs to answer questions such as:

- Can my tester discover a DoIP ECU correctly?
- Can it activate routing and maintain the connection?
- Can it parse DoIP and UDS responses correctly?
- Can it handle NRC, timeout, disconnect, malformed frames, and TLS errors?
- Can I prove what happened by looking at event logs and Wireshark captures?

## 3. Core Use Cases

### 3.1 Standard DoIP Link Validation

The simulator shall provide stable baseline behavior for:

- UDP Vehicle Identification.
- TCP Routing Activation.
- Alive Check.
- DoIP Diagnostic Message.
- Entity status and power mode where supported.
- Single ECU logical address.
- Source address whitelist validation.

The baseline behavior should be deterministic, so tester regressions can be compared across repeated runs.

### 3.2 Standard UDS Flow Validation

The simulator shall provide repeatable UDS service behavior for common tester workflows:

- `0x10` DiagnosticSessionControl.
- `0x3E` TesterPresent.
- `0x22` ReadDataByIdentifier.
- `0x2E` WriteDataByIdentifier.
- `0x19` ReadDTCInformation.
- `0x14` ClearDiagnosticInformation.
- `0x27` SecurityAccess.
- `0x31` RoutineControl.

Phase 2 should prioritize correctness, observability, and configurability over full ECU business realism.

### 3.3 TLS DoIP Debugging

The simulator shall support TLS DoIP tester validation.

The key Phase 2 requirement is Wireshark usability:

- TLS handshake should be observable.
- Certificate failures should be reproducible.
- TLS key log export should be supported for decrypting captures in Wireshark.
- The UI should clearly show the TLS key log path.

Without TLS key log export, encrypted UDS payload analysis is incomplete.

### 3.4 PCAP Evidence Generation

The simulator shall produce PCAP files that are useful for debugging tester behavior.

PCAP output should support:

- Standard DoIP/UDS TCP traffic.
- UDP discovery traffic.
- TLS traffic.
- Correct enough TCP sequence and acknowledgement tracking for Wireshark analysis.
- Connection lifecycle markers where feasible, including SYN/FIN/RST modeling or equivalent capture clarity.
- Clear file naming with timestamp and scenario name.

The PCAP workflow should be visible from the Web Console:

- Recording state.
- Current file path.
- Bytes written.
- Related TLS key log path.
- Wireshark decode instructions.

### 3.5 Fault Injection for Tester Robustness

The simulator shall provide controlled negative scenarios to expose tester defects.

Required Phase 2 fault categories:

- Routing activation rejection.
- Source address rejection.
- No response.
- Delayed response.
- `0x78 ResponsePending`.
- Manual NRC.
- Custom UDS response.
- TCP disconnect.
- TCP idle timeout.
- Malformed DoIP header.
- Wrong payload type.
- Wrong payload length.
- Wrong inverse protocol version.
- Partial packet / sticky packet behavior if technically feasible.
- TLS certificate failure.
- Missing client certificate.

Each fault should produce a clear runtime event and should be visible in the Realtime view.

### 3.6 Compatibility Test Cases

The simulator shall include a tester compatibility test suite.

Each test case should define:

- Case ID.
- Name.
- Purpose.
- Initial simulator configuration.
- Trigger steps.
- Expected tester behavior.
- Expected simulator response.
- Generated event log.
- Generated PCAP path.

The UI should allow the user to run individual cases and inspect results.

## 4. Product Scope

### 4.1 In Scope

Phase 2 includes:

- Stable standard DoIP/UDS validation flows.
- PCAP reliability improvements.
- TLS key log export.
- Fault-injection scenarios for tester robustness.
- Scenario presets.
- Compatibility test case runner.
- Runtime shutdown from UI.
- Port and process visibility.
- Configuration profile management for common tester scenarios.

### 4.2 Out of Scope

Phase 2 does not prioritize:

- Full ODX/PDX parsing.
- Multi-ECU gateway simulation.
- Full OEM flashing implementation.
- Full DTC memory model with snapshot and extended data.
- Enterprise user management.
- Cloud deployment.
- Cross-platform support.
- Public SDK.
- Complete vehicle gateway behavior.

ODX/PDX import may be improved only when it directly helps tester validation scenarios.

## 5. Functional Requirements

### 5.1 Runtime Control

The Web Console shall provide a controlled shutdown action.

The shutdown action shall:

- Require confirmation.
- Stop DoIP UDP/TCP/TLS listeners.
- Stop active PCAP recording.
- Flush runtime logs.
- Close WebSocket event streams.
- Exit the simulator process.

The UI shall show:

- Web API port.
- DoIP UDP/TCP port.
- TLS port.
- Config path.
- Log path.
- PCAP directory.
- Current process ID.

### 5.2 PCAP and Wireshark Support

The simulator shall expose a capture workflow designed for tester debugging.

Required UI fields:

- Recording status.
- Current PCAP file path.
- Capture size.
- DoIP decode port.
- TLS key log file path when TLS is enabled.
- Short Wireshark setup guidance.

Required backend behavior:

- Generate PCAP files in a predictable directory.
- Include scenario name in filename when recording is started from a test case.
- Support TLS key log export if TLS is enabled.
- Preserve enough packet metadata for Wireshark to parse DoIP/UDS reliably.

### 5.3 Scenario Presets

The simulator shall provide built-in scenario presets:

- Standard ECU.
- Slow ECU.
- NRC-heavy ECU.
- SecurityAccess ECU.
- TLS ECU.
- Routing activation failure ECU.
- Disconnect-prone ECU.
- Malformed frame ECU.

Each preset shall be selectable without editing raw JSON.

### 5.4 Test Case Runner

The simulator shall provide a case runner for compatibility testing.

Minimum Phase 2 cases:

1. UDP vehicle identification success.
2. Routing activation success.
3. Routing activation rejected by source address.
4. Read VIN DID success.
5. Write VIN DID success.
6. Unsupported DID returns NRC.
7. Session switch default to extended.
8. TesterPresent keeps session alive.
9. TesterPresent timeout falls back to default.
10. SecurityAccess success.
11. SecurityAccess invalid key.
12. SecurityAccess lockout.
13. DTC read active DTC.
14. DTC clear.
15. RoutineControl fixed response.
16. ResponsePending before final response.
17. Delayed UDS response.
18. TCP disconnect during request.
19. Malformed DoIP header.
20. TLS handshake success.
21. TLS missing client certificate.
22. TLS certificate validation failure.

Each case shall produce:

- Runtime events.
- Optional PCAP.
- Pass/fail notes for simulator-side expectations.

The simulator does not need to control the tester process in Phase 2. The tester developer may run their tester manually against the scenario.

### 5.5 Realtime Observation

Realtime observation shall remain always visible.

It shall show:

- Active connections.
- Last DoIP frame.
- Last UDS request.
- Last UDS response.
- Last fault applied.
- Capture status.
- Current scenario.

The event rail should help the user quickly correlate tester behavior with simulator-side events.

## 6. Non-Functional Requirements

### 6.1 Repeatability

Scenarios must be deterministic by default.

Random delays, random disconnects, and random NRC should be disabled unless explicitly configured.

### 6.2 Debuggability

Every important simulator decision shall produce an event:

- Routing activation accepted/rejected.
- UDS request received.
- UDS response sent.
- NRC produced.
- Fault applied.
- Timeout evaluated.
- Connection closed.
- PCAP started/stopped.
- TLS handshake accepted/rejected.

### 6.3 Local Installability

The MSI shall remain the primary delivery artifact.

After installation, a tester developer should be able to:

1. Start the simulator from the Start Menu.
2. Open `http://127.0.0.1:5080/`.
3. Select a scenario.
4. Start PCAP.
5. Run their tester.
6. Stop PCAP.
7. Inspect logs and captures.
8. Shut down the simulator from the UI.

### 6.4 Safety

The simulator shall only bind local configured ports and shall not require administrator privileges by default.

Any operation that terminates the runtime shall require confirmation.

## 7. Success Criteria

Phase 2 is successful when:

- A tester developer can install the MSI and run the simulator without source code.
- The Web Console can start and stop capture.
- The Web Console can shut down the runtime and release ports.
- Standard DoIP discovery and routing activation can be tested repeatedly.
- Core UDS tester flows can be tested repeatedly.
- At least 20 compatibility scenarios are available.
- Each scenario generates clear runtime events.
- PCAP files are useful in Wireshark for DoIP/UDS debugging.
- TLS captures can be decrypted in Wireshark when TLS key log is enabled.

## 8. Delivery Priorities

### P0

- Runtime shutdown from UI.
- Port/process/status visibility.
- PCAP reliability improvements.
- TLS key log export.
- Standard DoIP/UDS scenario presets.
- Compatibility test case runner skeleton.

### P1

- Complete 20+ tester compatibility cases.
- Better malformed DoIP and UDS fault injection.
- Scenario-named PCAP output.
- Wireshark guidance in UI.
- Config profile switching.

### P2

- Partial/sticky packet simulation.
- Enhanced DTC model.
- Enhanced Routine actions.
- Better ODX/PDX import for tester-relevant DID data.
- Exportable test report.

## 9. Key Risks

### 9.1 PCAP Accuracy

If PCAP output is not trusted, the tool will not serve its main Phase 2 purpose. Capture correctness is more important than adding more UI panels.

### 9.2 TLS Decryption

TLS traffic cannot be inspected in Wireshark without session secrets. TLS key log export is required for meaningful encrypted payload analysis.

### 9.3 Scope Creep

Full ODX/PDX, full ECU flashing, and multi-ECU gateway simulation can consume the phase without improving tester debugging. These should remain secondary unless required by a specific tester case.

### 9.4 Fault Injection Semantics

Faults must be deterministic and documented. Unclear random behavior will make tester debugging harder rather than easier.

## 10. Phase 2 Positioning

Phase 2 should position the simulator as:

> A local DoIP/UDS validation target for tester developers, with repeatable scenarios, fault injection, realtime observation, and Wireshark-ready capture evidence.

It should not position itself as:

> A complete OEM ECU engineering platform.

