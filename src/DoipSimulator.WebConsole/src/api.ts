export interface HealthResponse {
  status?: string;
  version?: string;
  startedAt?: string;
}

export interface SimulatorConfig {
  entity?: {
    vin?: string;
    eid?: string;
    gid?: string;
    logicalAddress?: string;
  };
  network?: {
    doipUdpPort?: number;
    doipTcpPort?: number;
    doipTlsPort?: number;
  };
  uds?: {
    routines?: RoutineSummary[];
  };
}

export interface ConfigSummary {
  vin: string;
  eid: string;
  gid: string;
  logicalAddress: string;
  doipUdpPort: string;
  doipTcpPort: string;
  doipTlsPort: string;
}

export interface DashboardState {
  health: HealthResponse;
  config: ConfigSummary;
}

export type RuntimeEventLevel = "info" | "warning" | "error";

export type RuntimeEventCategory =
  | "system"
  | "config"
  | "connection"
  | "doip"
  | "uds"
  | "state"
  | "fault"
  | "tls"
  | "pcap";

export interface RuntimeEvent {
  id: string;
  timestamp: string;
  level: RuntimeEventLevel;
  category: RuntimeEventCategory;
  name: string;
  message: string;
  connectionId?: string | null;
  data?: Record<string, unknown> | null;
}

export interface ConnectionSnapshot {
  connectionId: string;
  transport: string;
  remoteEndpoint: string;
  routingActivated: boolean;
  testerLogicalAddress?: string | null;
  ecuLogicalAddress?: string | null;
  connectedAt: string;
  state: string;
}

export interface EcuStateSnapshot {
  logicalAddress: string;
  currentSession: string;
  securityStateSummary: string;
  lastTesterPresentAt?: string | null;
  timing?: {
    timeoutEnabled: boolean;
    timeoutMs: number;
    lastAcceptedAt?: string | null;
    timeoutDeadline?: string | null;
    lastFallbackAt?: string | null;
    lastFallbackReason?: string | null;
    lastFallbackPreviousSession?: string | null;
    currentSession: string;
  };
}

export interface DidSummary {
  did: string;
  name?: string | null;
  valueEncoding: string;
  value: string;
  writable: boolean;
  expectedLength?: number | null;
  allowedWriteSessions: string[];
  requiredSecurityState?: string | null;
  permissionSummary: string;
}

export interface DidValueUpdateRequest {
  valueEncoding: "hex";
  value: string;
  persist: boolean;
}

export interface DtcSummary {
  code: string;
  name?: string | null;
  description?: string | null;
  status: string;
  active: boolean;
}

export interface DtcActivateRequest {
  status?: string | null;
  description?: string | null;
}

export interface RoutineSummary {
  routineId?: string;
  identifier?: string;
  name?: string | null;
  hasStartResponse?: boolean;
  hasStopResponse?: boolean;
  hasRequestResultsResponse?: boolean;
}

export interface CommunicationControlSummary {
  controlType: string;
  communicationType: string;
  lastChangedAt?: string | null;
  lastSource?: string | null;
}

export interface DtcSettingStateSummary {
  enabled: boolean;
  settingType: string;
  lastChangedAt?: string | null;
  lastSource?: string | null;
}

export interface ControlServicesSnapshot {
  routines: RoutineSummary[];
  communicationControl: CommunicationControlSummary;
  dtcSetting: DtcSettingStateSummary;
}

export interface PcapRecordingStatus {
  recording: boolean;
  filePath?: string | null;
  bytesWritten: number;
  maxBytes: number;
}

export interface FaultProfile {
  enabled: boolean;
  responseDelayMs: number;
  pauseResponses: boolean;
  routingActivationFailure: boolean;
  corruptNextDoipHeader: {
    inverseVersion: boolean;
    payloadLengthDelta: number;
  };
  nextNrc?: {
    serviceId: string;
    nrc?: string | null;
  } | null;
  nextCustomResponse?: {
    serviceId: string;
    responseBytes?: string | null;
  } | null;
}

export interface FaultRuntimeSnapshot {
  profile: FaultProfile;
  pauseResponses: boolean;
  hasPendingDoipHeaderFault: boolean;
  nextNrc?: FaultProfile["nextNrc"];
  nextCustomResponse?: FaultProfile["nextCustomResponse"];
}

export interface ImportReport {
  success: boolean;
  imported: {
    entityInfo: boolean;
    dids: number;
    dtcs: number;
    routines: number;
  };
  skipped: Array<{
    path: string;
    reason: string;
  }>;
  errors: Array<{
    path: string;
    message: string;
  }>;
  saved: boolean;
}

const unavailable = "Unavailable";

