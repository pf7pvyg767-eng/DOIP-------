<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import {
  loadDashboardState,
  loadRuntimeMetrics,
  type DashboardState,
  type RuntimeMetricsSnapshot,
} from "../api";
import ControlServicesPanel from "../components/ControlServicesPanel.vue";
import DidEditorPanel from "../components/DidEditorPanel.vue";
import DtcInjectionPanel from "../components/DtcInjectionPanel.vue";
import EventLogPanel from "../components/EventLogPanel.vue";
import FaultInjectionPanel from "../components/FaultInjectionPanel.vue";
import ImportPanel from "../components/ImportPanel.vue";
import MetricsPanel from "../components/MetricsPanel.vue";
import PcapRecordingPanel from "../components/PcapRecordingPanel.vue";
import RealtimeObservationPanel from "../components/RealtimeObservationPanel.vue";
import StatusPanel from "../components/StatusPanel.vue";

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
const activeWorkspace = ref<WorkspaceId>("overview");
const isLoading = ref(true);
const errorMessage = ref("");
let metricsTimer: number | undefined;

const activeWorkspaceDetails = computed(
  () => workspaces.find((workspace) => workspace.id === activeWorkspace.value) ?? workspaces[0],
);

onMounted(() => {
  void load();
  void refreshMetrics();
  metricsTimer = window.setInterval(() => void refreshMetrics(), 2000);
});

onBeforeUnmount(() => {
  if (metricsTimer !== undefined) {
    window.clearInterval(metricsTimer);
  }
});

async function load(): Promise<void> {
  isLoading.value = true;
  errorMessage.value = "";

  try {
    state.value = await loadDashboardState();
  } catch {
    state.value = null;
    errorMessage.value =
      "Dashboard data could not be loaded. Check that the backend API is running.";
  } finally {
    isLoading.value = false;
  }
}

async function refreshMetrics(): Promise<void> {
  try {
    metrics.value = await loadRuntimeMetrics();
  } catch {
    metrics.value = null;
  }
}

function activateWorkspace(id: WorkspaceId): void {
  activeWorkspace.value = id;
}

function formatMetric(value: number | undefined): string {
  return typeof value === "number" && Number.isFinite(value) ? value.toLocaleString() : "--";
}

function display(value: string | undefined): string {
  return value && value.trim().length > 0 ? value : "--";
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
            <DidEditorPanel />
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
