import * as ToastPrimitive from "@radix-ui/react-toast";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";
import { useToastStore, type ToastVariant } from "./use-toast";

const variantStyles: Record<ToastVariant, string> = {
  default: "bg-[var(--bg-card)] border-[var(--border)] text-[var(--text-primary)]",
  success: "bg-[var(--success-subtle)] border-[#1a6040] text-[var(--success)]",
  destructive: "bg-[var(--danger-subtle)] border-[#6e1f28] text-[var(--danger)]",
  warning: "bg-[var(--warning-subtle)] border-[#6e4a1e] text-[var(--warning)]",
};

export function Toaster() {
  const { toasts } = useToastStore();

  return (
    <ToastPrimitive.Provider swipeDirection="right">
      {toasts.map((t) => (
        <ToastPrimitive.Root
          key={t.id}
          className={cn(
            "flex items-start gap-3 rounded-lg border p-4 shadow-lg w-80",
            variantStyles[t.variant ?? "default"]
          )}
        >
          <div className="flex-1">
            {t.title && (
              <ToastPrimitive.Title className="text-sm font-semibold">{t.title}</ToastPrimitive.Title>
            )}
            {t.description && (
              <ToastPrimitive.Description className="text-xs opacity-80 mt-0.5">
                {t.description}
              </ToastPrimitive.Description>
            )}
          </div>
          <ToastPrimitive.Close className="opacity-60 hover:opacity-100">
            <X className="h-4 w-4" />
          </ToastPrimitive.Close>
        </ToastPrimitive.Root>
      ))}
      <ToastPrimitive.Viewport className="fixed bottom-4 right-4 z-[100] flex flex-col gap-2 max-w-sm" />
    </ToastPrimitive.Provider>
  );
}
