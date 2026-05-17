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
