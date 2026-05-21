<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import {
  createRuntimeEventSocket,
  loadConnections,
  loadEcuState,
  loadRecentEvents,
  type ConnectionSnapshot,
  type EcuStateSnapshot,
  type RuntimeEvent,
} from "../api";

interface TraceRow {
  id: string;
  timestamp: string;
  direction: string;
  connectionId: string;
  name: string;
  summary: string;
}

const props = withDefaults(defineProps<{
  compact?: boolean;
}>(), {
  compact: false,
});

const maxTraceRows = 500;
const reconnectDelayMs = 2000;
const connections = ref<ConnectionSnapshot[]>([]);
const ecuState = ref<EcuStateSnapshot | null>(null);
const doipRows = ref<TraceRow[]>([]);
const udsRows = ref<TraceRow[]>([]);
const connectionFilter = ref("");
const directionFilter = ref("");
const keywordFilter = ref("");
const streamState = ref("connecting");
const errorMessage = ref("");
let socket: WebSocket | null = null;
let reconnectTimer: number | undefined;
let disposed = false;

const filteredConnections = computed(() => {
  const needle = connectionFilter.value.trim().toLowerCase();
  if (!needle) {
    return connections.value;
  }

  return connections.value.filter((connection) =>
    [
      connection.connectionId,
      connection.transport,
      connection.remoteEndpoint,
      connection.testerLogicalAddress,
      connection.ecuLogicalAddress,
      connection.state,
    ]
      .filter(Boolean)
      .some((value) => String(value).toLowerCase().includes(needle)),
  );
});

const filteredDoipRows = computed(() => filterRows(doipRows.value));
const filteredUdsRows = computed(() => filterRows(udsRows.value));
const railDoipRows = computed(() => doipRows.value.slice(0, 10));
const railUdsRows = computed(() => udsRows.value.slice(0, 10));
const activeConnections = computed(() => connections.value.filter((connection) => connection.state !== "closed"));
const headingId = computed(() => props.compact ? "observation-rail-title" : "observation-title");

onMounted(async () => {
  await refreshSnapshots();
  connect();
});

onBeforeUnmount(() => {
  disposed = true;
  if (reconnectTimer !== undefined) {
    window.clearTimeout(reconnectTimer);
  }

  socket?.close();
});

async function refreshSnapshots(): Promise<void> {
  errorMessage.value = "";
  try {
    const [connectionSnapshot, ecuSnapshot, recentEvents] = await Promise.all([
      loadConnections(),
      loadEcuState(),
      loadRecentEvents(),
    ]);
    connections.value = connectionSnapshot;
    ecuState.value = ecuSnapshot;
    recentEvents.forEach(applyRuntimeEvent);
  } catch {
    errorMessage.value = "Observation snapshots could not be loaded.";
  }
}

function connect(): void {
  if (disposed) {
    return;
  }

  streamState.value = "connecting";
  socket = createRuntimeEventSocket();
  socket.addEventListener("open", () => {
    streamState.value = "live";
    void refreshSnapshots();
  });
  socket.addEventListener("message", (message) => {
    try {
      applyRuntimeEvent(JSON.parse(message.data) as RuntimeEvent);
    } catch {
      errorMessage.value = "A realtime observation event could not be parsed.";
    }
  });
  socket.addEventListener("close", scheduleReconnect);
  socket.addEventListener("error", () => {
    streamState.value = "disconnected";
  });
}

function scheduleReconnect(): void {
  if (disposed) {
    return;
  }

  socket = null;
  streamState.value = "disconnected";
  reconnectTimer = window.setTimeout(() => {
    void refreshSnapshots();
    connect();
  }, reconnectDelayMs);
}

