import { Inbox } from "lucide-react";

interface EmptyStateProps {
  message?: string;
  icon?: React.ReactNode;
}

export default function EmptyState({ message = "Keine Einträge gefunden", icon }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-[var(--text-muted)] gap-3">
      {icon ?? <Inbox className="w-10 h-10 opacity-40" />}
      <p className="text-sm">{message}</p>
    </div>
  );
}
