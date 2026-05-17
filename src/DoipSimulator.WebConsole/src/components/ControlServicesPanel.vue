<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { loadControlServices, type ControlServicesSnapshot } from "../api";

const snapshot = ref<ControlServicesSnapshot | null>(null);
const isLoading = ref(true);
const errorMessage = ref("");

const routines = computed(() => snapshot.value?.routines ?? []);

onMounted(load);

async function load(): Promise<void> {
  isLoading.value = true;
  errorMessage.value = "";

  try {
    snapshot.value = await loadControlServices();
  } catch {
    snapshot.value = null;
    errorMessage.value = "Control service state could not be loaded.";
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <section class="section" aria-labelledby="control-services-title">
    <div class="section__heading section__heading--row">
      <div>
        <p class="eyebrow">Control services</p>
        <h2 id="control-services-title">Routine and control status</h2>
      </div>
      <button class="utility-button" type="button" @click="load">Refresh</button>
    </div>

    <p v-if="isLoading">Loading control service state.</p>
    <p v-else-if="errorMessage" class="inline-state inline-state--error" role="alert">{{ errorMessage }}</p>

    <template v-else-if="snapshot">
      <dl class="facts facts--config">
        <div class="fact">
          <dt>CommunicationControl</dt>
          <dd>{{ snapshot.communicationControl.controlType }}</dd>
        </div>
        <div class="fact">
          <dt>Communication type</dt>
          <dd>{{ snapshot.communicationControl.communicationType }}</dd>
        </div>
        <div class="fact">
          <dt>DTC setting</dt>
          <dd>{{ snapshot.dtcSetting.enabled ? "enabled" : "disabled" }}</dd>
        </div>
        <div class="fact">
          <dt>DTC setting type</dt>
          <dd>{{ snapshot.dtcSetting.settingType }}</dd>
        </div>
      </dl>

      <div class="compact-table compact-table--routines" v-if="routines.length > 0">
        <div class="compact-row compact-row--header">
          <span>Routine ID</span>
          <span>Name</span>
          <span>Start</span>
          <span>Stop</span>
          <span>Results</span>
        </div>
        <div
          class="compact-row"
          v-for="routine in routines"
          :key="routine.routineId ?? routine.identifier"
        >
          <span>{{ routine.routineId ?? routine.identifier }}</span>
          <span>{{ routine.name ?? "Unnamed" }}</span>
          <span>{{ routine.hasStartResponse ? "configured" : "not configured" }}</span>
          <span>{{ routine.hasStopResponse ? "configured" : "not configured" }}</span>
          <span>{{ routine.hasRequestResultsResponse ? "configured" : "not configured" }}</span>
        </div>
      </div>
      <p v-else>No routines configured.</p>
    </template>
  </section>
</template>
