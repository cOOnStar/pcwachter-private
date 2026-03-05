import { AlertTriangle } from "lucide-react";

interface ErrorBannerProps {
  message: string;
  status?: number;
}

export default function ErrorBanner({ message, status }: ErrorBannerProps) {
  const is403 = status === 403;
  return (
    <div className="flex items-start gap-3 rounded-lg border bg-[var(--danger-subtle)] border-[#6e1f28] text-[var(--danger)] px-4 py-3 text-sm">
      <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
      <div>
        {is403 && <p className="font-semibold mb-0.5">Zugriff verweigert</p>}
        <p>{message}</p>
      </div>
    </div>
  );
}
