import { useCallback, useEffect, useMemo, useState } from "react";
import { SeverityBadge } from "../components/SeverityBadge";
import { listServices, serviceAction } from "../services/tauriApi";
import type { ServiceInfo } from "../types";

export function ServicesPage() {
  const [services, setServices] = useState<ServiceInfo[]>([]);
  const [selectedName, setSelectedName] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await listServices();
      setServices(response.services);
      if (!selectedName && response.services.length > 0) {
        setSelectedName(response.services[0].name);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load services.");
    } finally {
      setLoading(false);
    }
  }, [selectedName]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const selected = useMemo(
    () => services.find((service) => service.name === selectedName) ?? null,
    [services, selectedName],
  );

  const runAction = useCallback(
    async (action: string) => {
      if (!selected) return;
      setBusy(true);
      setError(null);
      setMessage(null);
      try {
        const response = await serviceAction(selected.name, action);
        setMessage(response.message);
        await refresh();
      } catch (err) {
        setError(err instanceof Error ? err.message : "Service action failed.");
      } finally {
        setBusy(false);
      }
    },
    [selected, refresh],
  );

  return (
    <section className="page">
      <div className="page-header">
        <h2>Services</h2>
        <button type="button" onClick={refresh} disabled={loading || busy}>
          {loading ? "Refreshing..." : "Refresh"}
        </button>
      </div>
      {error ? <div className="error-banner">{error}</div> : null}
      {message ? <div className="success-banner">{message}</div> : null}
      <div className="split-layout">
        <div className="panel">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Status</th>
                <th>Start Type</th>
                <th>Risk</th>
              </tr>
            </thead>
            <tbody>
              {services.map((service) => (
                <tr
                  key={service.name}
                  className={selected?.name === service.name ? "selected-row" : undefined}
                  onClick={() => setSelectedName(service.name)}
                >
                  <td>
                    <div>{service.displayName}</div>
                    <small className="muted">{service.name}</small>
                  </td>
                  <td>{service.status}</td>
                  <td>{service.startType}</td>
                  <td>
                    <SeverityBadge value={service.risk} />
                  </td>
                </tr>
              ))}
              {!loading && services.length === 0 ? (
                <tr>
                  <td colSpan={4}>No services found.</td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
        <div className="panel details-pane">
          {selected ? (
            <>
              <h3>{selected.displayName}</h3>
              <p className="muted">{selected.name}</p>
              <p>Status: {selected.status}</p>
              <p>Startup: {selected.startType}</p>
              <p className="break-word">Binary: {selected.binaryPath || "-"}</p>
              <div className="button-row">
                <button type="button" onClick={() => runAction("start")} disabled={busy}>
                  Start
                </button>
                <button type="button" onClick={() => runAction("stop")} disabled={busy}>
                  Stop
                </button>
                <button type="button" onClick={() => runAction("restart")} disabled={busy}>
                  Restart
                </button>
              </div>
              <div className="button-row">
                <button type="button" onClick={() => runAction("automatic")} disabled={busy}>
                  Automatic
                </button>
                <button type="button" onClick={() => runAction("manual")} disabled={busy}>
                  Manual
                </button>
                <button type="button" onClick={() => runAction("disabled")} disabled={busy}>
                  Disabled
                </button>
              </div>
            </>
          ) : (
            <p>Select a service for details.</p>
          )}
        </div>
      </div>
    </section>
  );
}
