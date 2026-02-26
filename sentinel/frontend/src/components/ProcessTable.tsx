import type { ProcessInfo } from "../types";
import { SearchBox } from "./SearchBox";
import { SeverityBadge } from "./SeverityBadge";

interface ProcessTableProps {
  processes: ProcessInfo[];
  loading: boolean;
  search: string;
  selectedPid?: number | null;
  onSearchChange: (value: string) => void;
  onSelect: (process: ProcessInfo) => void;
  onRefresh: () => void;
}

function formatMemory(memoryMb: number): string {
  return memoryMb >= 1024 ? `${(memoryMb / 1024).toFixed(2)} GB` : `${memoryMb.toFixed(2)} MB`;
}

export function ProcessTable({
  processes,
  loading,
  search,
  selectedPid,
  onSearchChange,
  onSelect,
  onRefresh,
}: ProcessTableProps) {
  const normalizedSearch = search.trim().toLowerCase();
  const filtered = normalizedSearch
    ? processes.filter((process) => process.name.toLowerCase().includes(normalizedSearch))
    : processes;

  return (
    <div className="panel">
      <div className="panel-toolbar">
        <SearchBox value={search} placeholder="Search process name..." onChange={onSearchChange} />
        <button type="button" onClick={onRefresh} disabled={loading}>
          {loading ? "Refreshing..." : "Refresh"}
        </button>
      </div>
      <div className="table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>PID</th>
              <th>CPU %</th>
              <th>Memory</th>
              <th>Risk</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((process) => (
              <tr
                key={process.pid}
                className={selectedPid === process.pid ? "selected-row" : undefined}
                onClick={() => onSelect(process)}
              >
                <td>{process.name}</td>
                <td>{process.pid}</td>
                <td>{process.cpu.toFixed(2)}</td>
                <td>{formatMemory(process.memoryMB)}</td>
                <td>
                  <SeverityBadge value={process.risk} />
                </td>
              </tr>
            ))}
            {!loading && filtered.length === 0 ? (
              <tr>
                <td colSpan={5}>No matching processes.</td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>
    </div>
  );
}
