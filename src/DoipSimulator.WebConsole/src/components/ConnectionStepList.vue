<script setup lang="ts">
import type { ConnectionStepId, ConnectionStepViewModel } from "../connectionWorkflow";

defineProps<{
  steps: ConnectionStepViewModel[];
  selectedStepId: ConnectionStepId;
}>();

const emit = defineEmits<{
  select: [stepId: ConnectionStepId];
}>();
</script>

<template>
  <div class="cockpit-step-list" aria-label="Diagnostic connection steps">
    <button
      v-for="step in steps"
      :key="step.id"
      type="button"
      class="cockpit-step"
      :class="{
        'is-selected': step.id === selectedStepId,
        [`is-${step.state}`]: true,
      }"
      @click="emit('select', step.id)"
    >
      <span class="cockpit-step__index">{{ step.index }}</span>
      <span class="cockpit-step__body">
        <strong>{{ step.title }}</strong>
        <small>{{ step.subtitle }}</small>
      </span>
      <span class="cockpit-step__state">{{ step.state }}</span>
    </button>
  </div>
</template>
