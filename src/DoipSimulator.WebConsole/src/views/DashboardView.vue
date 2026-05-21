<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import {
  createRuntimeEventSocket,
  loadConnections,
  loadDashboardState,
  loadDidSamples,
  loadPcapStatus,
  loadRecentEvents,
  loadRuntimeMetrics,
  loadRuntimeSummary,
  requestRuntimeShutdown,
  type ConnectionSnapshot,
  type DashboardState,
  type DidRuntimeSample,
  type PcapRecordingStatus,
  type RuntimeEvent,
  type RuntimeMetricsSnapshot,
  type RuntimeSummaryResponse,
} from "../api";
import ControlServicesPanel from "../components/ControlServicesPanel.vue";
import DidDataPanel from "../components/DidDataPanel.vue";
import DtcInjectionPanel from "../components/DtcInjectionPanel.vue";
import EventLogPanel from "../components/EventLogPanel.vue";
import FaultInjectionPanel from "../components/FaultInjectionPanel.vue";
import ImportPanel from "../components/ImportPanel.vue";
import MetricsPanel from "../components/MetricsPanel.vue";
import PcapRecordingPanel from "../components/PcapRecordingPanel.vue";
import RealtimeObservationPanel from "../components/RealtimeObservationPanel.vue";
import RuntimeCockpitPanel from "../components/RuntimeCockpitPanel.vue";
import StatusPanel from "../components/StatusPanel.vue";
import type { RuntimeCockpitSnapshot } from "../connectionWorkflow";

type WorkspaceId = "overview" | "diagnostics" | "data" | "faults" | "capture" | "import" | "events";

interface Workspace {
  id: WorkspaceId;
  label: string;
  kicker: string;
  title: string;
  description: string;
}

const workspaces: Workspace[] = [
  {
    id: "overview",
    label: "Overview",
    kicker: "Runtime",
    title: "System overview",
    description: "Service state, telemetry and simulator identity in one control surface.",
  },
  {
    id: "diagnostics",
    label: "Diagnostics",
    kicker: "DoIP / UDS",
    title: "Live diagnostic traffic",
    description: "Connections, DoIP frames and UDS messages with realtime event replay.",
  },
  {
    id: "data",
    label: "Data",
    kicker: "DID / DTC",
    title: "Diagnostic data controls",
    description: "Edit runtime DID values, inject DTC state and inspect control services.",
  },
  {
    id: "faults",
    label: "Faults",
    kicker: "Injection",
    title: "Fault strategy bench",
    description: "Apply manual response delays, NRC overrides, disconnects and malformed DoIP cases.",
  },
  {
    id: "capture",
    label: "Capture",
    kicker: "PCAP",
    title: "Packet capture",
    description: "Start and stop simulator-side packet recording.",
  },
  {
    id: "import",
    label: "Import",
    kicker: "ODX / PDX",
    title: "Diagnostic data import",
    description: "Load supported ODX and PDX subsets into the active simulator configuration.",
  },
  {
    id: "events",
    label: "Events",
    kicker: "Audit",
    title: "Runtime event log",
    description: "Review the full event stream with level and category filters.",
  },
];

const state = ref<DashboardState | null>(null);
const metrics = ref<RuntimeMetricsSnapshot | null>(null);
const runtimeSummary = ref<RuntimeSummaryResponse | null>(null);
const runtimeSummaryError = ref("");
const phaseConnections = ref<ConnectionSnapshot[]>([]);
const recentPhaseEvents = ref<RuntimeEvent[]>([]);
const didSamples = ref<DidRuntimeSample[]>([]);
const pcapStatus = ref<PcapRecordingStatus | null>(null);
const udsTrafficActive = ref(false);
const activeWorkspace = ref<WorkspaceId>("overview");
const isLoading = ref(true);
const errorMessage = ref("");
const shutdownStatus = ref<"idle" | "stopping" | "stopped" | "failed">("idle");
const shutdownMessage = ref("");
let metricsTimer: number | undefined;
let phaseSocket: WebSocket | null = null;
let phaseReconnectTimer: number | undefined;
let disposed = false;

