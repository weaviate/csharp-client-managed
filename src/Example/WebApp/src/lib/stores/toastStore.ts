import { writable } from 'svelte/store';

export type ToastType = 'success' | 'error' | 'info' | 'warning';

export interface ToastMessage {
    id: number;
    type: ToastType;
    message: string;
    duration?: number;
}

export const toasts = writable<ToastMessage[]>([]);

let nextId = 1;

export function addToast(type: ToastType, message: string, duration = 5000) {
    const id = nextId++;
    const toast: ToastMessage = { id, type, message, duration };

    toasts.update(t => [...t, toast]);

    if (duration > 0) {
        setTimeout(() => {
            removeToast(id);
        }, duration);
    }

    return id;
}

export function removeToast(id: number) {
    toasts.update(t => t.filter(toast => toast.id !== id));
}

export function showSuccess(message: string, duration?: number) {
    return addToast('success', message, duration);
}

export function showError(message: string, duration?: number) {
    return addToast('error', message, duration);
}

export function showInfo(message: string, duration?: number) {
    return addToast('info', message, duration);
}

export function showWarning(message: string, duration?: number) {
    return addToast('warning', message, duration);
}
