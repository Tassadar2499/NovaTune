<script setup lang="ts">
import { Dialog, DialogPanel, DialogTitle, TransitionChild, TransitionRoot } from '@headlessui/vue';

interface Props {
  open: boolean;
  title?: string;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  close: [];
}>();
</script>

<template>
  <TransitionRoot :show="props.open" as="template">
    <Dialog class="relative z-50" @close="emit('close')">
      <TransitionChild
        as="template"
        enter="ease-out duration-300"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="ease-in duration-200"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-black/50" />
      </TransitionChild>

      <div class="fixed inset-0 overflow-y-auto">
        <div class="flex min-h-full items-center justify-center p-4">
          <TransitionChild
            as="template"
            enter="ease-out duration-300"
            enter-from="opacity-0 scale-95"
            enter-to="opacity-100 scale-100"
            leave="ease-in duration-200"
            leave-from="opacity-100 scale-100"
            leave-to="opacity-0 scale-95"
          >
            <DialogPanel
              class="w-full max-w-md transform rounded-lg bg-slate-800 border border-slate-700 p-6 shadow-xl transition-all"
            >
              <DialogTitle v-if="props.title" class="text-xl font-semibold text-white mb-4">
                {{ props.title }}
              </DialogTitle>
              <slot />
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
