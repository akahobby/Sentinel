import { useEffect, useState } from "react";

type ThemeName = "dark" | "light";

export function SettingsPage() {
  const [theme, setTheme] = useState<ThemeName>("dark");
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(3);
  const [retentionDays, setRetentionDays] = useState(30);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  return (
    <section className="page">
      <div className="page-header">
        <h2>Settings</h2>
      </div>
      <div className="panel">
        <div className="settings-row">
          <label htmlFor="theme-select">Theme</label>
          <select id="theme-select" value={theme} onChange={(event) => setTheme(event.target.value as ThemeName)}>
            <option value="dark">Dark</option>
            <option value="light">Light</option>
          </select>
        </div>
        <div className="settings-row">
          <label htmlFor="refresh-interval">Polling interval (seconds)</label>
          <input
            id="refresh-interval"
            type="number"
            min={1}
            max={60}
            value={autoRefreshSeconds}
            onChange={(event) => setAutoRefreshSeconds(Number(event.target.value))}
          />
        </div>
        <div className="settings-row">
          <label htmlFor="retention-days">History retention (days)</label>
          <input
            id="retention-days"
            type="number"
            min={1}
            max={365}
            value={retentionDays}
            onChange={(event) => setRetentionDays(Number(event.target.value))}
          />
        </div>
        <p className="muted">
          Logs path: <code>%LOCALAPPDATA%/Sentinel/logs/</code>
        </p>
        <p className="muted">
          Reports path: <code>%LOCALAPPDATA%/Sentinel/reports/</code>
        </p>
      </div>
    </section>
  );
}
