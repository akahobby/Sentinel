import { useCallback, useEffect, useState } from "react";
import { SeverityBadge } from "../components/SeverityBadge";
import { listStartupItems, toggleStartupItem } from "../services/tauriApi";
import type { StartupItem } from "../types";

export function StartupPage() {
  const [items, setItems] = useState<StartupItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await listStartupItems();
      setItems(response.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load startup items.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const onToggle = useCallback(
    async (item: StartupItem) => {
      setLoading(true);
      setError(null);
      try {
        await toggleStartupItem(item.id, !item.isEnabled);
        await refresh();
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to toggle startup item.");
      } finally {
        setLoading(false);
      }
    },
    [refresh],
  );

  return (
    <section className="page">
      <div className="page-header">
        <h2>Startup</h2>
        <button type="button" onClick={refresh} disabled={loading}>
          {loading ? "Refreshing..." : "Refresh"}
        </button>
      </div>
      {error ? <div className="error-banner">{error}</div> : null}
      <div className="panel">
        <table className="data-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Location</th>
              <th>Status</th>
              <th>Risk</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td>
                  <div>{item.name}</div>
                  <small className="muted">{item.command}</small>
                </td>
                <td>{item.location}</td>
                <td>{item.isEnabled ? "Enabled" : "Disabled"}</td>
                <td>
                  <SeverityBadge value={item.risk} />
                </td>
                <td>
                  <button type="button" onClick={() => onToggle(item)} disabled={loading}>
                    {item.isEnabled ? "Disable" : "Enable"}
                  </button>
                </td>
              </tr>
            ))}
            {!loading && items.length === 0 ? (
              <tr>
                <td colSpan={5}>No startup items detected.</td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>
    </section>
  );
}
