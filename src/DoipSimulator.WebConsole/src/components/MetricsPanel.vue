<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from "vue";
import { loadRuntimeMetrics, type RuntimeMetricsSnapshot } from "../api";

const metrics = ref<RuntimeMetricsSnapshot | null>(null);
const isLoading = ref(true);
const errorMessage = ref("");
let refreshHandle: number | undefined;

onMounted(() => {
  void refresh();
  refreshHandle = window.setInterval(() => void refresh(), 2000);
});

onBeforeUnmount(() => {
  if (refreshHandle !== undefined) {
    window.clearInterval(refreshHandle);
  }
});

async function refresh(): Promise<void> {
  try {
    metrics.value = await loadRuntimeMetrics();
    errorMessage.value = "";
  } catch {
    errorMessage.value = "Metrics unavailable.";
  } finally {
    isLoading.value = false;
  }
}

function formatNumber(value: number | undefined): string {
  return typeof value === "number" && Number.isFinite(value) ? value.toLocaleString() : "Unavailable";
}

function formatRate(value: number | undefined, unit: string): string {
  return typeof value === "number" && Number.isFinite(value) ? `${value.toFixed(2)} ${unit}` : "Unavailable";
}

function formatBytes(value: number | undefined): string {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return "Unavailable";
  }

  if (value >= 1024 * 1024) {
    return `${(value / 1024 / 1024).toFixed(1)} MiB`;
  }

  if (value >= 1024) {
    return `${(value / 1024).toFixed(1)} KiB`;
  }

  return `${value} B`;
}

function formatQueue(length: number | null | undefined, state: string | undefined): string {
  if (typeof length === "number") {
    return `${length.toLocaleString()} (${state ?? "available"})`;
  }

  return state ?? "Unavailable";
}
</script>

<template>
  <section class="section" aria-labelledby="metrics-title">
    <div class="section__heading">
      <p class="eyebrow">Runtime</p>
      <h2 id="metrics-title">Basic metrics</h2>
    </div>

    <p v-if="isLoading" class="inline-state">Loading metrics.</p>
    <p v-else-if="errorMessage" class="inline-state inline-state--warning">{{ errorMessage }}</p>

    <dl v-else-if="metrics" class="facts facts--metrics">
      <div class="fact">
        <dt>Active connections</dt>
        <dd>{{ formatNumber(metrics.connections.active) }}</dd>
      </div>
      <div class="fact">
        <dt>Total accepted</dt>
        <dd>{{ formatNumber(metrics.connections.totalAccepted) }}</dd>
      </div>
      <div class="fact">
        <dt>UDS throughput</dt>
        <dd>{{ formatRate(metrics.throughput.udsRequestsPerSecond, "req/s") }}</dd>
      </div>
      <div class="fact">
        <dt>Event queue</dt>
        <dd>{{ formatQueue(metrics.queues.event.length, metrics.queues.event.state) }}</dd>
      </div>
      <div class="fact">
        <dt>PCAP queue</dt>
        <dd>{{ formatQueue(metrics.queues.pcap.length, metrics.queues.pcap.state) }}</dd>
      </div>
      <div class="fact">
        <dt>Log writes</dt>
        <dd>{{ formatRate(metrics.writeRates.logEntriesPerSecond, "events/s") }}</dd>
      </div>
      <div class="fact">
        <dt>PCAP writes</dt>
        <dd>{{ formatRate(metrics.writeRates.pcapBytesPerSecond, "B/s") }}</dd>
      </div>
      <div class="fact">
        <dt>Working set</dt>
        <dd>{{ formatBytes(metrics.memory.workingSetBytes) }}</dd>
      </div>
      <div class="fact">
        <dt>Managed heap</dt>
        <dd>{{ formatBytes(metrics.memory.managedHeapBytes) }}</dd>
      </div>
    </dl>
  </section>
</template>
