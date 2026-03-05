interface PageHeaderProps {
  title: string;
  subtitle?: string;
  action?: React.ReactNode;
}

export default function PageHeader({ title, subtitle, action }: PageHeaderProps) {
  return (
    <div className="flex items-start justify-between mb-6">
      <div>
        <h1 className="text-[1.35rem] font-bold text-[var(--text-primary)] tracking-tight leading-tight">{title}</h1>
        {subtitle && (
          <p className="text-sm text-[var(--text-muted)] mt-1 leading-snug">{subtitle}</p>
        )}
      </div>
      {action && <div className="flex items-center gap-2 shrink-0 ml-4">{action}</div>}
    </div>
  );
}
