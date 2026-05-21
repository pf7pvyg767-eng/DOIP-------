import type {
  ConnectionSnapshot,
  DidRuntimeSample,
  PcapRecordingStatus,
  RuntimeEvent,
  RuntimeMetricsSnapshot,
  RuntimeSummaryResponse,
} from "./api";

export type ConnectionStepId =
  | "udp-discovery"
  | "tcp-connect"
  | "routing-activation"
  | "uds-read-did";

export type ConnectionStepState = "not-started" | "waiting" | "active" | "passed" | "failed";

export interface RuntimeCockpitSnapshot {
  runtimeSummary: RuntimeSummaryResponse | null;
  connections: ConnectionSnapshot[];
  recentEvents: RuntimeEvent[];
  metrics: RuntimeMetricsSnapshot | null;
  didSamples: DidRuntimeSample[];
  pcapStatus: PcapRecordingStatus | null;
  runtimeSummaryError: string;
}

export interface ConnectionStepViewModel {
  id: ConnectionStepId;
  index: number;
  title: string;
  subtitle: string;
  state: ConnectionStepState;
}

export interface WorkflowEvidence {
  latestDoip: RuntimeEvent | null;
  latestUdsRequest: RuntimeEvent | null;
  latestUdsResponse: RuntimeEvent | null;
  latestDidRead: RuntimeEvent | null;
}

export const connectionStepOrder: ConnectionStepId[] = [
  "udp-discovery",
  "tcp-connect",
  "routing-activation",
  "uds-read-did",
];

export function buildConnectionSteps(snapshot: RuntimeCockpitSnapshot): ConnectionStepViewModel[] {
  const hasSummary = snapshot.runtimeSummary !== null && snapshot.runtimeSummaryError.length === 0;
  const hasOpenConnection = snapshot.connections.some((connection) => connection.state !== "closed");
  const hasRouting = snapshot.connections.some((connection) => connection.routingActivated);
  const hasUds = snapshot.recentEvents.some((event) =>
    event.name === "uds.request.received" ||
    event.name === "uds.response.sent" ||
    event.name === "uds.did.read");

  return [
    {
      id: "udp-discovery",
      index: 1,
      title: "UDP Discovery",
      subtitle: hasSummary ? "Vehicle identification endpoint is available" : "Waiting for runtime summary",
      state: hasSummary ? "passed" : "waiting",
    },
    {
      id: "tcp-connect",
      index: 2,
      title: "TCP Connect",
      subtitle: hasOpenConnection ? "Tester socket is open" : "Waiting for tester TCP connection",
      state: hasOpenConnection ? "passed" : hasSummary ? "waiting" : "not-started",
    },
    {
      id: "routing-activation",
      index: 3,
      title: "Routing Activation",
      subtitle: hasRouting ? "Routing activation accepted" : "Waiting for activation request",
      state: hasRouting ? "passed" : hasOpenConnection ? "active" : "not-started",
    },
    {
      id: "uds-read-did",
      index: 4,
      title: "UDS Read DID",
      subtitle: hasUds ? "UDS traffic observed" : "Ready for ReadDataByIdentifier",
      state: hasUds ? "active" : hasRouting ? "waiting" : "not-started",
    },
  ];
}

export function selectDefaultStep(steps: ConnectionStepViewModel[]): ConnectionStepId {
  return steps.find((step) => step.state === "active" || step.state === "waiting" || step.state === "failed")?.id
    ?? steps[steps.length - 1].id;
}

export function collectWorkflowEvidence(events: RuntimeEvent[]): WorkflowEvidence {
  return {
    latestDoip: findLatest(events, (event) => event.category === "doip"),
    latestUdsRequest: findLatest(events, (event) => event.name === "uds.request.received"),
    latestUdsResponse: findLatest(events, (event) => event.name === "uds.response.sent"),
    latestDidRead: findLatest(events, (event) => event.name === "uds.did.read"),
  };
}

export function buildCopyText(stepId: ConnectionStepId, snapshot: RuntimeCockpitSnapshot): string {
  const summary = snapshot.runtimeSummary;
  if (!summary) {
    return "Runtime summary is unavailable.";
  }

  if (stepId === "udp-discovery") {
    return `DoIP UDP discovery: ${summary.webApiListenAddress}:${summary.doipUdpPort}`;
  }

  if (stepId === "tcp-connect") {
    return `DoIP TCP connect: ${summary.webApiListenAddress}:${summary.doipTcpPort}`;
  }

  if (stepId === "routing-activation") {
    const tester = summary.testerSourceAddressWhitelist[0] ?? "0x0E00";
    return `Routing Activation: tester ${tester} -> ECU ${summary.ecuLogicalAddress}`;
  }

  return `UDS ReadDataByIdentifier: target ECU ${summary.ecuLogicalAddress}, request 22 F1 90`;
}

export function formatEventSummary(event: RuntimeEvent | null): string {
  if (!event) {
    return "Unavailable";
  }

  return `${event.name} at ${formatTime(event.timestamp)}`;
}

export function formatDidPreview(samples: DidRuntimeSample[]): string {
  const sample = samples.find((item) => typeof item.numericValue === "number") ?? samples[0];
  if (!sample) {
    return "No DID sample";
  }

  const value = typeof sample.numericValue === "number" ? sample.numericValue.toString() : sample.rawValue;
  return `${sample.did}: ${value}`;
}

function findLatest(events: RuntimeEvent[], predicate: (event: RuntimeEvent) => boolean): RuntimeEvent | null {
  return [...events].reverse().find(predicate) ?? null;
}

function formatTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleTimeString();
}
