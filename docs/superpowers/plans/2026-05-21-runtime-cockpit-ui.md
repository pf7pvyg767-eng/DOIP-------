# Runtime Cockpit UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Overview workspace's connection guide with a real diagnostic connection cockpit that shows a four-step DoIP/UDS workflow, state-driven step detail, copy actions, and compact evidence summaries.

**Architecture:** Keep `DashboardView.vue` as the data orchestrator and move the cockpit UI into focused Vue components. Add a small TypeScript workflow model so phase derivation, step selection, copy payloads, and evidence formatting are testable by build-time type checking and lightweight UI smoke.

**Tech Stack:** Vue 3 Composition API, TypeScript, Vite, existing Web API endpoints, existing runtime event WebSocket, PowerShell smoke scripts.

---

## File Structure

- Create: `src/DoipSimulator.WebConsole/src/components/RuntimeCockpitPanel.vue`
  - Owns the Overview cockpit layout.
  - Receives runtime summary, connections, metrics, recent events, DID samples, shutdown state, and callbacks as props.
  - Owns selected step state and copy feedback state.

- Create: `src/DoipSimulator.WebConsole/src/components/ConnectionStepList.vue`
  - Renders the four workflow steps.
  - Emits the selected step id.

- Create: `src/DoipSimulator.WebConsole/src/components/ConnectionStepDetail.vue`
  - Renders the selected step details.
  - Switches between parameter-first and troubleshooting-first content based on step state.

- Create: `src/DoipSimulator.WebConsole/src/components/EvidenceSummaryGrid.vue`
  - Renders latest traffic, PCAP/event evidence, and DID preview.

- Create: `src/DoipSimulator.WebConsole/src/connectionWorkflow.ts`
  - Defines step ids, step state, input snapshot type, derivation helpers, evidence helpers, and copy payload helpers.

- Modify: `src/DoipSimulator.WebConsole/src/views/DashboardView.vue`
  - Import and render `RuntimeCockpitPanel` inside the Overview workspace.
  - Load DID samples and PCAP status for Overview evidence.
  - Remove the old inline connection-guide section once the cockpit renders the same shutdown and runtime summary behavior.

- Modify: `src/DoipSimulator.WebConsole/src/api.ts`
  - Reuse existing `loadDidSamples()` and `loadPcapStatus()`.
  - Add no new endpoint unless implementation discovers a real data gap.

- Modify: `src/DoipSimulator.WebConsole/src/styles.css`
  - Add cockpit, step list, detail panel, copy action, and evidence grid styles using existing CSS variables.

- Create: `scripts/web-console-runtime-cockpit-smoke.ps1`
  - Lightweight source/build smoke for the new UI labels and build.
  - Does not require MSI or full browser E2E.

---

### Task 1: Add Workflow Model

**Files:**
- Create: `src/DoipSimulator.WebConsole/src/connectionWorkflow.ts`
- Verify: `src/DoipSimulator.WebConsole`

- [ ] **Step 1: Create the workflow model file**

Add this file:

