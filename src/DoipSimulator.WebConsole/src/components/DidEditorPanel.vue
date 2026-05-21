<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { loadDids, updateDidProvider, updateDidValue, type DidProviderType, type DidSummary } from "../api";

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

const dids = ref<DidSummary[]>([]);
const isLoading = ref(true);
const errorMessage = ref("");
const editState = reactive<Record<string, DidEditState>>({});
const providerState = reactive<Record<string, DidProviderEditState>>({});

onMounted(load);

async function load(): Promise<void> {
  isLoading.value = true;
  errorMessage.value = "";

  try {
    const next = await loadDids();
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
  } catch {
    dids.value = [];
    errorMessage.value = "DID list could not be loaded.";
  } finally {
    isLoading.value = false;
  }
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
</script>

<template>
  <section class="section did-panel" aria-labelledby="did-title">
    <div class="section__heading section__heading--row">
      <div>
        <p class="eyebrow">DID</p>
        <h2 id="did-title">Runtime values</h2>
      </div>
      <button class="utility-button" type="button" @click="load">Refresh</button>
    </div>

    <p v-if="isLoading" class="inline-state">Loading DID values.</p>
    <p v-else-if="errorMessage" class="inline-state inline-state--error">{{ errorMessage }}</p>
    <p v-else-if="dids.length === 0" class="inline-state">No DIDs are configured.</p>

    <div v-else class="did-list">
      <form v-for="did in dids" :key="did.did" class="did-row" @submit.prevent="submit(did)">
        <div class="did-row__meta">
          <strong>{{ did.did }}</strong>
          <span>{{ did.name ?? "Unnamed" }}</span>
          <span>{{ did.valueEncoding }}</span>
          <span>provider: {{ providerState[did.did]?.type ?? "static" }}</span>
          <span>{{ did.expectedLength ?? "Any" }} bytes</span>
          <span>{{ did.permissionSummary }}</span>
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

        <p v-if="editState[did.did].error" class="did-row__error">{{ editState[did.did].error }}</p>

        <div class="did-provider">
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

        <p v-if="providerState[did.did].error" class="did-row__error">{{ providerState[did.did].error }}</p>
      </form>
    </div>
  </section>
</template>
