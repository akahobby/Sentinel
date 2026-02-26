import { useCallback, useEffect, useMemo, useState } from "react";
import { listProcesses, listServices } from "../services/tauriApi";
import type { ProcessInfo } from "../types";
import { StatCard } from "../components/StatCard";

export function OverviewPage() {
  const [processes, setProcesses] = useState<ProcessInfo[]>([]);
  const [serviceCount, setServiceCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [processResponse, servicesResponse] = await Promise.all([listProcesses(), listServices()]);
      setProcesses(processResponse.processes);
      setServiceCount(servicesResponse.services.length);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load overview.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const topCpu = useMemo(
    () => [...processes].sort((a, b) => b.cpu - a.cpu).slice(0, 5),
    [processes],
  );
  const totalCpu = useMemo(() => processes.reduce((acc, item) => acc + item.cpu, 0), [processes]);
  const totalMemoryMb = useMemo(() => processes.reduce((acc, item) => acc + item.memoryMB, 0), [processes]);

  return (
    <section className="page">
      <div className="page-header">
        <h2>Overview</h2>
        <button type="button" onClick={refresh} disabled={loading}>
          {loading ? "Refreshing..." : "Refresh"}
        </button>
      </div>
      {error ? <div className="error-banner">{error}</div> : null}
      <div className="stat-grid">
        <StatCard label="Processes" value={`${processes.length}`} />
        <StatCard label="Services" value={`${serviceCount}`} />
        <StatCard label="Total CPU" value={`${totalCpu.toFixed(2)}%`} />
        <StatCard label="Total Memory" value={`${(totalMemoryMb / 1024).toFixed(2)} GB`} />
      </div>

      <div className="panel">
        <h3>Top CPU consumers</h3>
        <table className="data-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>PID</th>
              <th>CPU %</th>
              <th>Memory MB</th>
            </tr>
          </thead>
          <tbody>
            {topCpu.map((item) => (
              <tr key={item.pid}>
                <td>{item.name}</td>
                <td>{item.pid}</td>
                <td>{item.cpu.toFixed(2)}</td>
                <td>{item.memoryMB.toFixed(2)}</td>
              </tr>
            ))}
            {topCpu.length === 0 ? (
              <tr>
                <td colSpan={4}>No process data available.</td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>
    </section>
  );
}