```ts
import type {
  ConnectionSnapshot,
  DidRuntimeSample,
  PcapRecordingStatus,
  RuntimeEvent,
  RuntimeMetricsSnapshot,
  RuntimeSummaryResponse,
} from "./api";

export type ConnectionStepId = "udp-discovery" | "tcp-connect" | "routing-activation" | "uds-read-did";
export type ConnectionStepState = "not-started" | "waiting" | "active" | "passed" | "failed";

export interface RuntimeCockpitSnapshot {
  runtimeSummary: RuntimeSummaryResponse | null;
  connections: ConnectionSnapshot[];
  recentEvents: RuntimeEvent[];
  metrics: RuntimeMetricsSnapshot | null;
  didSamples: DidRuntimeSample[];
  pcapStatus: PcapRecordingStatus | null;
  runtimeSummaryError: string;
}

export interface ConnectionStepViewModel {
  id: ConnectionStepId;
  index: number;
  title: string;
  subtitle: string;
  state: ConnectionStepState;
}

export interface WorkflowEvidence {
  latestDoip: RuntimeEvent | null;
  latestUdsRequest: RuntimeEvent | null;
  latestUdsResponse: RuntimeEvent | null;
  latestDidRead: RuntimeEvent | null;
}

export const connectionStepOrder: ConnectionStepId[] = [
  "udp-discovery",
  "tcp-connect",
  "routing-activation",
  "uds-read-did",
];

export function buildConnectionSteps(snapshot: RuntimeCockpitSnapshot): ConnectionStepViewModel[] {
  const hasSummary = snapshot.runtimeSummary !== null && snapshot.runtimeSummaryError.length === 0;
  const hasOpenConnection = snapshot.connections.some((connection) => connection.state !== "closed");
  const hasRouting = snapshot.connections.some((connection) => connection.routingActivated);
  const hasUds = snapshot.recentEvents.some((event) =>
    event.name === "uds.request.received" ||
    event.name === "uds.response.sent" ||
    event.name === "uds.did.read");

  return [
    {
      id: "udp-discovery",
      index: 1,
      title: "UDP Discovery",
      subtitle: hasSummary ? "Vehicle identification endpoint is available" : "Waiting for runtime summary",
      state: hasSummary ? "passed" : "waiting",
    },
    {
      id: "tcp-connect",
      index: 2,
      title: "TCP Connect",
      subtitle: hasOpenConnection ? "Tester socket is open" : "Waiting for tester TCP connection",
      state: hasOpenConnection ? "passed" : hasSummary ? "waiting" : "not-started",
    },
    {
      id: "routing-activation",
      index: 3,
      title: "Routing Activation",
      subtitle: hasRouting ? "Routing activation accepted" : "Waiting for activation request",
      state: hasRouting ? "passed" : hasOpenConnection ? "active" : "not-started",
    },
    {
      id: "uds-read-did",
      index: 4,
      title: "UDS Read DID",
      subtitle: hasUds ? "UDS traffic observed" : "Ready for ReadDataByIdentifier",
      state: hasUds ? "active" : hasRouting ? "waiting" : "not-started",
    },
  ];
}

export function selectDefaultStep(steps: ConnectionStepViewModel[]): ConnectionStepId {
  return steps.find((step) => step.state === "active" || step.state === "waiting" || step.state === "failed")?.id
    ?? steps[steps.length - 1].id;
}

export function collectWorkflowEvidence(events: RuntimeEvent[]): WorkflowEvidence {
  return {
    latestDoip: findLatest(events, (event) => event.category === "doip"),
    latestUdsRequest: findLatest(events, (event) => event.name === "uds.request.received"),
    latestUdsResponse: findLatest(events, (event) => event.name === "uds.response.sent"),
    latestDidRead: findLatest(events, (event) => event.name === "uds.did.read"),
  };
}

export function buildCopyText(stepId: ConnectionStepId, snapshot: RuntimeCockpitSnapshot): string {
  const summary = snapshot.runtimeSummary;
  if (!summary) {
    return "Runtime summary is unavailable.";
  }

  if (stepId === "udp-discovery") {
    return `DoIP UDP discovery: ${summary.webApiListenAddress}:${summary.doipUdpPort}`;
  }

  if (stepId === "tcp-connect") {
    return `DoIP TCP connect: ${summary.webApiListenAddress}:${summary.doipTcpPort}`;
  }

  if (stepId === "routing-activation") {
    const tester = summary.testerSourceAddressWhitelist[0] ?? "0x0E00";
    return `Routing Activation: tester ${tester} -> ECU ${summary.ecuLogicalAddress}`;
  }

  return `UDS ReadDataByIdentifier: target ECU ${summary.ecuLogicalAddress}, request 22 F1 90`;
}

export function formatEventSummary(event: RuntimeEvent | null): string {
  if (!event) {
    return "Unavailable";
  }

  return `${event.name} at ${formatTime(event.timestamp)}`;
}

export function formatDidPreview(samples: DidRuntimeSample[]): string {
  const sample = samples.find((item) => typeof item.numericValue === "number") ?? samples[0];
  if (!sample) {
    return "No DID sample";
  }

  const value = typeof sample.numericValue === "number" ? sample.numericValue.toString() : sample.rawValue;
  return `${sample.did}: ${value}`;
}

function findLatest(events: RuntimeEvent[], predicate: (event: RuntimeEvent) => boolean): RuntimeEvent | null {
  return [...events].reverse().find(predicate) ?? null;
}

function formatTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleTimeString();
}
```

- [ ] **Step 2: Run frontend build to verify TypeScript**

Run:

```powershell
cd .\src\DoipSimulator.WebConsole
npm.cmd run build
```

Expected: `vue-tsc --noEmit` succeeds and Vite produces a production build.

- [ ] **Step 3: Commit**

```powershell
git add src/DoipSimulator.WebConsole/src/connectionWorkflow.ts
git commit -m "feat: add runtime cockpit workflow model"
```

---

### Task 2: Add Cockpit Presentation Components