function applyRuntimeEvent(event: RuntimeEvent): void {
  if (event.name === "connection.opened") {
    upsertConnection(toConnectionSnapshot(event, "open"));
    return;
  }

  if (event.name === "connection.closed") {
    upsertConnection(toConnectionSnapshot(event, "closed"));
    return;
  }

  if (event.name === "doip.tcp.routing_activation.succeeded") {
    upsertConnection(toConnectionSnapshot(event, "open", true));
    return;
  }

  if (event.name === "doip.frame.received" || event.name === "doip.frame.sent") {
    appendTraceRow(doipRows, toTraceRow(event, event.name === "doip.frame.sent" ? "sent" : "received"));
    return;
  }

  if (event.name === "uds.request.received" || event.name === "uds.response.sent") {
    appendTraceRow(udsRows, toTraceRow(event, event.name === "uds.response.sent" ? "response" : "request"));
    return;
  }

  if (event.name === "state.session.changed" || event.name === "uds.session.changed") {
    ecuState.value = {
      logicalAddress: readString(event.data, "ecuLogicalAddress", ecuState.value?.logicalAddress ?? "Unavailable"),
      currentSession: readString(event.data, "newSession", readString(event.data, "currentSession", "Unavailable")),
      securityStateSummary: ecuState.value?.securityStateSummary ?? "locked",
      lastTesterPresentAt: ecuState.value?.lastTesterPresentAt ?? null,
      timing: ecuState.value?.timing,
    };
  }
}

function toConnectionSnapshot(
  event: RuntimeEvent,
  fallbackState: string,
  forceRoutingActivated = false,
): ConnectionSnapshot {
  return {
    connectionId: readString(event.data, "connectionId", event.connectionId ?? "Unavailable"),
    transport: readString(event.data, "transport", "tcp"),
    remoteEndpoint: readString(event.data, "remoteEndpoint", "Unavailable"),
    routingActivated: readBoolean(event.data, "routingActivated", forceRoutingActivated),
    testerLogicalAddress: readNullableString(event.data, "testerLogicalAddress"),
    ecuLogicalAddress: readNullableString(event.data, "ecuLogicalAddress"),
    connectedAt: readString(event.data, "connectedAt", event.timestamp),
    state: readString(event.data, "state", fallbackState),
  };
}

function toTraceRow(event: RuntimeEvent, fallbackDirection: string): TraceRow {
  const name = readString(
    event.data,
    "payloadTypeName",
    readString(event.data, "serviceId", readString(event.data, "responseSid", event.name)),
  );

  return {
    id: event.id,
    timestamp: event.timestamp,
    direction: readString(event.data, "direction", fallbackDirection),
    connectionId: readString(event.data, "connectionId", event.connectionId ?? "Unavailable"),
    name,
    summary: readString(
      event.data,
      "payloadSummary",
      readString(event.data, "byteSummary", readString(event.data, "responseType", event.message)),
    ),
  };
}

function upsertConnection(next: ConnectionSnapshot): void {
  const index = connections.value.findIndex((connection) => connection.connectionId === next.connectionId);
  if (index < 0) {
    connections.value = [next, ...connections.value];
    return;
  }

  const current = connections.value[index];
  connections.value = [
    ...connections.value.slice(0, index),
    { ...current, ...next },
    ...connections.value.slice(index + 1),
  ];
}

function appendTraceRow(target: typeof doipRows, row: TraceRow): void {
  target.value = [row, ...target.value.filter((item) => item.id !== row.id)].slice(0, maxTraceRows);
}

function filterRows(rows: TraceRow[]): TraceRow[] {
  const connectionNeedle = connectionFilter.value.trim().toLowerCase();
  const keywordNeedle = keywordFilter.value.trim().toLowerCase();

  return rows.filter((row) => {
    if (connectionNeedle && !row.connectionId.toLowerCase().includes(connectionNeedle)) {
      return false;
    }

    if (directionFilter.value && row.direction !== directionFilter.value) {
      return false;
    }

    if (!keywordNeedle) {
      return true;
    }

    return [row.name, row.summary, row.connectionId, row.direction]
      .some((value) => value.toLowerCase().includes(keywordNeedle));
  });
}

function readString(
  data: Record<string, unknown> | null | undefined,
  key: string,
  fallback: string,
): string {
  const value = data?.[key];
  return typeof value === "string" && value.trim().length > 0 ? value : fallback;
}

