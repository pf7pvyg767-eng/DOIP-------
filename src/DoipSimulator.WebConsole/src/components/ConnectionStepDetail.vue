<script setup lang="ts">
import {
  buildCopyText,
  collectWorkflowEvidence,
  formatEventSummary,
  type ConnectionStepId,
  type ConnectionStepViewModel,
  type RuntimeCockpitSnapshot,
} from "../connectionWorkflow";

const props = defineProps<{
  step: ConnectionStepViewModel;
  selectedStepId: ConnectionStepId;
  snapshot: RuntimeCockpitSnapshot;
  copyState: string;
}>();

const emit = defineEmits<{
  copy: [text: string];
}>();

function copyCurrent(): void {
  emit("copy", buildCopyText(props.selectedStepId, props.snapshot));
}

function firstTesterAddress(): string {
  return props.snapshot.runtimeSummary?.testerSourceAddressWhitelist[0] ?? "0x0E00";
}

function selectedHint(): string {
  if (props.step.state === "failed") {
    return "Check the latest response, source address whitelist, and tester target logical address.";
  }

  if (props.selectedStepId === "uds-read-did") {
    return "Send ReadDataByIdentifier 22 F1 90 and confirm the response starts with 62 F1 90.";
  }

  if (props.selectedStepId === "routing-activation") {
    return "After activation is accepted, continue with UDS ReadDataByIdentifier.";
  }

  return "Use the copied parameters in your diagnostic tester, then watch the next step become active.";
}
</script>

<template>
  <article class="cockpit-detail" aria-live="polite">
    <header class="cockpit-detail__header">
      <div>
        <p class="eyebrow">Selected step</p>
        <h3>{{ step.title }}</h3>
      </div>
      <button class="primary-button" type="button" @click="copyCurrent">
        {{ copyState || "Copy action" }}
      </button>
    </header>

    <p
      class="inline-state"
      :class="{
        'inline-state--warning': step.state === 'waiting' || step.state === 'active',
        'inline-state--success': step.state === 'passed',
        'inline-state--error': step.state === 'failed',
      }"
    >
      {{ selectedHint() }}
    </p>

    <dl class="cockpit-param-grid">
      <div class="fact">
        <dt>Web API</dt>
        <dd>{{ snapshot.runtimeSummary?.webApiEndpoint ?? "Unavailable" }}</dd>
      </div>
      <div class="fact">
        <dt>DoIP TCP</dt>
        <dd>{{ snapshot.runtimeSummary?.doipTcpPort ?? "Unavailable" }}</dd>
      </div>
      <div class="fact">
        <dt>Tester SA</dt>
        <dd>{{ firstTesterAddress() }}</dd>
      </div>
      <div class="fact">
        <dt>ECU logical</dt>
        <dd>{{ snapshot.runtimeSummary?.ecuLogicalAddress ?? "Unavailable" }}</dd>
      </div>
    </dl>

    <div class="cockpit-copy-row">
      <button class="utility-button" type="button" @click="emit('copy', snapshot.runtimeSummary?.webApiEndpoint ?? '')">
        Copy API
      </button>
      <button class="utility-button" type="button" @click="emit('copy', buildCopyText('tcp-connect', snapshot))">
        Copy DoIP TCP
      </button>
      <button class="utility-button" type="button" @click="emit('copy', buildCopyText('uds-read-did', snapshot))">
        Copy UDS 22 F1 90
      </button>
    </div>

    <pre class="cockpit-code">{{ formatEventSummary(collectWorkflowEvidence(snapshot.recentEvents).latestDoip) }}
{{ formatEventSummary(collectWorkflowEvidence(snapshot.recentEvents).latestUdsRequest) }}
{{ formatEventSummary(collectWorkflowEvidence(snapshot.recentEvents).latestUdsResponse) }}</pre>
  </article>
</template>
