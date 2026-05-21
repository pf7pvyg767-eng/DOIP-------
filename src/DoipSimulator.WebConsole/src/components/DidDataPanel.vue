<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from "vue";
import {
  createRuntimeEventSocket,
  loadDidSamples,
  loadDids,
  updateDidProvider,
  updateDidValue,
  type DidProviderType,
  type DidRuntimeSample,
  type DidSummary,
  type RuntimeEvent,
} from "../api";

interface DidEditState {
  value: string;
  persist: boolean;
  saving: boolean;
  error: string;
}

interface DidProviderEditState {
  type: DidProviderType;
  numericType: string;
  min: number | null;
  max: number | null;
  amplitude: number | null;
  offset: number | null;
  periodMs: number | null;
  slopePerSecond: number | null;
  seed: number | null;
  persist: boolean;
  saving: boolean;
  error: string;
}

interface ChartPoint {
  at: number;
  value: number;
}

const maxAgeMs = 60_000;
const maxPoints = 180;
const chartWidth = 320;
const chartHeight = 112;
const padding = 16;

const dids = ref<DidSummary[]>([]);
const samples = ref<DidRuntimeSample[]>([]);
const isLoading = ref(true);
const errorMessage = ref("");
const editState = reactive<Record<string, DidEditState>>({});
const providerState = reactive<Record<string, DidProviderEditState>>({});
const series = reactive<Record<string, ChartPoint[]>>({});
let pollHandle: number | undefined;
let socket: WebSocket | undefined;

const numericDidCount = computed(
  () => samples.value.filter((sample) => typeof sample.numericValue === "number").length,
);

onMounted(() => {
  void load();
  pollHandle = window.setInterval(() => void refreshSamples(), 1000);
  connectEvents();
});

onBeforeUnmount(() => {
  if (pollHandle !== undefined) {
    window.clearInterval(pollHandle);
  }

  socket?.close();
});

async function load(): Promise<void> {
  isLoading.value = true;
  errorMessage.value = "";

  try {
    const [nextDids, nextSamples] = await Promise.all([loadDids(), loadDidSamples()]);
    applyDids(nextDids);
    applySamples(nextSamples);
  } catch {
    dids.value = [];
    samples.value = [];
    errorMessage.value = "DID data could not be loaded.";
  } finally {
    isLoading.value = false;
  }
}

function applyDids(next: DidSummary[]): void {
  for (const did of next) {
    editState[did.did] = {
      value: did.value,
      persist: editState[did.did]?.persist ?? true,
      saving: false,
      error: "",
    };
    providerState[did.did] = toProviderEditState(did, providerState[did.did]?.persist ?? false);
  }

  dids.value = next;
}

function applySamples(next: DidRuntimeSample[]): void {
  samples.value = next;
  for (const sample of next) {
    appendSample(sample);
  }
}

async function refreshSamples(): Promise<void> {
  try {
    applySamples(await loadDidSamples());
  } catch {
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
      if (!sample) {
        return;
      }

      samples.value = [sample, ...samples.value.filter((item) => item.did !== sample.did)];
      appendSample(sample);
    });
  } catch {
  }
}

