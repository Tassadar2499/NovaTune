import { ref } from 'vue';

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info' | 'warning';
  duration?: number;
}

const toasts = ref<Toast[]>([]);

export function useToast() {
  function show(message: string, type: Toast['type'] = 'info', duration = 3000) {
    const id = crypto.randomUUID();
    toasts.value.push({ id, message, type, duration });

    if (duration > 0) {
      setTimeout(() => {
        dismiss(id);
      }, duration);
    }

    return id;
  }

  function success(message: string, duration?: number) {
    return show(message, 'success', duration);
  }

  function error(message: string, duration?: number) {
    return show(message, 'error', duration);
  }

  function info(message: string, duration?: number) {
    return show(message, 'info', duration);
  }

  function warning(message: string, duration?: number) {
    return show(message, 'warning', duration);
  }

  function dismiss(id: string) {
    const index = toasts.value.findIndex((t) => t.id === id);
    if (index !== -1) {
      toasts.value.splice(index, 1);
    }
  }

  function dismissAll() {
    toasts.value = [];
  }

  return {
    toasts,
    show,
    success,
    error,
    info,
    warning,
    dismiss,
    dismissAll,
  };
}
