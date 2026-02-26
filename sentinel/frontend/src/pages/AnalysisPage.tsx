import { useCallback, useEffect, useState } from "react";
import { SeverityBadge } from "../components/SeverityBadge";
import { StatCard } from "../components/StatCard";
import { analyzeSystem } from "../services/tauriApi";
import type { AnalyzeSystemResponse } from "../types";

export function AnalysisPage() {
  const [result, setResult] = useState<AnalyzeSystemResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const runAnalysis = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await analyzeSystem();
      setResult(response);
    } catch (err) {
      setError(err instanceof Error ? err.message : "System analysis failed.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void runAnalysis();
  }, [runAnalysis]);

  return (
    <section className="page">
      <div className="page-header">
        <h2>Analysis</h2>
        <button type="button" onClick={runAnalysis} disabled={loading}>
          {loading ? "Analyzing..." : "Run analysis"}
        </button>
      </div>

      {error ? <div className="error-banner">{error}</div> : null}

      {result ? (
        <>
          <div className="stat-grid">
            <StatCard label="Machine" value={result.systemSnapshot.machineName} />
            <StatCard label="OS" value={result.systemSnapshot.osVersion || "Unknown"} />
            <StatCard
              label="Memory (total)"
              value={`${(result.systemSnapshot.totalPhysicalMemoryMb / 1024).toFixed(2)} GB`}
            />
            <StatCard label="CPUs" value={`${result.systemSnapshot.processorCount}`} />
          </div>
          <div className="panel">
            <h3>Findings</h3>
            <div className="stack">
              {result.findings.map((finding, index) => (
                <div key={`${finding.title}-${index}`} className="finding-card">
                  <div className="finding-header">
                    <h4>{finding.title}</h4>
                    <SeverityBadge value={finding.severity} />
                  </div>
                  <p>{finding.explanation}</p>
                  <p className="muted">{finding.evidence}</p>
                  {finding.recommendedActions.length > 0 ? (
                    <ul>
                      {finding.recommendedActions.map((action) => (
                        <li key={action}>{action}</li>
                      ))}
                    </ul>
                  ) : null}
                </div>
              ))}
            </div>
          </div>
        </>
      ) : null}
    </section>
  );
}
