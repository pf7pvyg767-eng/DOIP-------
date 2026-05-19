<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import {
  createRuntimeEventSocket,
  loadPcapStatus,
  startPcapRecording,
  stopPcapRecording,
  type PcapRecordingStatus,
} from "../api";

const status = ref<PcapRecordingStatus | null>(null);
const isLoading = ref(true);
const isBusy = ref(false);
const errorMessage = ref("");
let refreshTimer: number | undefined;
let eventSocket: WebSocket | undefined;

onMounted(() => {
  void refresh();
  refreshTimer = window.setInterval(() => void refresh(), 5000);
  connectEvents();
});

onUnmounted(() => {
  if (refreshTimer) {
    window.clearInterval(refreshTimer);
  }

  eventSocket?.close();
});

async function refresh(): Promise<void> {
  try {
    status.value = await loadPcapStatus();
    errorMessage.value = "";
  } catch {
    errorMessage.value = "PCAP status could not be loaded.";
  } finally {
    isLoading.value = false;
  }
}

async function start(): Promise<void> {
  await runOperation(() => startPcapRecording());
}

async function stop(): Promise<void> {
  await runOperation(() => stopPcapRecording());
}

async function runOperation(operation: () => Promise<PcapRecordingStatus>): Promise<void> {
  isBusy.value = true;
  errorMessage.value = "";

  try {
    status.value = await operation();
  } catch {
    errorMessage.value = "PCAP recording operation failed.";
  } finally {
    isBusy.value = false;
  }
}

function connectEvents(): void {
  try {
    eventSocket = createRuntimeEventSocket();
    eventSocket.addEventListener("message", (event) => {
      const payload = JSON.parse(event.data as string) as { category?: string; name?: string };
      if (payload.category === "pcap") {
        void refresh();
      }
    });
  } catch {
  }
}

function formatBytes(value: number | undefined): string {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return "Unavailable";
  }

  return value.toLocaleString();
}
</script>

<template>
  <section class="section" aria-labelledby="pcap-recording-title">
    <div class="section__heading section__heading--row">
      <div>
        <p class="eyebrow">PCAP</p>
        <h2 id="pcap-recording-title">Recording status</h2>
      </div>

      <div class="button-row">
        <button
          class="primary-button"
          type="button"
          :disabled="isBusy || status?.recording"
          @click="start"
        >
          Start
        </button>
        <button
          class="utility-button"
          type="button"
          :disabled="isBusy || !status?.recording"
          @click="stop"
        >
          Stop
        </button>
      </div>
    </div>

    <div v-if="isLoading" class="inline-state">Loading PCAP status.</div>
    <div v-else-if="errorMessage" class="inline-state inline-state--error">{{ errorMessage }}</div>
    <dl v-else-if="status" class="facts facts--pcap">
      <div class="fact">
        <dt>Status</dt>
        <dd>{{ status.recording ? "Recording" : "Stopped" }}</dd>
      </div>
      <div class="fact">
        <dt>File path</dt>
        <dd>{{ status.filePath || "Unavailable" }}</dd>
      </div>
      <div class="fact">
        <dt>Bytes written</dt>
        <dd>{{ formatBytes(status.bytesWritten) }}</dd>
      </div>
      <div class="fact">
        <dt>Max bytes</dt>
        <dd>{{ formatBytes(status.maxBytes) }}</dd>
      </div>
    </dl>
  </section>
</template>