export async function loadDashboardState(): Promise<DashboardState> {
  const [health, config] = await Promise.all([
    getJson<HealthResponse>("/api/health"),
    getJson<SimulatorConfig>("/api/config"),
  ]);

  return {
    health,
    config: toConfigSummary(config),
  };
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, {
    headers: {
      Accept: "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Request failed: ${path} returned ${response.status}`);
  }

  return (await response.json()) as T;
}

export async function loadRecentEvents(limit = 200, category = ""): Promise<RuntimeEvent[]> {
  const parameters = new URLSearchParams();
  parameters.set("limit", limit.toString());
  if (category) {
    parameters.set("category", category);
  }

  return getJson<RuntimeEvent[]>(`/api/events/recent?${parameters.toString()}`);
}

export async function loadConnections(): Promise<ConnectionSnapshot[]> {
  return getJson<ConnectionSnapshot[]>("/api/connections");
}

export async function loadEcuState(): Promise<EcuStateSnapshot> {
  return getJson<EcuStateSnapshot>("/api/ecu/state");
}

export async function loadDids(): Promise<DidSummary[]> {
  return getJson<DidSummary[]>("/api/dids");
}

export async function updateDidValue(did: string, body: DidValueUpdateRequest): Promise<void> {
  const response = await fetch(`/api/dids/${encodeURIComponent(did.replace(/^0x/i, ""))}/value`, {
    method: "PUT",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    let message = `DID write failed with HTTP ${response.status}.`;
    try {
      const error = (await response.json()) as { message?: string };
      if (error.message) {
        message = error.message;
      }
    } catch {
    }

    throw new Error(message);
  }
}

export async function loadDtcs(): Promise<DtcSummary[]> {
  return getJson<DtcSummary[]>("/api/dtcs");
}

export async function loadControlServices(): Promise<ControlServicesSnapshot> {
  return getJson<ControlServicesSnapshot>("/api/control-services");
}

export async function loadPcapStatus(): Promise<PcapRecordingStatus> {
  return getJson<PcapRecordingStatus>("/api/pcap/status");
}

export async function startPcapRecording(): Promise<PcapRecordingStatus> {
  return postPcapOperation("start");
}

export async function stopPcapRecording(): Promise<PcapRecordingStatus> {
  return postPcapOperation("stop");
}

export async function loadFaults(): Promise<FaultRuntimeSnapshot> {
  return getJson<FaultRuntimeSnapshot>("/api/faults");
}

export async function updateFaultProfile(profile: FaultProfile): Promise<FaultRuntimeSnapshot> {
  const response = await fetch("/api/faults", {
    method: "PUT",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(profile),
  });

  if (!response.ok) {
    throw new Error(await readErrorMessage(response, "Fault profile update failed."));
  }

  return (await response.json()) as FaultRuntimeSnapshot;
}

export async function triggerFaultDisconnect(connectionId: string): Promise<void> {
  const response = await fetch("/api/faults/actions/disconnect", {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ connectionId }),
  });

  if (!response.ok) {
    throw new Error(await readErrorMessage(response, "Manual disconnect failed."));
  }
}

export async function configureNextNrc(serviceId: string, nrc: string): Promise<FaultRuntimeSnapshot> {
  const response = await fetch("/api/faults/actions/next-nrc", {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ serviceId, nrc }),
  });

  if (!response.ok) {
    throw new Error(await readErrorMessage(response, "Next NRC setup failed."));
  }

  return (await response.json()) as FaultRuntimeSnapshot;
}

export async function uploadDiagnosticImport(kind: "odx" | "pdx", file: File): Promise<ImportReport> {
  const body = new FormData();
  body.append("file", file);
  const response = await fetch(`/api/import/${kind}`, {
    method: "POST",
    headers: {
      Accept: "application/json",
    },
    body,
  });

  if (!response.ok) {
    throw new Error(await readErrorMessage(response, "Diagnostic import failed."));
  }

  return (await response.json()) as ImportReport;
}

export async function activateDtc(code: string, body: DtcActivateRequest): Promise<void> {
  await postDtcOperation(code, "activate", body);
}

export async function clearDtc(code: string): Promise<void> {
  await postDtcOperation(code, "clear");
}

async function postDtcOperation(code: string, operation: "activate" | "clear", body?: DtcActivateRequest): Promise<void> {
  const response = await fetch(`/api/dtcs/${encodeURIComponent(code.replace(/^0x/i, ""))}/${operation}`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      ...(body ? { "Content-Type": "application/json" } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    let message = `DTC ${operation} failed with HTTP ${response.status}.`;
    try {
      const error = (await response.json()) as { message?: string };
      if (error.message) {
        message = error.message;
      }
    } catch {
    }

    throw new Error(message);
  }
}

async function postPcapOperation(operation: "start" | "stop"): Promise<PcapRecordingStatus> {
  const response = await fetch(`/api/pcap/${operation}`, {
    method: "POST",
    headers: {
      Accept: "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`PCAP ${operation} failed with HTTP ${response.status}.`);
  }

  return (await response.json()) as PcapRecordingStatus;
}

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  try {
    const error = (await response.json()) as {
      message?: string;
      errors?: { path?: string; message?: string }[];
    };
    if (error.errors?.length) {
      return error.errors.map((item) => `${item.path ?? "field"}: ${item.message ?? "invalid"}`).join("; ");
    }

    return error.message ?? `${fallback} HTTP ${response.status}.`;
  } catch {
    return `${fallback} HTTP ${response.status}.`;
  }
}

export function createRuntimeEventSocket(): WebSocket {
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  return new WebSocket(`${protocol}//${window.location.host}/api/events/stream`);
}

function toConfigSummary(config: SimulatorConfig): ConfigSummary {
  return {
    vin: normalizeText(config.entity?.vin),
    eid: normalizeText(config.entity?.eid),
    gid: normalizeText(config.entity?.gid),
    logicalAddress: normalizeText(config.entity?.logicalAddress),
    doipUdpPort: normalizeNumber(config.network?.doipUdpPort),
    doipTcpPort: normalizeNumber(config.network?.doipTcpPort),
    doipTlsPort: normalizeNumber(config.network?.doipTlsPort),
  };
}

function normalizeText(value: string | undefined): string {
  return value && value.trim().length > 0 ? value : unavailable;
}

function normalizeNumber(value: number | undefined): string {
  return typeof value === "number" && Number.isFinite(value)
    ? value.toString()
    : unavailable;
}