function readNullableString(data: Record<string, unknown> | null | undefined, key: string): string | null {
  const value = data?.[key];
  return typeof value === "string" && value.trim().length > 0 ? value : null;
}

function readBoolean(
  data: Record<string, unknown> | null | undefined,
  key: string,
  fallback: boolean,
): boolean {
  const value = data?.[key];
  return typeof value === "boolean" ? value : fallback;
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) {
    return "Unavailable";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleTimeString();
}

function formatTimingStatus(state: EcuStateSnapshot | null): string {
  if (!state?.timing) {
    return "Unavailable";
  }

  return state.timing.timeoutEnabled
    ? `${state.timing.timeoutMs} ms`
    : "disabled";
}

function formatFallbackStatus(state: EcuStateSnapshot | null): string {
  if (!state?.timing?.lastFallbackAt) {
    return "None";
  }

  const reason = state.timing.lastFallbackReason ?? "timeout";
  const previous = state.timing.lastFallbackPreviousSession ?? "unknown";
  return `${formatTimestamp(state.timing.lastFallbackAt)} ${previous} -> default (${reason})`;
}
</script>

<template>
  <section
    class="section observation"
    :class="{ 'observation--rail': props.compact }"
    :aria-labelledby="headingId"
  >
    <div class="section__heading section__heading--row">
      <div>
        <p class="eyebrow">Diagnostics</p>
        <h2 :id="headingId">{{ props.compact ? "Realtime rail" : "Realtime observation" }}</h2>
      </div>
      <span class="connection-pill" :data-state="streamState">{{ streamState }}</span>
    </div>

    <p v-if="errorMessage" class="inline-state inline-state--error">{{ errorMessage }}</p>

    <div class="facts facts--ecu" aria-label="ECU state">
      <div class="fact">
        <dt>ECU logical address</dt>
        <dd>{{ ecuState?.logicalAddress ?? "Unavailable" }}</dd>
      </div>
      <div class="fact">
        <dt>Session</dt>
        <dd>{{ ecuState?.currentSession ?? "Unavailable" }}</dd>
      </div>
      <div class="fact">
        <dt>Security</dt>
        <dd>{{ ecuState?.securityStateSummary ?? "Unavailable" }}</dd>
      </div>
      <div class="fact">
        <dt>TesterPresent</dt>
        <dd>{{ formatTimestamp(ecuState?.lastTesterPresentAt) }}</dd>
      </div>
      <div class="fact">
        <dt>TP timeout</dt>
        <dd>{{ formatTimingStatus(ecuState) }}</dd>
      </div>
      <div class="fact">
        <dt>Last fallback</dt>
        <dd>{{ formatFallbackStatus(ecuState) }}</dd>
      </div>
    </div>

    <template v-if="props.compact">
      <div class="rail-summary">
        <div>
          <span>Active connections</span>
          <strong>{{ activeConnections.length }}</strong>
        </div>
        <div>
          <span>DoIP frames</span>
          <strong>{{ doipRows.length }}</strong>
        </div>
        <div>
          <span>UDS messages</span>
          <strong>{{ udsRows.length }}</strong>
        </div>
      </div>

      <div class="rail-block">
        <h3>DoIP</h3>
        <p v-if="railDoipRows.length === 0" class="inline-state">No DoIP frames yet.</p>
        <div v-else class="rail-feed">
          <article v-for="row in railDoipRows" :key="row.id">
            <header>
              <span>{{ formatTimestamp(row.timestamp) }}</span>
              <b>{{ row.direction }}</b>
            </header>
            <strong>{{ row.name }}</strong>
            <p>{{ row.summary }}</p>
          </article>
        </div>
      </div>

      <div class="rail-block">
        <h3>UDS</h3>
        <p v-if="railUdsRows.length === 0" class="inline-state">No UDS messages yet.</p>
        <div v-else class="rail-feed">
          <article v-for="row in railUdsRows" :key="row.id">
            <header>
              <span>{{ formatTimestamp(row.timestamp) }}</span>
              <b>{{ row.direction }}</b>
            </header>
            <strong>{{ row.name }}</strong>
            <p>{{ row.summary }}</p>
          </article>
        </div>
      </div>
    </template>

    <template v-else>
    <div class="filters observation__filters" aria-label="Observation filters">
      <label>
        Connection
        <input v-model="connectionFilter" type="search" placeholder="conn_000001" />
      </label>
      <label>
        Direction
        <select v-model="directionFilter">
          <option value="">All directions</option>
          <option value="received">Received</option>
          <option value="sent">Sent</option>
          <option value="request">Request</option>
          <option value="response">Response</option>
        </select>
      </label>
      <label>
        Keyword
        <input v-model="keywordFilter" type="search" placeholder="0x8001 or 22 F1" />
      </label>
    </div>

    <div class="observation-grid">
      <div class="observation-block">
        <h3>Connections</h3>
        <p v-if="filteredConnections.length === 0" class="inline-state">No connections match the filters.</p>
        <div v-else class="compact-table compact-table--connections" role="table" aria-label="Connection list">
          <div class="compact-row compact-row--header" role="row">
            <span role="columnheader">ID</span>
            <span role="columnheader">Transport</span>
            <span role="columnheader">Endpoint</span>
            <span role="columnheader">Routing</span>
            <span role="columnheader">Tester</span>
            <span role="columnheader">ECU</span>
            <span role="columnheader">State</span>
          </div>
          <div v-for="connection in filteredConnections" :key="connection.connectionId" class="compact-row" role="row">
            <span role="cell">{{ connection.connectionId }}</span>
            <span role="cell">{{ connection.transport.toUpperCase() }}</span>
            <span role="cell">{{ connection.remoteEndpoint }}</span>
            <span role="cell">{{ connection.routingActivated ? "active" : "pending" }}</span>
            <span role="cell">{{ connection.testerLogicalAddress ?? "Unavailable" }}</span>
            <span role="cell">{{ connection.ecuLogicalAddress ?? "Unavailable" }}</span>
            <span role="cell">{{ connection.state }}</span>
          </div>
        </div>
      </div>

      <div class="observation-block">
        <h3>DoIP frames</h3>
        <p v-if="filteredDoipRows.length === 0" class="inline-state">No DoIP frames match the filters.</p>
        <div v-else class="compact-table compact-table--messages" role="table" aria-label="DoIP message list">
          <div class="compact-row compact-row--header" role="row">
            <span role="columnheader">Time</span>
            <span role="columnheader">Direction</span>
            <span role="columnheader">Connection</span>
            <span role="columnheader">Frame</span>
            <span role="columnheader">Summary</span>
          </div>
          <div v-for="row in filteredDoipRows" :key="row.id" class="compact-row" role="row">
            <span role="cell">{{ formatTimestamp(row.timestamp) }}</span>
            <span role="cell">{{ row.direction }}</span>
            <span role="cell">{{ row.connectionId }}</span>
            <span role="cell">{{ row.name }}</span>
            <span role="cell">{{ row.summary }}</span>
          </div>
        </div>
      </div>

      <div class="observation-block">
        <h3>UDS messages</h3>
        <p v-if="filteredUdsRows.length === 0" class="inline-state">No UDS messages match the filters.</p>
        <div v-else class="compact-table compact-table--messages" role="table" aria-label="UDS message list">
          <div class="compact-row compact-row--header" role="row">
            <span role="columnheader">Time</span>
            <span role="columnheader">Direction</span>
            <span role="columnheader">Connection</span>
            <span role="columnheader">Service</span>
            <span role="columnheader">Bytes</span>
          </div>
          <div v-for="row in filteredUdsRows" :key="row.id" class="compact-row" role="row">
            <span role="cell">{{ formatTimestamp(row.timestamp) }}</span>
            <span role="cell">{{ row.direction }}</span>
            <span role="cell">{{ row.connectionId }}</span>
            <span role="cell">{{ row.name }}</span>
            <span role="cell">{{ row.summary }}</span>
          </div>
        </div>
      </div>
    </div>
    </template>
  </section>
</template>
