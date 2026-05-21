<script setup lang="ts">
import {
  collectWorkflowEvidence,
  formatDidPreview,
  formatEventSummary,
  type RuntimeCockpitSnapshot,
} from "../connectionWorkflow";

const props = defineProps<{
  snapshot: RuntimeCockpitSnapshot;
}>();
</script>

<template>
  <div class="cockpit-evidence-grid">
    <article class="cockpit-evidence">
      <h3>Latest traffic</h3>
      <p>DoIP: {{ formatEventSummary(collectWorkflowEvidence(props.snapshot.recentEvents).latestDoip) }}</p>
      <p>UDS: {{ formatEventSummary(collectWorkflowEvidence(props.snapshot.recentEvents).latestUdsResponse) }}</p>
    </article>

    <article class="cockpit-evidence">
      <h3>Evidence</h3>
      <p>Events: {{ props.snapshot.recentEvents.length }} recent</p>
      <p>
        PCAP:
        <span v-if="props.snapshot.pcapStatus?.recording">
          recording, {{ props.snapshot.pcapStatus.bytesWritten }} bytes
        </span>
        <span v-else>not recording</span>
      </p>
    </article>

    <article class="cockpit-evidence">
      <h3>DID preview</h3>
      <p>{{ formatDidPreview(props.snapshot.didSamples) }}</p>
      <p>Open Data workspace for dynamic provider editing and live charts.</p>
    </article>
  </div>
</template>
