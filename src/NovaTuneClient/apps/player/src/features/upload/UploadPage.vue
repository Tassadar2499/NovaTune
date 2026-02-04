<script setup lang="ts">
import { ref } from 'vue';
import { useAuthStore } from '@/stores/auth';

const auth = useAuthStore();

const file = ref<File | null>(null);
const title = ref('');
const artist = ref('');
const isUploading = ref(false);
const uploadProgress = ref(0);
const error = ref<string | null>(null);
const success = ref(false);

function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  if (input.files?.length) {
    file.value = input.files[0];
    // Auto-fill title from filename
    if (!title.value) {
      title.value = file.value.name.replace(/\.[^/.]+$/, '');
    }
  }
}

async function handleUpload() {
  if (!file.value) return;

  isUploading.value = true;
  uploadProgress.value = 0;
  error.value = null;
  success.value = false;

  try {
    // Step 1: Initiate upload
    const initiateResponse = await fetch(`${import.meta.env.VITE_API_BASE_URL}/uploads/initiate`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${auth.accessToken}`,
      },
      body: JSON.stringify({
        filename: file.value.name,
        contentType: file.value.type,
        size: file.value.size,
        title: title.value,
        artist: artist.value,
      }),
    });

    if (!initiateResponse.ok) {
      throw new Error('Failed to initiate upload');
    }

    const { uploadId, uploadUrl } = await initiateResponse.json();
    uploadProgress.value = 10;

    // Step 2: Upload file to presigned URL
    const uploadResponse = await fetch(uploadUrl, {
      method: 'PUT',
      body: file.value,
      headers: {
        'Content-Type': file.value.type,
      },
    });

    if (!uploadResponse.ok) {
      throw new Error('Failed to upload file');
    }

    uploadProgress.value = 80;

    // Step 3: Complete upload
    const completeResponse = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/uploads/${uploadId}/complete`,
      {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      }
    );

    if (!completeResponse.ok) {
      throw new Error('Failed to complete upload');
    }

    uploadProgress.value = 100;
    success.value = true;

    // Reset form
    file.value = null;
    title.value = '';
    artist.value = '';
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Upload failed';
  } finally {
    isUploading.value = false;
  }
}
</script>

<template>
  <div class="max-w-xl">
    <h1 class="text-2xl font-bold text-white mb-8">Upload Track</h1>

    <div v-if="success" class="card bg-green-900/30 border-green-700 mb-6">
      <div class="flex items-center gap-3">
        <svg class="w-6 h-6 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
        </svg>
        <p class="text-green-200">Track uploaded successfully! It may take a moment to appear in your library.</p>
      </div>
    </div>

    <div v-if="error" class="card bg-red-900/30 border-red-700 mb-6">
      <p class="text-red-200">{{ error }}</p>
    </div>

    <form @submit.prevent="handleUpload" class="space-y-6">
      <div>
        <label class="block text-sm font-medium text-slate-300 mb-2">Audio File</label>
        <div
          class="border-2 border-dashed border-slate-600 rounded-lg p-8 text-center hover:border-slate-500 transition-colors"
          :class="{ 'border-primary-500 bg-primary-500/10': file }"
        >
          <input
            type="file"
            accept="audio/*"
            @change="handleFileChange"
            class="hidden"
            id="file-input"
          />
          <label for="file-input" class="cursor-pointer">
            <svg class="w-12 h-12 text-slate-500 mx-auto mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
            </svg>
            <p v-if="file" class="text-white font-medium">{{ file.name }}</p>
            <p v-else class="text-slate-400">
              Click to select a file or drag and drop
            </p>
            <p class="text-slate-500 text-sm mt-2">MP3, FLAC, WAV, or OGG up to 100MB</p>
          </label>
        </div>
      </div>

      <div>
        <label for="title" class="block text-sm font-medium text-slate-300 mb-1">Title</label>
        <input
          id="title"
          v-model="title"
          type="text"
          required
          class="input"
          placeholder="Track title"
        />
      </div>

      <div>
        <label for="artist" class="block text-sm font-medium text-slate-300 mb-1">Artist</label>
        <input
          id="artist"
          v-model="artist"
          type="text"
          required
          class="input"
          placeholder="Artist name"
        />
      </div>

      <div v-if="isUploading" class="space-y-2">
        <div class="flex justify-between text-sm">
          <span class="text-slate-400">Uploading...</span>
          <span class="text-slate-400">{{ uploadProgress }}%</span>
        </div>
        <div class="w-full bg-slate-700 rounded-full h-2">
          <div
            class="bg-primary-500 h-2 rounded-full transition-all duration-300"
            :style="{ width: `${uploadProgress}%` }"
          ></div>
        </div>
      </div>

      <button
        type="submit"
        :disabled="!file || isUploading"
        class="w-full btn-primary disabled:opacity-50"
      >
        {{ isUploading ? 'Uploading...' : 'Upload Track' }}
      </button>
    </form>
  </div>
</template>
