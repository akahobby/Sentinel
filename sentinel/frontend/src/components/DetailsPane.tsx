import type { ProcessInfo } from "../types";
import { SeverityBadge } from "./SeverityBadge";

interface DetailsPaneProps {
  process?: ProcessInfo | null;
  loading?: boolean;
  onKill?: (pid: number) => void;
}

function formatKbps(value: number): string {
  return value >= 1024 ? `${(value / 1024).toFixed(2)} Mbps` : `${value.toFixed(2)} Kbps`;
}

export function DetailsPane({ process, loading = false, onKill }: DetailsPaneProps) {
  if (!process) {
    return <div className="panel details-pane">Select a process to view details.</div>;
  }

  return (
    <div className="panel details-pane">
      <div className="details-header">
        <h3>{process.name}</h3>
        <SeverityBadge value={process.risk} />
      </div>
      <div className="details-grid">
        <div>PID</div>
        <div>{process.pid}</div>
        <div>Path</div>
        <div className="break-word">{process.path || "Unknown"}</div>
        <div>Publisher</div>
        <div>{process.publisher || "Unknown"}</div>
        <div>Signed</div>
        <div>{process.signed ? "Yes" : "No"}</div>
        <div>CPU</div>
        <div>{process.cpu.toFixed(2)}%</div>
        <div>Memory</div>
        <div>{process.memoryMB.toFixed(2)} MB</div>
        <div>GPU</div>
        <div>{process.gpuPercent.toFixed(2)}%</div>
        <div>Network</div>
        <div>{formatKbps(process.networkKbps)}</div>
        <div>Command line</div>
        <div className="break-word">{process.commandLine || "-"}</div>
      </div>
      {onKill ? (
        <button className="danger-button" onClick={() => onKill(process.pid)} disabled={loading} type="button">
          {loading ? "Stopping..." : "End process"}
        </button>
      ) : null}
    </div>
  );
}
