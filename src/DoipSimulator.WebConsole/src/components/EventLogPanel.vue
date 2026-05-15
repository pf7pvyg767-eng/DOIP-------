<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import {
  createRuntimeEventSocket,
  loadRecentEvents,
  type RuntimeEvent,
  type RuntimeEventCategory,
  type RuntimeEventLevel,
} from "../api";

const maxEvents = 1000;
const reconnectDelayMs = 2000;
const maxReconnectAttempts = 5;
const levels: Array<RuntimeEventLevel | ""> = ["", "info", "warning", "error"];
const categories: Array<RuntimeEventCategory | ""> = [
  "",
  "system",
  "config",
  "connection",
  "doip",
  "uds",
  "state",
  "fault",
  "tls",
  "pcap",
];

const events = ref<RuntimeEvent[]>([]);
const levelFilter = ref<RuntimeEventLevel | "">("");
const categoryFilter = ref<RuntimeEventCategory | "">("");
const isLoading = ref(true);
const connectionState = ref("connecting");
const errorMessage = ref("");
let socket: WebSocket | null = null;
let reconnectTimer: number | undefined;
let reconnectAttempts = 0;
let isDisposed = false;

const visibleEvents = computed(() =>
  events.value.filter((event) => {
    if (levelFilter.value && event.level !== levelFilter.value) {
      return false;
    }

    if (categoryFilter.value && event.category !== categoryFilter.value) {
      return false;
    }

    return true;
  }),
);

onMounted(async () => {
  await refreshRecentEvents();
  connect();
});

onBeforeUnmount(() => {
  isDisposed = true;
  if (reconnectTimer !== undefined) {
    window.clearTimeout(reconnectTimer);
  }

  socket?.close();
});

async function refreshRecentEvents(): Promise<void> {
  isLoading.value = true;
  errorMessage.value = "";

  try {
    mergeEvents(await loadRecentEvents(200));
  } catch {
    errorMessage.value = "Recent runtime events could not be loaded.";
  } finally {
    isLoading.value = false;
  }
}

function connect(): void {
  if (isDisposed) {
    return;
  }

  connectionState.value = reconnectAttempts > 0 ? "reconnecting" : "connecting";
  socket = createRuntimeEventSocket();

  socket.addEventListener("open", () => {
    reconnectAttempts = 0;
    connectionState.value = "live";
    void refreshRecentEvents();
  });

  socket.addEventListener("message", (message) => {
    try {
      mergeEvents([JSON.parse(message.data) as RuntimeEvent]);
    } catch {
      errorMessage.value = "A runtime event message could not be parsed.";
    }
  });

  socket.addEventListener("close", scheduleReconnect);
  socket.addEventListener("error", () => {
    connectionState.value = "disconnected";
  });
}

function scheduleReconnect(): void {
  if (isDisposed) {
    return;
  }

  socket = null;
  connectionState.value = "disconnected";
  if (reconnectAttempts >= maxReconnectAttempts) {
    return;
  }

  reconnectAttempts += 1;
  reconnectTimer = window.setTimeout(() => {
    void refreshRecentEvents();
    connect();
  }, reconnectDelayMs);
}

function mergeEvents(nextEvents: RuntimeEvent[]): void {
  const byId = new Map(events.value.map((event) => [event.id, event]));
  for (const event of nextEvents) {
    byId.set(event.id, event);
  }

  events.value = Array.from(byId.values())
    .sort((left, right) => new Date(left.timestamp).getTime() - new Date(right.timestamp).getTime())
    .slice(-maxEvents);
}

function formatTimestamp(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleTimeString();
}
</script>

<template>
  <section class="section event-log" aria-labelledby="event-log-title">
    <div class="section__heading section__heading--row">
      <div>
        <p class="eyebrow">Runtime events</p>
        <h2 id="event-log-title">Event log</h2>
      </div>
      <span class="connection-pill" :data-state="connectionState">{{ connectionState }}</span>
    </div>

    <div class="filters" aria-label="Runtime event filters">
      <label>
        Level
        <select v-model="levelFilter">
          <option v-for="level in levels" :key="level || 'all-levels'" :value="level">
            {{ level || "All levels" }}
          </option>
        </select>
      </label>

      <label>
        Category
        <select v-model="categoryFilter">
          <option v-for="category in categories" :key="category || 'all-categories'" :value="category">
            {{ category || "All categories" }}
          </option>
        </select>
      </label>
    </div>

    <p v-if="isLoading" class="inline-state">Loading recent events.</p>
    <p v-else-if="errorMessage" class="inline-state inline-state--error">{{ errorMessage }}</p>
    <p v-else-if="visibleEvents.length === 0" class="inline-state">No events match the current filters.</p>

    <div v-else class="event-table" role="table" aria-label="Runtime event log">
      <div class="event-row event-row--header" role="row">
        <span role="columnheader">Time</span>
        <span role="columnheader">Level</span>
        <span role="columnheader">Category</span>
        <span role="columnheader">Name</span>
        <span role="columnheader">Message</span>
      </div>

      <div v-for="event in visibleEvents" :key="event.id" class="event-row" role="row">
        <span role="cell">{{ formatTimestamp(event.timestamp) }}</span>
        <span role="cell">
          <span class="level-pill" :data-level="event.level">{{ event.level }}</span>
        </span>
        <span role="cell">{{ event.category }}</span>
        <span role="cell">{{ event.name }}</span>
        <span role="cell">{{ event.message }}</span>
      </div>
    </div>
  </section>
</template>
