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
