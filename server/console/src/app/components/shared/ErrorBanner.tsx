import { AlertTriangle, ShieldX } from "lucide-react";

interface ErrorBannerProps {
  message: string;
  status?: number;
}

export default function ErrorBanner({ message, status }: ErrorBannerProps) {
  const is403 = status === 403;
  return (
    <div
      className="flex items-start gap-3 rounded-xl px-4 py-3 text-sm"
      style={{
        background: "rgba(240,64,112,0.08)",
        border: "1px solid rgba(240,64,112,0.2)",
        color: "var(--danger)",
      }}
    >
      {is403 ? <ShieldX className="w-4 h-4 mt-0.5 shrink-0" /> : <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />}
      <div>
        {is403 && <p className="font-semibold mb-0.5 text-[var(--text-primary)]">Zugriff verweigert</p>}
        <p className="text-[var(--danger)] opacity-90">{message}</p>
      </div>
    </div>
  );
}
