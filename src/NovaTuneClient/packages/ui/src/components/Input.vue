<script setup lang="ts">
interface Props {
  modelValue?: string;
  type?: string;
  placeholder?: string;
  disabled?: boolean;
  error?: string;
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: '',
  type: 'text',
  placeholder: '',
  disabled: false,
});

const emit = defineEmits<{
  'update:modelValue': [value: string];
}>();
</script>

<template>
  <div>
    <input
      :type="props.type"
      :value="props.modelValue"
      :placeholder="props.placeholder"
      :disabled="props.disabled"
      @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      :class="[
        'w-full px-4 py-2 bg-slate-800 border rounded-lg text-white placeholder-slate-400',
        'focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent',
        'disabled:opacity-50 disabled:cursor-not-allowed',
        props.error ? 'border-red-500' : 'border-slate-700',
      ]"
    />
    <p v-if="props.error" class="mt-1 text-sm text-red-400">
      {{ props.error }}
    </p>
  </div>
</template>