**Files:**
- Create: `src/DoipSimulator.WebConsole/src/components/ConnectionStepList.vue`
- Create: `src/DoipSimulator.WebConsole/src/components/ConnectionStepDetail.vue`
- Create: `src/DoipSimulator.WebConsole/src/components/EvidenceSummaryGrid.vue`
- Create: `src/DoipSimulator.WebConsole/src/components/RuntimeCockpitPanel.vue`
- Modify: `src/DoipSimulator.WebConsole/src/styles.css`

- [ ] **Step 1: Create `ConnectionStepList.vue`**

```vue
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
```

- [ ] **Step 2: Create `ConnectionStepDetail.vue`**

```vue
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
```

- [ ] **Step 3: Create `EvidenceSummaryGrid.vue`**

```vue
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
```

- [ ] **Step 4: Create `RuntimeCockpitPanel.vue`**

```vue
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

  await navigator.clipboard.writeText(text);
  copyState.value = "Copied";
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
```

- [ ] **Step 5: Add cockpit CSS**

Append these styles to `src/DoipSimulator.WebConsole/src/styles.css`:

```css
.cockpit-panel {
  display: grid;
  gap: 7px;
}

.cockpit-flow {
  display: grid;
  grid-template-columns: minmax(190px, 0.75fr) minmax(0, 1.25fr);
  gap: 7px;
}

.cockpit-step-list {
  display: grid;
  gap: 5px;
}

.cockpit-step {
  display: grid;
  grid-template-columns: 24px minmax(0, 1fr) auto;
  gap: 7px;
  align-items: center;
  min-height: 58px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: #0f1720;
  color: var(--text);
  padding: 7px;
  text-align: left;
}

.cockpit-step.is-selected {
  border-color: #38637f;
  background: linear-gradient(90deg, #123047, #111a24);
}

.cockpit-step__index {
  display: grid;
  width: 24px;
  height: 24px;
  place-items: center;
  border-radius: 6px;
  background: var(--green-soft);
  color: var(--green);
  font-size: var(--type-data);
  font-weight: 900;
}

.cockpit-step__body {
  display: grid;
  gap: 3px;
  min-width: 0;
}

.cockpit-step__body strong {
  color: var(--text);
  font-size: var(--type-section);
}

.cockpit-step__body small,
.cockpit-step__state {
  color: var(--muted);
  font-size: var(--type-label);
}

.cockpit-step__state {
  border: 1px solid var(--line-strong);
  border-radius: 6px;
  background: var(--panel-3);
  padding: 2px 5px;
  font-weight: 900;
  text-transform: capitalize;
}

.cockpit-step.is-active .cockpit-step__state,
.cockpit-step.is-passed .cockpit-step__state {
  border-color: #2c7951;
  background: var(--green-soft);
  color: #8ff0bd;
}

.cockpit-step.is-waiting .cockpit-step__state {
  border-color: #806227;
  background: var(--amber-soft);
  color: #ffd58d;
}

.cockpit-step.is-failed .cockpit-step__state {
  border-color: #7d3939;
  background: var(--red-soft);
  color: var(--red);
}

.cockpit-detail,
.cockpit-evidence {
  border: 1px solid var(--line);
  border-radius: 8px;
  background: #0f1720;
  padding: 8px;
}

.cockpit-detail__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 7px;
}

.cockpit-param-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 5px;
  margin: 0 0 7px;
}

.cockpit-copy-row {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 7px;
}

.cockpit-code {
  overflow: auto;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: #0b1118;
  color: var(--text);
  margin: 0;
  padding: 7px;
  font-family: ui-monospace, SFMono-Regular, Consolas, "Liberation Mono", monospace;
  font-size: var(--type-micro);
  line-height: 1.35;
}

.cockpit-evidence-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 7px;
}

.cockpit-evidence {
  min-height: 108px;
}

.cockpit-evidence h3 {
  margin-bottom: 5px;
}

.cockpit-evidence p + p {
  margin-top: 4px;
}

@media (max-width: 900px) {
  .cockpit-flow,
  .cockpit-evidence-grid,
  .cockpit-param-grid {
    grid-template-columns: 1fr;
  }
}
```

- [ ] **Step 6: Run frontend build**

Run:

```powershell
cd .\src\DoipSimulator.WebConsole
npm.cmd run build
```

Expected: build passes.

- [ ] **Step 7: Commit**

```powershell
git add src/DoipSimulator.WebConsole/src/components/ConnectionStepList.vue src/DoipSimulator.WebConsole/src/components/ConnectionStepDetail.vue src/DoipSimulator.WebConsole/src/components/EvidenceSummaryGrid.vue src/DoipSimulator.WebConsole/src/components/RuntimeCockpitPanel.vue src/DoipSimulator.WebConsole/src/styles.css
git commit -m "feat: add runtime cockpit components"
```

