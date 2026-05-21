## Context

The simulator runtime already starts Web API, UDP DoIP, TCP DoIP, optional TLS, runtime events, metrics, and PCAP recording from the Host process. Shutdown is currently driven by Ctrl+C through a linked cancellation token. The Web Console can show runtime status and connection guidance, but it cannot request a controlled shutdown from the same UI.

Task-02 needs a small lifecycle control plane that crosses Host, Web API, and Web Console without changing DoIP/UDS protocol behavior.

## Goals / Non-Goals

**Goals:**

- Allow Web Console users to stop the current simulator runtime after explicit confirmation.
- Add a Web API shutdown endpoint that requests Host shutdown through an injected runtime signal.
- Reuse graceful shutdown so Web API, DoIP TCP/UDP/TLS listeners, event resources, and PCAP recorder cleanup run through existing disposal paths.
- Publish an auditable `system.shutdown.requested` runtime event before the process begins stopping.
- Show a clear stopping/disconnected UI state after shutdown is requested.

**Non-Goals:**

- No remote authentication or authorization model in this task.
- No configuration editing, diagnostic request sending, or protocol behavior changes.
- No supervisor/restart daemon. After shutdown, the Host process exits and must be started again externally.
- No per-port shutdown; this stops the whole simulator runtime.

## Decisions

### Runtime shutdown signal is owned by Host and exposed to Web API

Host will create a runtime shutdown coordinator or callback that wraps the existing linked cancellation token source. `WebApiApplication.Create` receives that dependency when the full Host runtime is constructed. Tests or standalone Web API construction can use a no-op/default implementation.

Rationale: Host owns the lifetime of Web API and DoIP servers, so Web API should request shutdown but not directly dispose Host resources.

Alternative considered: call `IHostApplicationLifetime.StopApplication()` only inside Web API. That can stop Kestrel, but it does not clearly communicate cancellation to Host-managed DoIP servers and cleanup sequencing.

### Shutdown endpoint returns before process exit

`POST /api/runtime/shutdown` should publish `system.shutdown.requested`, attempt any required cleanup such as stopping active PCAP recording, trigger the shutdown signal, and return a small accepted response when possible. The process may terminate soon after the response, so clients must tolerate either a success response or connection closure during shutdown.

Rationale: this matches real shutdown behavior and keeps UI resilient instead of depending on a long-lived response after the server is stopping.

Alternative considered: block the HTTP request until the process exits. That makes the caller experience unreliable and can leave the request hanging while sockets close.

### PCAP cleanup happens before triggering final cancellation

If PCAP recording is active, the shutdown handler should call the recorder stop operation before cancelling Host lifetime. Normal async disposal remains the fallback for any remaining cleanup.

Rationale: this gives the recorder a deterministic chance to flush and close the current file before server cancellation races begin.

Alternative considered: rely only on async disposal. This may work but is less explicit for a user-triggered shutdown path where preserving capture output matters.

### Web Console treats shutdown as a terminal state

The UI will show a confirmation dialog before calling the API. After confirmation it enters a `stopping` state, disables repeated shutdown actions, stops aggressive refresh loops where practical, and displays that the runtime is stopping or disconnected once API calls fail.

Rationale: backend unavailability after shutdown is success, not a dashboard failure.

Alternative considered: keep polling all dashboard APIs after shutdown. That creates noisy errors and makes the user think shutdown failed.

## Risks / Trade-offs

- Endpoint could be accidentally exposed beyond loopback if the Web API listen address is changed → Keep this task scoped to the existing deployment model and document that no auth model is added here.
- HTTP response may be interrupted by fast process shutdown → UI treats both accepted response and expected disconnect as shutdown progress.
- PCAP stop can fail while shutdown is requested → Publish/log the failure when possible and continue shutdown so the runtime is not stuck.
- Repeated shutdown clicks could create duplicate events → Make the shutdown request idempotent at the coordinator/UI level and disable the UI action once requested.
