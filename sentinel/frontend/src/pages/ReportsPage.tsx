import { useState } from "react";
import { analyzeSystem, exportReport } from "../services/tauriApi";

export function ReportsPage() {
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [latestReportPath, setLatestReportPath] = useState<string | null>(null);
  const [latestExportPath, setLatestExportPath] = useState<string | null>(null);

  const generateLatest = async () => {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const response = await analyzeSystem();
      setLatestReportPath(response.reportPath);
      setMessage("latest.json generated successfully.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to generate latest report.");
    } finally {
      setBusy(false);
    }
  };

  const exportZip = async () => {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const response = await exportReport();
      setLatestExportPath(response.exportPath);
      setMessage("Report ZIP exported.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to export report zip.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="page">
      <div className="page-header">
        <h2>Reports</h2>
      </div>
      {error ? <div className="error-banner">{error}</div> : null}
      {message ? <div className="success-banner">{message}</div> : null}
      <div className="panel">
        <p>
          Generate and export system reports. Reports are stored in
          <code>%LOCALAPPDATA%/Sentinel/reports/</code> and exports in
          <code>%LOCALAPPDATA%/Sentinel/exports/</code>.
        </p>
        <div className="button-row">
          <button type="button" onClick={generateLatest} disabled={busy}>
            Generate latest.json
          </button>
          <button type="button" onClick={exportZip} disabled={busy}>
            Export ZIP
          </button>
        </div>
        {latestReportPath ? (
          <p className="break-word">
            Latest report: <strong>{latestReportPath}</strong>
          </p>
        ) : null}
        {latestExportPath ? (
          <p className="break-word">
            Latest export: <strong>{latestExportPath}</strong>
          </p>
        ) : null}
      </div>
    </section>
  );
}
