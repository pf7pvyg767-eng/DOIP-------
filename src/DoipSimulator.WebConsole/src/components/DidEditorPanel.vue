<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { loadDids, updateDidValue, type DidSummary } from "../api";

interface DidEditState {
  value: string;
  persist: boolean;
  saving: boolean;
  error: string;
}

const dids = ref<DidSummary[]>([]);
const isLoading = ref(true);
const errorMessage = ref("");
const editState = reactive<Record<string, DidEditState>>({});

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
    }
    dids.value = next;
  } catch {
    dids.value = [];
    errorMessage.value = "DID list could not be loaded.";
  } finally {
    isLoading.value = false;
  }
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
          <span>{{ did.expectedLength ?? "Any" }} bytes</span>
          <span>{{ did.permissionSummary }}</span>
        </div>

        <label class="did-row__value">
          Value
          <input
            v-model="editState[did.did].value"
            :disabled="!did.writable || editState[did.did].saving"
            spellcheck="false"
            autocomplete="off"
          />
        </label>

        <label class="did-row__persist">
          <input
            v-model="editState[did.did].persist"
            :disabled="!did.writable || editState[did.did].saving"
            type="checkbox"
          />
          Persist
        </label>

        <button class="primary-button" type="submit" :disabled="!did.writable || editState[did.did].saving">
          {{ editState[did.did].saving ? "Saving" : "Write" }}
        </button>

        <p v-if="editState[did.did].error" class="did-row__error">{{ editState[did.did].error }}</p>
      </form>
    </div>
  </section>
</template>