const activeWorkspaceDetails = computed(
  () => workspaces.find((workspace) => workspace.id === activeWorkspace.value) ?? workspaces[0],
);
const runtimePhase = computed(() => deriveRuntimePhase());
const shutdownButtonLabel = computed(() => {
  if (shutdownStatus.value === "stopping") {
    return "Stopping...";
  }

  if (shutdownStatus.value === "stopped") {
    return "Runtime stopped";
  }

  return "Stop runtime";
});
const shutdownStateClass = computed(() => ({
  "inline-state": true,
  "inline-state--warning": shutdownStatus.value === "stopping",
  "inline-state--success": shutdownStatus.value === "stopped",
  "inline-state--error": shutdownStatus.value === "failed",
}));
const cockpitSnapshot = computed<RuntimeCockpitSnapshot>(() => ({
  runtimeSummary: runtimeSummary.value,
  connections: phaseConnections.value,
  recentEvents: recentPhaseEvents.value,
  metrics: metrics.value,
  didSamples: didSamples.value,
  pcapStatus: pcapStatus.value,
  runtimeSummaryError: runtimeSummaryError.value,
}));

onMounted(() => {
  void load();
  void refreshMetrics();
  void refreshRuntimeInputs();
  connectPhaseEvents();
  metricsTimer = window.setInterval(() => void refreshMetrics(), 2000);
});

onBeforeUnmount(() => {
  disposed = true;
  if (metricsTimer !== undefined) {
    window.clearInterval(metricsTimer);
  }

  if (phaseReconnectTimer !== undefined) {
    window.clearTimeout(phaseReconnectTimer);
  }

  phaseSocket?.close();
});

async function load(): Promise<void> {
  isLoading.value = true;
  errorMessage.value = "";
  runtimeSummaryError.value = "";

  try {
    state.value = await loadDashboardState();
  } catch {
    if (shutdownStatus.value === "stopping" || shutdownStatus.value === "stopped") {
      markRuntimeStopped();
    } else {
      state.value = null;
      errorMessage.value =
        "Dashboard data could not be loaded. Check that the backend API is running.";
    }
  } finally {
    isLoading.value = false;
  }

  await refreshRuntimeSummary();
}

async function refreshMetrics(): Promise<void> {
  if (shutdownStatus.value === "stopping" || shutdownStatus.value === "stopped") {
    return;
  }

  try {
    metrics.value = await loadRuntimeMetrics();
  } catch {
    metrics.value = null;
  }
}

async function refreshRuntimeSummary(): Promise<void> {
  if (shutdownStatus.value === "stopping" || shutdownStatus.value === "stopped") {
    return;
  }

  try {
    runtimeSummary.value = await loadRuntimeSummary();
    runtimeSummaryError.value = "";
  } catch {
    runtimeSummary.value = null;
    runtimeSummaryError.value = "Runtime summary could not be loaded.";
  }
}

async function refreshRuntimeInputs(): Promise<void> {
  await Promise.all([
    refreshRuntimeSummary(),
    refreshPhaseSnapshots(),
    refreshCockpitEvidence(),
  ]);
}

async function refreshPhaseSnapshots(): Promise<void> {
  if (shutdownStatus.value === "stopping" || shutdownStatus.value === "stopped") {
    return;
  }

  try {
    const [connections, recentEvents] = await Promise.all([
      loadConnections(),
      loadRecentEvents(100),
    ]);
    phaseConnections.value = connections;
    recentPhaseEvents.value = recentEvents;
    udsTrafficActive.value = recentEvents.some((event) =>
      event.name === "uds.request.received" || event.name === "uds.response.sent");
  } catch {
    phaseConnections.value = [];
    recentPhaseEvents.value = [];
  }
}

async function refreshCockpitEvidence(): Promise<void> {
  if (shutdownStatus.value === "stopping" || shutdownStatus.value === "stopped") {
    return;
  }

  try {
    const [samples, pcap] = await Promise.all([
      loadDidSamples(),
      loadPcapStatus(),
    ]);
    didSamples.value = samples;
    pcapStatus.value = pcap;
  } catch {
    didSamples.value = [];
    pcapStatus.value = null;
  }
}

function connectPhaseEvents(): void {
  if (disposed || shutdownStatus.value === "stopping" || shutdownStatus.value === "stopped") {
    return;
  }

  phaseSocket = createRuntimeEventSocket();
  phaseSocket.addEventListener("open", () => {
    void refreshPhaseSnapshots();
  });
  phaseSocket.addEventListener("message", (message) => {
    try {
      applyPhaseEvent(JSON.parse(message.data) as RuntimeEvent);
    } catch {
    }
  });
  phaseSocket.addEventListener("close", schedulePhaseReconnect);
  phaseSocket.addEventListener("error", schedulePhaseReconnect);
}

function schedulePhaseReconnect(): void {
  if (
    disposed ||
    phaseReconnectTimer !== undefined ||
    shutdownStatus.value === "stopping" ||
    shutdownStatus.value === "stopped"
  ) {
    return;
  }

  phaseSocket = null;
  phaseReconnectTimer = window.setTimeout(() => {
    phaseReconnectTimer = undefined;
    void refreshPhaseSnapshots();
    connectPhaseEvents();
  }, 2000);
}

