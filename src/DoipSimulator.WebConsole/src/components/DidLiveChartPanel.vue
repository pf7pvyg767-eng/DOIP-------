<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from "vue";
import {
  createRuntimeEventSocket,
  loadDidSamples,
  type DidRuntimeSample,
  type RuntimeEvent,
} from "../api";

interface ChartPoint {
  at: number;
  value: number;
}

const maxAgeMs = 60_000;
const maxPoints = 300;
const chartWidth = 680;
const chartHeight = 220;
const padding = 22;

const samples = ref<DidRuntimeSample[]>([]);
const selected = reactive<Record<string, boolean>>({});
const series = reactive<Record<string, ChartPoint[]>>({});
const errorMessage = ref("");
let pollHandle: number | undefined;
let socket: WebSocket | undefined;

const numericSamples = computed(() =>
  samples.value.filter((sample) => typeof sample.numericValue === "number" && Number.isFinite(sample.numericValue)));

const selectedDids = computed(() =>
  numericSamples.value.map((sample) => sample.did).filter((did) => selected[did]));

const selectedSamples = computed(() =>
  numericSamples.value.filter((sample) => selected[sample.did]));

const allVisiblePoints = computed(() =>
  selectedDids.value.flatMap((did) => series[did] ?? []));

const valueRange = computed(() => {
  const values = allVisiblePoints.value.map((point) => point.value);
  if (values.length === 0) {
    return { min: 0, max: 1 };
  }

  const min = Math.min(...values);
  const max = Math.max(...values);
  return min === max ? { min: min - 1, max: max + 1 } : { min, max };
});

const timeRange = computed(() => {
  const points = allVisiblePoints.value;
  const now = Date.now();
  if (points.length === 0) {
    return { min: now - maxAgeMs, max: now };
  }

  const max = Math.max(now, ...points.map((point) => point.at));
  return { min: max - maxAgeMs, max };
});

onMounted(() => {
  void refreshSamples();
  pollHandle = window.setInterval(() => void refreshSamples(), 1000);
  connectEvents();
});

onBeforeUnmount(() => {
  if (pollHandle !== undefined) {
    window.clearInterval(pollHandle);
  }

  socket?.close();
});

async function refreshSamples(): Promise<void> {
  try {
    const next = await loadDidSamples();
    samples.value = next;
    for (const sample of next) {
      if (typeof selected[sample.did] !== "boolean" && typeof sample.numericValue === "number") {
        selected[sample.did] = selectedDids.value.length === 0;
      }
      appendSample(sample);
    }
    errorMessage.value = "";
  } catch {
    errorMessage.value = "DID samples could not be loaded.";
  }
}

function connectEvents(): void {
  try {
    socket = createRuntimeEventSocket();
    socket.addEventListener("message", (event) => {
      const runtimeEvent = JSON.parse(event.data as string) as RuntimeEvent;
      if (runtimeEvent.name !== "uds.did.read" || !runtimeEvent.data) {
        return;
      }

      const sample = sampleFromEvent(runtimeEvent);
      if (sample) {
        appendSample(sample);
      }
    });
  } catch {
  }
}

function sampleFromEvent(event: RuntimeEvent): DidRuntimeSample | null {
  const data = event.data ?? {};
  const did = typeof data.did === "string" ? data.did : "";
  const numericValue = typeof data.numericValue === "number" ? data.numericValue : null;
  const sampledAt = typeof data.sampledAt === "string" ? data.sampledAt : event.timestamp;
  if (!did || numericValue === null) {
    return null;
  }

  return {
    did,
    rawValue: typeof data.rawValue === "string" ? data.rawValue : "",
    numericValue,
    providerType: typeof data.providerType === "string" ? data.providerType : "event",
    sampledAt,
  };
}

function appendSample(sample: DidRuntimeSample): void {
  if (typeof sample.numericValue !== "number" || !Number.isFinite(sample.numericValue)) {
    return;
  }

  const at = Date.parse(sample.sampledAt);
  if (!Number.isFinite(at)) {
    return;
  }

  const points = series[sample.did] ?? [];
  points.push({ at, value: sample.numericValue });
  const cutoff = Date.now() - maxAgeMs;
  series[sample.did] = points
    .filter((point) => point.at >= cutoff)
    .slice(-maxPoints);
}

function pointsFor(did: string): string {
  const points = series[did] ?? [];
  const time = timeRange.value;
  const values = valueRange.value;
  return points
    .map((point) => {
      const x = padding + ((point.at - time.min) / Math.max(time.max - time.min, 1)) * (chartWidth - padding * 2);
      const y = chartHeight - padding - ((point.value - values.min) / Math.max(values.max - values.min, 1)) * (chartHeight - padding * 2);
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(" ");
}

function colorFor(index: number): string {
  return ["#59a9ff", "#73d99f", "#f5c15c", "#ff8d7a", "#b99cff"][index % 5];
}
</script>

<template>
  <section class="section did-chart-panel" aria-labelledby="did-chart-title">
    <div class="section__heading section__heading--row">
      <div>
        <p class="eyebrow">DID chart</p>
        <h2 id="did-chart-title">Live samples</h2>
      </div>
      <button class="utility-button" type="button" @click="refreshSamples">Refresh</button>
    </div>

    <p v-if="errorMessage" class="inline-state inline-state--error">{{ errorMessage }}</p>
    <p v-if="numericSamples.length === 0" class="inline-state">No numeric DID samples are available.</p>

    <div v-else class="did-chart-layout">
      <div class="did-chart-selectors">
        <label v-for="sample in numericSamples" :key="sample.did" class="toggle-row">
          <input v-model="selected[sample.did]" type="checkbox" />
          <span>{{ sample.did }} {{ sample.name ?? sample.providerType }} {{ sample.numericValue }}</span>
        </label>
      </div>

      <svg class="did-chart" :viewBox="`0 0 ${chartWidth} ${chartHeight}`" role="img" aria-label="Selected DID live samples">
        <line :x1="padding" :x2="chartWidth - padding" :y1="chartHeight - padding" :y2="chartHeight - padding" />
        <line :x1="padding" :x2="padding" :y1="padding" :y2="chartHeight - padding" />
        <text :x="padding" :y="padding - 6">{{ valueRange.max.toFixed(1) }}</text>
        <text :x="padding" :y="chartHeight - 5">{{ valueRange.min.toFixed(1) }}</text>
        <polyline
          v-for="(did, index) in selectedDids"
          :key="did"
          :points="pointsFor(did)"
          :stroke="colorFor(index)"
        />
      </svg>

      <div class="did-chart-legend">
        <span v-for="(sample, index) in selectedSamples" :key="sample.did">
          <i :style="{ background: colorFor(index) }"></i>
          {{ sample.did }} {{ sample.numericValue }}
        </span>
      </div>
    </div>
  </section>
</template>
