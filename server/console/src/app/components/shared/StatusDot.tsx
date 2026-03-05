interface StatusDotProps {
  online: boolean;
}

export default function StatusDot({ online }: StatusDotProps) {
  return (
    <span
      className={`status-dot ${online ? "status-dot-online" : "status-dot-offline"}`}
      title={online ? "Online" : "Offline"}
    />
  );
}