function applyPhaseEvent(event: RuntimeEvent): void {
  recentPhaseEvents.value = [event, ...recentPhaseEvents.value].slice(0, 100);

  if (event.name === "uds.did.read") {
    void refreshCockpitEvidence();
  }

  if (event.name === "uds.request.received" || event.name === "uds.response.sent") {
    udsTrafficActive.value = true;
    return;
  }

  if (
    event.name === "connection.opened" ||
    event.name === "connection.closed" ||
    event.name === "doip.tcp.routing_activation.succeeded"
  ) {
    void refreshPhaseSnapshots();
  }
}

function activateWorkspace(id: WorkspaceId): void {
  activeWorkspace.value = id;
}

async function confirmRuntimeShutdown(): Promise<void> {
  if (shutdownStatus.value === "stopping" || shutdownStatus.value === "stopped") {
    return;
  }

  const confirmed = window.confirm("Stop the simulator runtime and release Web API / DoIP ports?");
  if (!confirmed) {
    return;
  }

  shutdownStatus.value = "stopping";
  shutdownMessage.value = "Shutdown request is being sent.";

  try {
    await requestRuntimeShutdown();
    shutdownMessage.value = "Shutdown request accepted. Waiting for the runtime to disconnect.";
    stopRuntimeRefresh();
    void waitForRuntimeDisconnect();
  } catch {
    const disconnected = await isBackendDisconnected();
    if (disconnected) {
      markRuntimeStopped();
      return;
    }

    shutdownStatus.value = "failed";
    shutdownMessage.value = "Shutdown request failed before the runtime accepted it.";
  }
}

function stopRuntimeRefresh(): void {
  if (metricsTimer !== undefined) {
    window.clearInterval(metricsTimer);
    metricsTimer = undefined;
  }

  if (phaseReconnectTimer !== undefined) {
    window.clearTimeout(phaseReconnectTimer);
    phaseReconnectTimer = undefined;
  }

  phaseSocket?.close();
  phaseSocket = null;
}

async function waitForRuntimeDisconnect(): Promise<void> {
  const deadline = Date.now() + 6000;
  while (Date.now() < deadline) {
    await delay(500);
    if (await isBackendDisconnected()) {
      markRuntimeStopped();
      return;
    }
  }

  shutdownMessage.value = "Shutdown request accepted. The runtime may still be stopping.";
}

async function isBackendDisconnected(): Promise<boolean> {
  try {
    const response = await fetch("/api/health", {
      cache: "no-store",
      headers: {
        Accept: "application/json",
      },
    });
    return !response.ok;
  } catch {
    return true;
  }
}

function markRuntimeStopped(): void {
  shutdownStatus.value = "stopped";
  shutdownMessage.value = "Runtime stopped. Web API and DoIP ports should now be released.";
  stopRuntimeRefresh();
}

function delay(milliseconds: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, milliseconds));
}

function formatMetric(value: number | undefined): string {
  return typeof value === "number" && Number.isFinite(value) ? value.toLocaleString() : "--";
}

function display(value: string | undefined): string {
  return value && value.trim().length > 0 ? value : "--";
}

function displayNullable(value: string | null | undefined): string {
  return value && value.trim().length > 0 ? value : "Unavailable";
}

