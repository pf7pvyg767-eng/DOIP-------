<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { activateDtc, clearDtc, loadDtcs, type DtcSummary } from "../api";

interface DtcEditState {
  status: string;
  busy: boolean;
  error: string;
}

const dtcs = ref<DtcSummary[]>([]);
const isLoading = ref(true);
const errorMessage = ref("");
const editState = reactive<Record<string, DtcEditState>>({});

onMounted(load);

async function load(): Promise<void> {
  isLoading.value = true;
  errorMessage.value = "";

  try {
    const next = await loadDtcs();
    for (const dtc of next) {
      editState[dtc.code] = {
        status: editState[dtc.code]?.status ?? dtc.status,
        busy: false,
        error: "",
      };
    }
    dtcs.value = next;
  } catch {
    dtcs.value = [];
    errorMessage.value = "DTC list could not be loaded.";
  } finally {
    isLoading.value = false;
  }
}

async function activate(dtc: DtcSummary): Promise<void> {
  const state = editState[dtc.code];
  if (!state || state.busy) {
    return;
  }

  state.busy = true;
  state.error = "";
  try {
    await activateDtc(dtc.code, { status: state.status });
    await load();
  } catch (error) {
    state.error = error instanceof Error ? error.message : "DTC activation failed.";
  } finally {
    state.busy = false;
  }
}

async function clear(dtc: DtcSummary): Promise<void> {
  const state = editState[dtc.code];
  if (!state || state.busy) {
    return;
  }

  state.busy = true;
  state.error = "";
  try {
    await clearDtc(dtc.code);
    await load();
  } catch (error) {
    state.error = error instanceof Error ? error.message : "DTC clear failed.";
  } finally {
    state.busy = false;
  }
}
</script>

<template>
  <section class="section did-panel" aria-labelledby="dtc-title">
    <div class="section__heading section__heading--row">
      <div>
        <p class="eyebrow">DTC</p>
        <h2 id="dtc-title">Runtime injection</h2>
      </div>
      <button class="utility-button" type="button" @click="load">Refresh</button>
    </div>

    <p v-if="isLoading" class="inline-state">Loading DTC values.</p>
    <p v-else-if="errorMessage" class="inline-state inline-state--error">{{ errorMessage }}</p>
    <p v-else-if="dtcs.length === 0" class="inline-state">No DTCs are configured.</p>

    <div v-else class="did-list">
      <form v-for="dtc in dtcs" :key="dtc.code" class="dtc-row" @submit.prevent="activate(dtc)">
        <div class="did-row__meta">
          <strong>{{ dtc.code }}</strong>
          <span>{{ dtc.name ?? "Unnamed" }}</span>
          <span>{{ dtc.description ?? "No description" }}</span>
          <span>{{ dtc.active ? "active" : "cleared" }}</span>
        </div>

        <label class="did-row__value">
          Status
          <input
            v-model="editState[dtc.code].status"
            :disabled="editState[dtc.code].busy"
            spellcheck="false"
            autocomplete="off"
          />
        </label>

        <button class="primary-button" type="submit" :disabled="editState[dtc.code].busy">
          Activate
        </button>
        <button class="utility-button" type="button" :disabled="editState[dtc.code].busy" @click="clear(dtc)">
          Clear
        </button>

        <p v-if="editState[dtc.code].error" class="did-row__error">{{ editState[dtc.code].error }}</p>
      </form>
    </div>
  </section>
</template>
