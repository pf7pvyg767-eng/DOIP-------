<script setup lang="ts">
import { computed, ref, watch } from "vue";
import ConnectionStepDetail from "./ConnectionStepDetail.vue";
import ConnectionStepList from "./ConnectionStepList.vue";
import EvidenceSummaryGrid from "./EvidenceSummaryGrid.vue";
import {
  buildConnectionSteps,
  selectDefaultStep,
  type ConnectionStepId,
  type RuntimeCockpitSnapshot,
} from "../connectionWorkflow";

const props = defineProps<{
  snapshot: RuntimeCockpitSnapshot;
  runtimePhase: { label: string; detail: string; state: string };
  shutdownButtonLabel: string;
  shutdownStatus: "idle" | "stopping" | "stopped" | "failed";
  shutdownMessage: string;
}>();

const emit = defineEmits<{
  shutdown: [];
}>();

const selectedStepId = ref<ConnectionStepId>("udp-discovery");
const copyState = ref("");
let copyTimer: number | undefined;

const steps = computed(() => buildConnectionSteps(props.snapshot));
const selectedStep = computed(() =>
  steps.value.find((step) => step.id === selectedStepId.value) ?? steps.value[0]);

watch(
  steps,
  (next) => {
    if (!next.some((step) => step.id === selectedStepId.value)) {
      selectedStepId.value = selectDefaultStep(next);
      return;
    }

    const current = next.find((step) => step.id === selectedStepId.value);
    if (current?.state === "not-started") {
      selectedStepId.value = selectDefaultStep(next);
    }
  },
  { immediate: true },
);

async function copyText(text: string): Promise<void> {
  if (!text) {
    return;
  }

  try {
    await navigator.clipboard.writeText(text);
    copyState.value = "Copied";
  } catch {
    copyState.value = "Copy failed";
  }

  if (copyTimer !== undefined) {
    window.clearTimeout(copyTimer);
  }
  copyTimer = window.setTimeout(() => {
    copyState.value = "";
    copyTimer = undefined;
  }, 1500);
}
</script>

<template>
  <section class="section connection-guide cockpit-panel" aria-labelledby="runtime-cockpit-title">
    <div class="section__heading section__heading--row">
      <div>
        <p class="eyebrow">Connection</p>
        <h2 id="runtime-cockpit-title">Diagnostic connection workflow</h2>
      </div>
      <div class="connection-guide__actions">
        <span class="phase-pill" :data-state="runtimePhase.state">{{ runtimePhase.label }}</span>
        <button
          type="button"
          class="danger-button"
          :disabled="shutdownStatus === 'stopping' || shutdownStatus === 'stopped'"
          @click="emit('shutdown')"
        >
          {{ shutdownButtonLabel }}
        </button>
      </div>
    </div>

    <p class="phase-detail">{{ runtimePhase.detail }}</p>
    <p v-if="shutdownMessage" class="inline-state" aria-live="polite">{{ shutdownMessage }}</p>

    <div class="cockpit-flow">
      <ConnectionStepList
        :steps="steps"
        :selected-step-id="selectedStepId"
        @select="selectedStepId = $event"
      />
      <ConnectionStepDetail
        :step="selectedStep"
        :selected-step-id="selectedStepId"
        :snapshot="snapshot"
        :copy-state="copyState"
        @copy="copyText"
      />
    </div>

    <EvidenceSummaryGrid :snapshot="snapshot" />
  </section>
</template>