---

### Task 3: Wire Cockpit Into Dashboard Data Flow

**Files:**
- Modify: `src/DoipSimulator.WebConsole/src/views/DashboardView.vue`

- [ ] **Step 1: Add imports**

In `DashboardView.vue`, update imports from `../api` to include:

```ts
  loadDidSamples,
  loadPcapStatus,
  type DidRuntimeSample,
  type PcapRecordingStatus,
```

Remove the `StatusPanel` import if it is no longer rendered in Overview after the cockpit replaces the old connection-guide stack.

Add:

```ts
import RuntimeCockpitPanel from "../components/RuntimeCockpitPanel.vue";
import type { RuntimeCockpitSnapshot } from "../connectionWorkflow";
```

- [ ] **Step 2: Add Overview evidence state**

Near existing refs in `DashboardView.vue`, add:

```ts
const didSamples = ref<DidRuntimeSample[]>([]);
const pcapStatus = ref<PcapRecordingStatus | null>(null);
```

Add computed snapshot:

```ts
const cockpitSnapshot = computed<RuntimeCockpitSnapshot>(() => ({
  runtimeSummary: runtimeSummary.value,
  connections: phaseConnections.value,
  recentEvents: recentPhaseEvents.value,
  metrics: metrics.value,
  didSamples: didSamples.value,
  pcapStatus: pcapStatus.value,
  runtimeSummaryError: runtimeSummaryError.value,
}));
```

If `recentPhaseEvents` does not exist yet, add:

```ts
const recentPhaseEvents = ref<RuntimeEvent[]>([]);
```

- [ ] **Step 3: Preserve recent events while refreshing phase snapshots**

Replace the body of `refreshPhaseSnapshots()` with this shape:

```ts
async function refreshPhaseSnapshots(): Promise<void> {
  if (shutdownStatus.value === "stopping" || shutdownStatus.value === "stopped") {
    return;
  }

  try {
    const [connections, recentEvents] = await Promise.all([
      loadConnections(),
      loadRecentEvents(100),
    ]);
    phaseConnections.value = connections;
    recentPhaseEvents.value = recentEvents;
    udsTrafficActive.value = recentEvents.some((event) =>
      event.name === "uds.request.received" || event.name === "uds.response.sent");
  } catch {
    phaseConnections.value = [];
    recentPhaseEvents.value = [];
  }
}
```

- [ ] **Step 4: Add Overview evidence refresh**

Add:

```ts
async function refreshCockpitEvidence(): Promise<void> {
  if (shutdownStatus.value === "stopping" || shutdownStatus.value === "stopped") {
    return;
  }

  try {
    const [samples, pcap] = await Promise.all([
      loadDidSamples(),
      loadPcapStatus(),
    ]);
    didSamples.value = samples;
    pcapStatus.value = pcap;
  } catch {
    didSamples.value = [];
    pcapStatus.value = null;
  }
}
```

Call it in `onMounted()` after `refreshRuntimeInputs()`:

```ts
void refreshCockpitEvidence();
```

Call it from `refreshRuntimeInputs()`:

```ts
async function refreshRuntimeInputs(): Promise<void> {
  await Promise.all([
    refreshRuntimeSummary(),
    refreshPhaseSnapshots(),
    refreshCockpitEvidence(),
  ]);
}
```

- [ ] **Step 5: Keep event stream evidence current**

In `applyPhaseEvent(event: RuntimeEvent)`, before existing conditional logic, add:

```ts
recentPhaseEvents.value = [event, ...recentPhaseEvents.value].slice(0, 100);
```

When event name is `uds.did.read`, call:

```ts
if (event.name === "uds.did.read") {
  void refreshCockpitEvidence();
}
```

- [ ] **Step 6: Replace Overview template content**

Inside the `activeWorkspace === 'overview'` template branch, replace the old connection-guide section with:

```vue
<RuntimeCockpitPanel
  :snapshot="cockpitSnapshot"
  :runtime-phase="runtimePhase"
  :shutdown-button-label="shutdownButtonLabel"
  :shutdown-status="shutdownStatus"
  :shutdown-message="shutdownMessage"
  @shutdown="confirmRuntimeShutdown"
/>
```

Keep `MetricsPanel` and the config summary if they still add value below the cockpit. Remove `StatusPanel` if the top telemetry and cockpit make it redundant.

- [ ] **Step 7: Run frontend build**

Run:

```powershell
cd .\src\DoipSimulator.WebConsole
npm.cmd run build
```

Expected: build passes with no TypeScript errors.

- [ ] **Step 8: Commit**

