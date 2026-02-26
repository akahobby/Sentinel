interface SeverityBadgeProps {
  value: string;
}

export function SeverityBadge({ value }: SeverityBadgeProps) {
  const normalized = value.toLowerCase();
  const className =
    normalized === "fail" || normalized === "high" || normalized === "suspicious"
      ? "severity-badge danger"
      : normalized === "warn" || normalized === "medium"
        ? "severity-badge warn"
        : normalized === "ok" || normalized === "low"
          ? "severity-badge ok"
          : "severity-badge";

  return <span className={className}>{value}</span>;
}
