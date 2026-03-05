import { useState, type Dispatch, type SetStateAction } from "react";

export type ToastVariant = "default" | "success" | "destructive" | "warning";

export interface Toast {
  id: string;
  title?: string;
  description?: string;
  variant?: ToastVariant;
  duration?: number;
}

type ToastInput = Omit<Toast, "id">;

let _setState: Dispatch<SetStateAction<Toast[]>> | null = null;

export function useToastStore() {
  const [toasts, setToasts] = useState<Toast[]>([]);
  _setState = setToasts;
  return { toasts, setToasts };
}

export function toast(input: ToastInput) {
  if (!_setState) return;
  const id = Math.random().toString(36).slice(2);
  const t: Toast = { id, duration: 4000, ...input };
  _setState((prev) => [...prev, t]);
  setTimeout(() => {
    _setState?.((prev) => prev.filter((x) => x.id !== id));
  }, t.duration);
}

export function useToast() {
  return { toast };
}
