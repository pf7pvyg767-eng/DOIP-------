<script setup lang="ts">
import { computed, ref } from "vue";
import { uploadDiagnosticImport, type ImportReport } from "../api";

const selectedFile = ref<File | null>(null);
const report = ref<ImportReport | null>(null);
const errorMessage = ref("");
const isUploading = ref(false);

const detectedKind = computed<"odx" | "pdx" | null>(() => {
  const name = selectedFile.value?.name.toLowerCase() ?? "";
  if (name.endsWith(".odx")) {
    return "odx";
  }

  if (name.endsWith(".pdx")) {
    return "pdx";
  }

  return null;
});

function onFileChange(event: Event): void {
  const input = event.target as HTMLInputElement;
  selectedFile.value = input.files?.[0] ?? null;
  report.value = null;
  errorMessage.value = "";
}

async function submit(): Promise<void> {
  if (!selectedFile.value || !detectedKind.value) {
    errorMessage.value = "Select an .odx or .pdx file.";
    return;
  }

  isUploading.value = true;
  errorMessage.value = "";
  report.value = null;
  try {
    report.value = await uploadDiagnosticImport(detectedKind.value, selectedFile.value);
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Import failed.";
  } finally {
    isUploading.value = false;
  }
}
</script>

<template>
  <section class="section" aria-labelledby="import-title">
    <div class="section__heading">
      <p class="eyebrow">Import</p>
      <h2 id="import-title">ODX / PDX subset import</h2>
    </div>

    <div class="import-controls">
      <input type="file" accept=".odx,.pdx" @change="onFileChange" />
      <button type="button" :disabled="isUploading || !selectedFile" @click="submit">
        {{ isUploading ? "Importing" : "Import" }}
      </button>
    </div>

    <p v-if="errorMessage" class="inline-state inline-state--error" role="alert">{{ errorMessage }}</p>

    <div v-if="report" class="import-report" :class="{ 'import-report--failed': !report.success }">
      <dl class="facts facts--three">
        <div class="fact">
          <dt>Status</dt>
          <dd>{{ report.success ? "Success" : "Failed" }}</dd>
        </div>
        <div class="fact">
          <dt>Saved</dt>
          <dd>{{ report.saved ? "Yes" : "No" }}</dd>
        </div>
        <div class="fact">
          <dt>DIDs</dt>
          <dd>{{ report.imported.dids }}</dd>
        </div>
      </dl>

      <div v-if="report.skipped.length" class="import-list">
        <h3>Skipped</h3>
        <ul>
          <li v-for="item in report.skipped" :key="`${item.path}:${item.reason}`">
            <span>{{ item.path }}</span>
            <small>{{ item.reason }}</small>
          </li>
        </ul>
      </div>

      <div v-if="report.errors.length" class="import-list import-list--errors">
        <h3>Errors</h3>
        <ul>
          <li v-for="item in report.errors" :key="`${item.path}:${item.message}`">
            <span>{{ item.path }}</span>
            <small>{{ item.message }}</small>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>
