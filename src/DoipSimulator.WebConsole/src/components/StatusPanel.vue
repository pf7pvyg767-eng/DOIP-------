<script setup lang="ts">
import type { HealthResponse } from "../api";

const props = defineProps<{
  health: HealthResponse;
}>();

const unavailable = "Unavailable";

function display(value: string | undefined): string {
  return value && value.trim().length > 0 ? value : unavailable;
}

function formatStartedAt(value: string | undefined): string {
  if (!value) {
    return unavailable;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}
</script>

<template>
  <section class="section" aria-labelledby="service-status-title">
    <div class="section__heading">
      <p class="eyebrow">Service</p>
      <h2 id="service-status-title">Service status</h2>
    </div>

    <dl class="facts facts--three">
      <div class="fact">
        <dt>Status</dt>
        <dd>
          <span class="status-pill">{{ display(props.health.status) }}</span>
        </dd>
      </div>
      <div class="fact">
        <dt>Started at</dt>
        <dd>{{ formatStartedAt(props.health.startedAt) }}</dd>
      </div>
      <div class="fact">
        <dt>Version</dt>
        <dd>{{ display(props.health.version) }}</dd>
      </div>
    </dl>
  </section>
</template>