```powershell
git add src/DoipSimulator.WebConsole/src/views/DashboardView.vue
git commit -m "feat: wire runtime cockpit into overview"
```

---

### Task 4: Add Lightweight Cockpit Smoke

**Files:**
- Create: `scripts/web-console-runtime-cockpit-smoke.ps1`
- Modify: `README.md`

- [ ] **Step 1: Create smoke script**

```powershell
param(
    [string]$WebConsoleRoot = ".\src\DoipSimulator.WebConsole"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path $WebConsoleRoot
$requiredFiles = @(
    "src\components\RuntimeCockpitPanel.vue",
    "src\components\ConnectionStepList.vue",
    "src\components\ConnectionStepDetail.vue",
    "src\components\EvidenceSummaryGrid.vue",
    "src\connectionWorkflow.ts"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $root $file
    if (-not (Test-Path $path)) {
        Write-Host "FAIL missing $file" -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS found $file" -ForegroundColor Green
}

$dashboard = Get-Content (Join-Path $root "src\views\DashboardView.vue") -Raw
$labels = @(
    "RuntimeCockpitPanel",
    "Diagnostic connection workflow",
    "confirmRuntimeShutdown"
)

foreach ($label in $labels) {
    if ($dashboard -notmatch [regex]::Escape($label)) {
        Write-Host "FAIL DashboardView missing $label" -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS DashboardView contains $label" -ForegroundColor Green
}

Push-Location $root
try {
    npm.cmd run build
} finally {
    Pop-Location
}
```

- [ ] **Step 2: Document smoke command**

Add to `README.md` under the existing Phase 2 functional smoke section:

```md
For the runtime cockpit UI smoke:

```powershell
.\scripts\web-console-runtime-cockpit-smoke.ps1
```
```

- [ ] **Step 3: Run the smoke**

Run:

```powershell
.\scripts\web-console-runtime-cockpit-smoke.ps1
```

Expected: all `PASS` checks print and `npm.cmd run build` succeeds.

- [ ] **Step 4: Commit**

```powershell
git add scripts/web-console-runtime-cockpit-smoke.ps1 README.md
git commit -m "test: add runtime cockpit UI smoke"
```

---

### Task 5: Browser Review And Final Verification

**Files:**
- Verify: `src/DoipSimulator.WebConsole`
- Verify: `DoipSimulator.sln`

- [ ] **Step 1: Run frontend build**

```powershell
cd .\src\DoipSimulator.WebConsole
npm.cmd run build
```

Expected: build passes.

- [ ] **Step 2: Run backend regression tests**

Use the local .NET SDK and Visual Studio Build Tools environment:

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
cmd /c 'call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" && dotnet test DoipSimulator.sln --no-restore'
```

Expected: all existing backend tests pass.

- [ ] **Step 3: Run cockpit smoke**

```powershell
.\scripts\web-console-runtime-cockpit-smoke.ps1
```

Expected: all cockpit file/label checks pass and the frontend build passes.

- [ ] **Step 4: Manual browser review**

Start the runtime and frontend dev server:

```powershell
.\scripts\run-host.ps1 run
cd .\src\DoipSimulator.WebConsole
npm.cmd run dev
```

Open the Vite URL in the browser and verify:

- Overview shows `Diagnostic connection workflow`.
- The four steps are visible: `UDP Discovery`, `TCP Connect`, `Routing Activation`, `UDS Read DID`.
- Selecting a step changes the detail panel.
- Copy buttons show `Copied` after use.
- Shutdown button still prompts before stopping runtime.
- Right realtime rail still renders.
- The page does not overlap or overflow at desktop width.

- [ ] **Step 5: Commit verification notes if needed**

If a task log is maintained, update it with the exact commands and results:

```powershell
git add docs/Phase2-Task-Plan.md
git commit -m "docs: record runtime cockpit verification"
```

Skip this commit if no task log is updated.

---

## Self-Review

Spec coverage:

- Runtime cockpit layout: covered by Tasks 2 and 3.
- Four-step connection workflow: covered by Tasks 1, 2, and 3.
- State-driven detail panel: covered by Tasks 1 and 2.
- Copy actions: covered by Tasks 1 and 2.
- Evidence summary: covered by Tasks 1, 2, and 3.
- Existing visual language: covered by Task 2 CSS and Task 5 browser review.
- Real runtime data only: covered by Task 3 data wiring.
- Error/shutdown behavior: covered by Task 3 and Task 5 manual review.
- Verification: covered by Tasks 4 and 5.

No placeholders are intentionally left in this plan. Any implementation that discovers missing API data should prefer using existing endpoints first and only propose backend changes through OpenSpec if a real data gap blocks the UI.
