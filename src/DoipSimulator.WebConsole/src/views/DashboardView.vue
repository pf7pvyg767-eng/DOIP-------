<script setup lang="ts">
import { onMounted, ref } from "vue";
import { loadDashboardState, type DashboardState } from "../api";
import DidEditorPanel from "../components/DidEditorPanel.vue";
import EventLogPanel from "../components/EventLogPanel.vue";
import RealtimeObservationPanel from "../components/RealtimeObservationPanel.vue";
import StatusPanel from "../components/StatusPanel.vue";

const state = ref<DashboardState | null>(null);
const isLoading = ref(true);
const errorMessage = ref("");

onMounted(load);

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
</script>

<template>
  <main class="dashboard-shell">
    <header class="page-header">
      <div>
        <p class="eyebrow">DOIP Simulator</p>
        <h1>Web Console</h1>
      </div>
    </header>

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
      <StatusPanel :health="state.health" />

      <RealtimeObservationPanel />

      <DidEditorPanel />

      <EventLogPanel />

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
    </template>
  </main>
</template>
