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
