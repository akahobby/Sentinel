import { useCallback, useEffect, useMemo, useState } from "react";
import { getSpikeEvents } from "../services/tauriApi";
import type { ChangeEvent, SpikeEvent } from "../types";

type TimelineItem =
  | { kind: "spike"; when: string; title: string; details: string; key: string }
  | { kind: "change"; when: string; title: string; details: string; key: string };

export function HistoryPage() {
  const [daysBack, setDaysBack] = useState(7);
  const [spikes, setSpikes] = useState<SpikeEvent[]>([]);
  const [changes, setChanges] = useState<ChangeEvent[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await getSpikeEvents(daysBack);
      setSpikes(response.spikeEvents);
      setChanges(response.changeEvents);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load history.");
    } finally {
      setLoading(false);
    }
  }, [daysBack]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const timeline = useMemo<TimelineItem[]>(() => {
    const spikeItems = spikes.map((spike) => ({
      kind: "spike" as const,
      when: spike.startUtc,
      title: `Spike: ${spike.metric}`,
      details: `${spike.processName ?? "System"} peak ${spike.peakValue.toFixed(2)} (${spike.durationSeconds.toFixed(
        1,
      )}s)`,
      key: `spike-${spike.id ?? spike.startUtc}`,
    }));
    const changeItems = changes.map((change) => ({
      kind: "change" as const,
      when: change.detectedUtc,
      title: `${change.category}: ${change.changeType}`,
      details: change.name || change.details || "-",
      key: `change-${change.id ?? change.detectedUtc}`,
    }));
    return [...spikeItems, ...changeItems].sort((a, b) => b.when.localeCompare(a.when));
  }, [spikes, changes]);

  return (
    <section className="page">
      <div className="page-header">
        <h2>History</h2>
        <div className="page-actions">
          <select value={daysBack} onChange={(event) => setDaysBack(Number(event.target.value))}>
            <option value={1}>1 day</option>
            <option value={7}>7 days</option>
            <option value={14}>14 days</option>
            <option value={30}>30 days</option>
          </select>
          <button type="button" onClick={refresh} disabled={loading}>
            {loading ? "Loading..." : "Load"}
          </button>
        </div>
      </div>
      {error ? <div className="error-banner">{error}</div> : null}
      <div className="panel">
        <div className="stack">
          {timeline.map((entry) => (
            <div key={entry.key} className="timeline-item">
              <div className="timeline-title">{entry.title}</div>
              <div>{entry.details}</div>
              <div className="muted">{new Date(entry.when).toLocaleString()}</div>
            </div>
          ))}
          {!loading && timeline.length === 0 ? <div>No events in selected range.</div> : null}
        </div>
      </div>
    </section>
  );
}
