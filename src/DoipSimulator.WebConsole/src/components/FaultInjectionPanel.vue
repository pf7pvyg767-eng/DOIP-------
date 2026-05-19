<script setup lang="ts">
import { onMounted, ref } from "vue";
import {
  configureNextNrc,
  loadConnections,
  loadFaults,
  triggerFaultDisconnect,
  updateFaultProfile,
  type ConnectionSnapshot,
  type FaultProfile,
  type FaultRuntimeSnapshot,
} from "../api";

const snapshot = ref<FaultRuntimeSnapshot | null>(null);
const connections = ref<ConnectionSnapshot[]>([]);
const selectedConnectionId = ref("");
const nextNrcServiceId = ref("0x22");
const nextNrc = ref("0x31");
const customResponseServiceId = ref("0x22");
const customResponseBytes = ref("62F190CAFE");
const isBusy = ref(false);
const errorMessage = ref("");

onMounted(refresh);

async function refresh(): Promise<void> {
  isBusy.value = true;
  errorMessage.value = "";

  try {
    const [faultState, activeConnections] = await Promise.all([loadFaults(), loadConnections()]);
    snapshot.value = faultState;
    connections.value = activeConnections;
    selectedConnectionId.value = activeConnections[0]?.connectionId ?? "";
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Fault state could not be loaded.";
  } finally {
    isBusy.value = false;
  }
}

async function saveProfile(patch: Partial<FaultProfile>): Promise<void> {
  if (!snapshot.value) {
    return;
  }

  const profile: FaultProfile = {
    ...snapshot.value.profile,
    ...patch,
    corruptNextDoipHeader: {
      ...snapshot.value.profile.corruptNextDoipHeader,
      ...(patch.corruptNextDoipHeader ?? {}),
    },
  };

  await run(async () => {
    snapshot.value = await updateFaultProfile(profile);
  });
}

async function disconnectSelected(): Promise<void> {
  await run(async () => {
    await triggerFaultDisconnect(selectedConnectionId.value);
    connections.value = await loadConnections();
  });
}

async function setNextNrc(): Promise<void> {
  await run(async () => {
    snapshot.value = await configureNextNrc(nextNrcServiceId.value, nextNrc.value);
  });
}

async function setCustomResponse(): Promise<void> {
  await saveProfile({
    nextCustomResponse: {
      serviceId: customResponseServiceId.value,
      responseBytes: customResponseBytes.value,
    },
  });
}

async function run(operation: () => Promise<void>): Promise<void> {
  isBusy.value = true;
  errorMessage.value = "";
  try {
    await operation();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Fault operation failed.";
  } finally {
    isBusy.value = false;
  }
}
</script>

<template>
  <section class="section" aria-labelledby="fault-title">
    <div class="section__heading section__heading--row">
      <div>
        <p class="eyebrow">Fault Injection</p>
        <h2 id="fault-title">Manual strategy controls</h2>
      </div>
      <button class="utility-button" type="button" :disabled="isBusy" @click="refresh">Refresh</button>
    </div>

    <div v-if="errorMessage" class="inline-state inline-state--error" role="alert">
      {{ errorMessage }}
    </div>

    <template v-if="snapshot">
      <div class="fault-grid">
        <label class="toggle-row">
          <input
            type="checkbox"
            :checked="snapshot.profile.enabled"
            :disabled="isBusy"
            @change="saveProfile({ enabled: ($event.target as HTMLInputElement).checked })"
          />
          <span>Enabled</span>
        </label>

        <label class="toggle-row">
          <input
            type="checkbox"
            :checked="snapshot.profile.pauseResponses"
            :disabled="isBusy"
            @change="saveProfile({ pauseResponses: ($event.target as HTMLInputElement).checked })"
          />
          <span>Pause responses</span>
        </label>

        <label class="toggle-row">
          <input
            type="checkbox"
            :checked="snapshot.profile.routingActivationFailure"
            :disabled="isBusy"
            @change="saveProfile({ routingActivationFailure: ($event.target as HTMLInputElement).checked })"
          />
          <span>Routing Activation failure</span>
        </label>

        <label class="field-row">
          <span>Response delay ms</span>
          <input
            type="number"
            min="0"
            :value="snapshot.profile.responseDelayMs"
            :disabled="isBusy"
            @change="saveProfile({ responseDelayMs: Number(($event.target as HTMLInputElement).value) })"
          />
        </label>

        <label class="toggle-row">
          <input
            type="checkbox"
            :checked="snapshot.profile.corruptNextDoipHeader.inverseVersion"
            :disabled="isBusy"
            @change="saveProfile({ corruptNextDoipHeader: { ...snapshot!.profile.corruptNextDoipHeader, inverseVersion: ($event.target as HTMLInputElement).checked } })"
          />
          <span>Corrupt next inverse version</span>
        </label>

        <label class="field-row">
          <span>Payload length delta</span>
          <input
            type="number"
            :value="snapshot.profile.corruptNextDoipHeader.payloadLengthDelta"
            :disabled="isBusy"
            @change="saveProfile({ corruptNextDoipHeader: { ...snapshot!.profile.corruptNextDoipHeader, payloadLengthDelta: Number(($event.target as HTMLInputElement).value) } })"
          />
        </label>
      </div>

      <div class="fault-actions">
        <label class="field-row">
          <span>Connection</span>
          <select v-model="selectedConnectionId" :disabled="isBusy || connections.length === 0">
            <option value="">No active connection</option>
            <option v-for="connection in connections" :key="connection.connectionId" :value="connection.connectionId">
              {{ connection.connectionId }} · {{ connection.remoteEndpoint }}
            </option>
          </select>
        </label>
        <button class="utility-button" type="button" :disabled="isBusy || !selectedConnectionId" @click="disconnectSelected">
          Disconnect
        </button>

        <label class="field-row">
          <span>Service ID</span>
          <input v-model="nextNrcServiceId" :disabled="isBusy" />
        </label>
        <label class="field-row">
          <span>NRC</span>
          <input v-model="nextNrc" :disabled="isBusy" />
        </label>
        <button class="primary-button" type="button" :disabled="isBusy" @click="setNextNrc">Set next NRC</button>
      </div>

      <div class="fault-actions fault-actions--custom">
        <label class="field-row">
          <span>Custom service ID</span>
          <input v-model="customResponseServiceId" :disabled="isBusy" />
        </label>
        <label class="field-row">
          <span>Custom response bytes</span>
          <input v-model="customResponseBytes" :disabled="isBusy" />
        </label>
        <button class="primary-button" type="button" :disabled="isBusy" @click="setCustomResponse">
          Set custom response
        </button>
      </div>
    </template>
  </section>
</template>
