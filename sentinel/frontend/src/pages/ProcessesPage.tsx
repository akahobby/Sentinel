import { useCallback, useEffect, useState } from "react";
import { DetailsPane } from "../components/DetailsPane";
import { ProcessTable } from "../components/ProcessTable";
import { usePolling } from "../hooks/usePolling";
import { getProcessDetails, killProcess, listProcesses } from "../services/tauriApi";
import type { ProcessInfo } from "../types";

export function ProcessesPage() {
  const [processes, setProcesses] = useState<ProcessInfo[]>([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(false);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [selected, setSelected] = useState<ProcessInfo | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await listProcesses();
      setProcesses(response.processes);
      if (selected) {
        const current = response.processes.find((item) => item.pid === selected.pid);
        if (!current) {
          setSelected(null);
        }
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load processes.");
    } finally {
      setLoading(false);
    }
  }, [selected]);

  const selectProcess = useCallback(async (process: ProcessInfo) => {
    setSelected(process);
    setDetailsLoading(true);
    try {
      const response = await getProcessDetails(process.pid);
      if (response.process) {
        setSelected(response.process);
      }
    } catch {
      // Keep previously selected summary row if details call fails.
    } finally {
      setDetailsLoading(false);
    }
  }, []);

  const terminate = useCallback(
    async (pid: number) => {
      setDetailsLoading(true);
      try {
        await killProcess(pid);
        await refresh();
        setSelected(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to end process.");
      } finally {
        setDetailsLoading(false);
      }
    },
    [refresh],
  );

  useEffect(() => {
    void refresh();
  }, [refresh]);

  usePolling(() => refresh(), 3000, true);

  return (
    <section className="page">
      <div className="page-header">
        <h2>Processes</h2>
      </div>
      {error ? <div className="error-banner">{error}</div> : null}
      <div className="split-layout">
        <ProcessTable
          processes={processes}
          loading={loading}
          search={search}
          selectedPid={selected?.pid}
          onSearchChange={setSearch}
          onSelect={selectProcess}
          onRefresh={refresh}
        />
        <DetailsPane process={selected} loading={detailsLoading} onKill={terminate} />
      </div>
    </section>
  );
}