function sampleFromEvent(event: RuntimeEvent): DidRuntimeSample | null {
  const data = event.data ?? {};
  const did = typeof data.did === "string" ? data.did : "";
  const numericValue = typeof data.numericValue === "number" ? data.numericValue : null;
  const sampledAt = typeof data.sampledAt === "string" ? data.sampledAt : event.timestamp;
  if (!did) {
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

function toProviderEditState(did: DidSummary, persist: boolean): DidProviderEditState {
  const provider = did.valueProvider;
  return {
    type: normalizeProviderType(provider?.type),
    numericType: provider?.numericType ?? "uint16",
    min: provider?.min ?? 0,
    max: provider?.max ?? 100,
    amplitude: provider?.amplitude ?? 10,
    offset: provider?.offset ?? 100,
    periodMs: provider?.periodMs ?? 1000,
    slopePerSecond: provider?.slopePerSecond ?? 1,
    seed: provider?.seed ?? null,
    persist,
    saving: false,
    error: "",
  };
}

function normalizeProviderType(value: string | null | undefined): DidProviderType {
  return value === "random" || value === "sine" || value === "linear" ? value : "static";
}

function isDynamic(did: DidSummary): boolean {
  return normalizeProviderType(did.valueProvider?.type) !== "static";
}

function buildProviderBody(state: DidProviderEditState) {
  if (state.type === "random") {
    return {
      type: "random",
      numericType: state.numericType,
      min: state.min,
      max: state.max,
      seed: state.seed,
    };
  }

  if (state.type === "sine") {
    return {
      type: "sine",
      numericType: state.numericType,
      amplitude: state.amplitude,
      offset: state.offset,
      periodMs: state.periodMs,
    };
  }

  if (state.type === "linear") {
    return {
      type: "linear",
      numericType: state.numericType,
      offset: state.offset,
      slopePerSecond: state.slopePerSecond,
    };
  }

  return { type: "static" };
}

async function submit(did: DidSummary): Promise<void> {
  const state = editState[did.did];
  if (!state || state.saving) {
    return;
  }

  state.saving = true;
  state.error = "";
  try {
    await updateDidValue(did.did, {
      valueEncoding: "hex",
      value: state.value,
      persist: state.persist,
    });
    await load();
  } catch (error) {
    state.error = error instanceof Error ? error.message : "DID write failed.";
  } finally {
    state.saving = false;
  }
}

async function submitProvider(did: DidSummary): Promise<void> {
  const state = providerState[did.did];
  if (!state || state.saving) {
    return;
  }

  state.saving = true;
  state.error = "";
  try {
    await updateDidProvider(did.did, {
      valueProvider: buildProviderBody(state),
      persist: state.persist,
    });
    await load();
  } catch (error) {
    state.error = error instanceof Error ? error.message : "DID provider update failed.";
  } finally {
    state.saving = false;
  }
}

function sampleFor(did: string): DidRuntimeSample | undefined {
  return samples.value.find((sample) => sample.did === did);
}

function valueRangeFor(did: string): { min: number; max: number } {
  const values = (series[did] ?? []).map((point) => point.value);
  if (values.length === 0) {
    return { min: 0, max: 1 };
  }

  const min = Math.min(...values);
  const max = Math.max(...values);
  return min === max ? { min: min - 1, max: max + 1 } : { min, max };
}

function timeRangeFor(did: string): { min: number; max: number } {
  const points = series[did] ?? [];
  const now = Date.now();
  if (points.length === 0) {
    return { min: now - maxAgeMs, max: now };
  }

  const max = Math.max(now, ...points.map((point) => point.at));
  return { min: max - maxAgeMs, max };
}

function pointsFor(did: string): string {
  const points = series[did] ?? [];
  const time = timeRangeFor(did);
  const values = valueRangeFor(did);
  return points
    .map((point) => {
      const x = padding + ((point.at - time.min) / Math.max(time.max - time.min, 1)) * (chartWidth - padding * 2);
      const y = chartHeight - padding - ((point.value - values.min) / Math.max(values.max - values.min, 1)) * (chartHeight - padding * 2);
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(" ");
}

function chartColorFor(providerType: string | null | undefined): string {
  switch (normalizeProviderType(providerType)) {
    case "random":
      return "#f5c15c";
    case "sine":
      return "#59a9ff";
    case "linear":
      return "#73d99f";
    default:
      return "#93a4b6";
  }
}

function formatSampleValue(sample: DidRuntimeSample | undefined): string {
  if (!sample) {
    return "--";
  }

  return typeof sample.numericValue === "number" ? sample.numericValue.toLocaleString() : sample.rawValue;
}

function formatSampleTime(sample: DidRuntimeSample | undefined): string {
  if (!sample) {
    return "No sample";
  }

  const date = new Date(sample.sampledAt);
  return Number.isNaN(date.getTime()) ? sample.sampledAt : date.toLocaleTimeString();
}
</script>

<template>
  <section class="section did-data-panel" aria-labelledby="did-data-title">
    <div class="section__heading section__heading--row">
      <div>
        <p class="eyebrow">DID data</p>
        <h2 id="did-data-title">Runtime values and live charts</h2>
        <p>{{ dids.length }} configured, {{ numericDidCount }} numeric sample streams</p>
      </div>
      <button class="utility-button" type="button" @click="load">Refresh</button>
    </div>

    <p v-if="isLoading" class="inline-state">Loading DID values.</p>
    <p v-else-if="errorMessage" class="inline-state inline-state--error">{{ errorMessage }}</p>
    <p v-else-if="dids.length === 0" class="inline-state">No DIDs are configured.</p>

    <div v-else class="did-data-grid">
      <form v-for="did in dids" :key="did.did" class="did-combined-card" @submit.prevent="submit(did)">
        <header class="did-card-header">
          <div class="did-card-title">
            <strong>{{ did.did }}</strong>
            <h3>{{ did.name ?? "Unnamed DID" }}</h3>
            <span>{{ did.permissionSummary }}</span>
          </div>
          <span class="did-provider-pill" :data-provider="providerState[did.did]?.type ?? 'static'">
            {{ providerState[did.did]?.type ?? "static" }}
          </span>
        </header>

        <div class="did-card-body">
          <div class="did-card-controls">
            <div class="did-card-facts">
              <span>{{ did.valueEncoding }}</span>
              <span>{{ did.expectedLength ?? "Any" }} bytes</span>
              <span>{{ did.writable && !isDynamic(did) ? "Writable" : "Read-only" }}</span>
            </div>

            <label class="did-row__value">
              Value
              <input
                v-model="editState[did.did].value"
                :disabled="!did.writable || isDynamic(did) || editState[did.did].saving"
                spellcheck="false"
                autocomplete="off"
              />
            </label>

            <div class="did-card-write-row">
              <label class="did-row__persist">
                <input
                  v-model="editState[did.did].persist"
                  :disabled="!did.writable || isDynamic(did) || editState[did.did].saving"
                  type="checkbox"
                />
                Persist
              </label>

              <button class="primary-button" type="submit" :disabled="!did.writable || isDynamic(did) || editState[did.did].saving">
                {{ editState[did.did].saving ? "Saving" : "Write" }}
              </button>
            </div>

            <div class="did-card-provider">
              <label class="field-row">
                Provider
                <select v-model="providerState[did.did].type" :disabled="providerState[did.did].saving">
                  <option value="static">static</option>
                  <option value="random">random</option>
                  <option value="sine">sine</option>
                  <option value="linear">linear</option>
                </select>
              </label>

              <label v-if="providerState[did.did].type !== 'static'" class="field-row">
                Numeric
                <select v-model="providerState[did.did].numericType" :disabled="providerState[did.did].saving">
                  <option value="uint8">uint8</option>
                  <option value="uint16">uint16</option>
                  <option value="int16">int16</option>
                  <option value="uint32">uint32</option>
                  <option value="int32">int32</option>
                </select>
              </label>

              <template v-if="providerState[did.did].type === 'random'">
                <label class="field-row">
                  Min
                  <input v-model.number="providerState[did.did].min" type="number" />
                </label>
                <label class="field-row">
                  Max
                  <input v-model.number="providerState[did.did].max" type="number" />
                </label>
                <label class="field-row">
                  Seed
                  <input v-model.number="providerState[did.did].seed" type="number" />
                </label>
              </template>

              <template v-if="providerState[did.did].type === 'sine'">
                <label class="field-row">
                  Amp
                  <input v-model.number="providerState[did.did].amplitude" type="number" />
                </label>
                <label class="field-row">
                  Offset
                  <input v-model.number="providerState[did.did].offset" type="number" />
                </label>
                <label class="field-row">
                  Period ms
                  <input v-model.number="providerState[did.did].periodMs" type="number" min="1" />
                </label>
              </template>

              <template v-if="providerState[did.did].type === 'linear'">
                <label class="field-row">
                  Offset
                  <input v-model.number="providerState[did.did].offset" type="number" />
                </label>
                <label class="field-row">
                  Slope/s
                  <input v-model.number="providerState[did.did].slopePerSecond" type="number" />
                </label>
              </template>

              <label class="did-row__persist">
                <input v-model="providerState[did.did].persist" :disabled="providerState[did.did].saving" type="checkbox" />
                Persist
              </label>

              <button class="primary-button" type="button" :disabled="providerState[did.did].saving" @click="submitProvider(did)">
                {{ providerState[did.did].saving ? "Saving" : "Apply" }}
              </button>
            </div>
          </div>

          <aside class="did-card-telemetry">
            <div class="did-card-live-value">
              <span>Latest</span>
              <strong>{{ formatSampleValue(sampleFor(did.did)) }}</strong>
              <small>{{ sampleFor(did.did)?.rawValue ?? did.value }}</small>
              <small>{{ formatSampleTime(sampleFor(did.did)) }}</small>
            </div>

            <svg
              v-if="(series[did.did] ?? []).length > 1"
              class="did-card-chart"
              :viewBox="`0 0 ${chartWidth} ${chartHeight}`"
              role="img"
              :aria-label="`${did.did} live chart`"
            >
              <line :x1="padding" :x2="chartWidth - padding" :y1="chartHeight - padding" :y2="chartHeight - padding" />
              <line :x1="padding" :x2="padding" :y1="padding" :y2="chartHeight - padding" />
              <text :x="padding" :y="padding - 5">{{ valueRangeFor(did.did).max.toFixed(0) }}</text>
              <text :x="padding" :y="chartHeight - 4">{{ valueRangeFor(did.did).min.toFixed(0) }}</text>
              <polyline :points="pointsFor(did.did)" :stroke="chartColorFor(sampleFor(did.did)?.providerType)" />
            </svg>

            <p v-else class="inline-state did-card-empty-chart">Waiting for numeric samples.</p>
          </aside>
        </div>

        <p v-if="editState[did.did].error" class="did-row__error">{{ editState[did.did].error }}</p>
        <p v-if="providerState[did.did].error" class="did-row__error">{{ providerState[did.did].error }}</p>
      </form>
    </div>
  </section>
</template>