function formatDateTime(value: string | undefined): string {
  if (!value) {
    return "Unavailable";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function formatList(values: string[] | undefined): string {
  return values && values.length > 0 ? values.join(", ") : "Unavailable";
}

function deriveRuntimePhase(): { label: string; detail: string; state: string } {
  if (udsTrafficActive.value) {
    return {
      label: "UDS Traffic Active",
      detail: "Diagnostic requests or responses have been observed.",
      state: "active",
    };
  }

  if (phaseConnections.value.some((connection) => connection.routingActivated)) {
    return {
      label: "Routing Activated",
      detail: "A tester has completed DoIP Routing Activation.",
      state: "activated",
    };
  }

  if (phaseConnections.value.some((connection) => connection.state !== "closed")) {
    return {
      label: "TCP Connected",
      detail: "A tester connection is open and waiting for Routing Activation.",
      state: "connected",
    };
  }

  return {
    label: "Waiting for DoIP Discovery",
    detail: "API Ready. No active tester connection is currently reported.",
    state: "waiting",
  };
}
</script>

<template>
  <main class="app-shell">
    <aside class="app-sidebar" aria-label="Workspace navigation">
      <div class="brand-block">
        <span class="brand-mark">DS</span>
        <div>
          <p class="eyebrow">DOIP Simulator</p>
          <h1>Control Desk</h1>
        </div>
      </div>

      <nav class="workspace-nav">
        <button
          v-for="workspace in workspaces"
          :key="workspace.id"
          type="button"
          :class="{ 'is-active': activeWorkspace === workspace.id }"
          @click="activateWorkspace(workspace.id)"
        >
          <span>{{ workspace.label }}</span>
          <small>{{ workspace.kicker }}</small>
        </button>
      </nav>
    </aside>

    <section class="app-main">
      <header class="app-topbar">
        <div class="topbar-title">
          <p class="eyebrow">{{ activeWorkspaceDetails.kicker }}</p>
          <h2>{{ activeWorkspaceDetails.title }}</h2>
          <p>{{ activeWorkspaceDetails.description }}</p>
        </div>

        <dl class="telemetry-strip" aria-label="Runtime telemetry">
          <div>
            <dt>Service</dt>
            <dd>
              <span class="status-pill">{{ display(state?.health.status) }}</span>
            </dd>
          </div>
          <div>
            <dt>VIN</dt>
            <dd>{{ state?.config.vin ?? "--" }}</dd>
          </div>
          <div>
            <dt>Logical</dt>
            <dd>{{ state?.config.logicalAddress ?? "--" }}</dd>
          </div>
          <div>
            <dt>DoIP</dt>
            <dd>{{ state?.config.doipTcpPort ?? "--" }}</dd>
          </div>
          <div>
            <dt>Active</dt>
            <dd>{{ formatMetric(metrics?.connections.active) }}</dd>
          </div>
          <div>
            <dt>UDS</dt>
            <dd>{{ metrics?.throughput.udsRequestsPerSecond?.toFixed(1) ?? "--" }}/s</dd>
          </div>
        </dl>
      </header>

      <section class="workspace-main">
        <section v-if="isLoading" class="state-panel" aria-live="polite">
          <p class="eyebrow">Loading</p>
          <h2>Loading dashboard data</h2>
          <p>Reading service health and simulator configuration.</p>
        </section>

        <section v-else-if="errorMessage" class="state-panel state-panel--error" role="alert">
          <p class="eyebrow">Error</p>
          <h2>Backend unavailable</h2>
          <p>{{ errorMessage }}</p>
        </section>

        <template v-else-if="state">
          <div v-if="activeWorkspace === 'overview'" class="workspace-stack">
            <RuntimeCockpitPanel
              :snapshot="cockpitSnapshot"
              :runtime-phase="runtimePhase"
              :shutdown-button-label="shutdownButtonLabel"
              :shutdown-status="shutdownStatus"
              :shutdown-message="shutdownMessage"
              @shutdown="confirmRuntimeShutdown"
            />

            <StatusPanel :health="state.health" />
            <MetricsPanel />
            <section class="section" aria-labelledby="config-summary-title">
              <div class="section__heading">
                <p class="eyebrow">Configuration</p>
                <h2 id="config-summary-title">Current simulator summary</h2>
              </div>

              <dl class="facts facts--config">
                <div class="fact">
                  <dt>VIN</dt>
                  <dd>{{ state.config.vin }}</dd>
                </div>
                <div class="fact">
                  <dt>EID</dt>
                  <dd>{{ state.config.eid }}</dd>
                </div>
                <div class="fact">
                  <dt>GID</dt>
                  <dd>{{ state.config.gid }}</dd>
                </div>
                <div class="fact">
                  <dt>Logical address</dt>
                  <dd>{{ state.config.logicalAddress }}</dd>
                </div>
                <div class="fact">
                  <dt>DoIP UDP port</dt>
                  <dd>{{ state.config.doipUdpPort }}</dd>
                </div>
                <div class="fact">
                  <dt>DoIP TCP port</dt>
                  <dd>{{ state.config.doipTcpPort }}</dd>
                </div>
                <div class="fact">
                  <dt>DoIP TLS port</dt>
                  <dd>{{ state.config.doipTlsPort }}</dd>
                </div>
              </dl>
            </section>
          </div>

          <RealtimeObservationPanel v-else-if="activeWorkspace === 'diagnostics'" />

          <div v-else-if="activeWorkspace === 'data'" class="workspace-stack">
            <DidDataPanel />
            <DtcInjectionPanel />
            <ControlServicesPanel />
          </div>

          <FaultInjectionPanel v-else-if="activeWorkspace === 'faults'" />

          <PcapRecordingPanel v-else-if="activeWorkspace === 'capture'" />

          <ImportPanel v-else-if="activeWorkspace === 'import'" />

          <EventLogPanel v-else-if="activeWorkspace === 'events'" />
        </template>
      </section>
    </section>

    <RealtimeObservationPanel compact />
  </main>
</template>
