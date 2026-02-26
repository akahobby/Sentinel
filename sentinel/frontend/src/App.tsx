import { useMemo, useState } from "react";
import { Sidebar } from "./components/Sidebar";
import { AnalysisPage } from "./pages/AnalysisPage";
import { HistoryPage } from "./pages/HistoryPage";
import { OverviewPage } from "./pages/OverviewPage";
import { ProcessesPage } from "./pages/ProcessesPage";
import { ReportsPage } from "./pages/ReportsPage";
import { ServicesPage } from "./pages/ServicesPage";
import { SettingsPage } from "./pages/SettingsPage";
import { StartupPage } from "./pages/StartupPage";

type PageKey =
  | "overview"
  | "processes"
  | "startup"
  | "analysis"
  | "history"
  | "services"
  | "reports"
  | "settings";

function renderPage(page: PageKey) {
  switch (page) {
    case "overview":
      return <OverviewPage />;
    case "processes":
      return <ProcessesPage />;
    case "startup":
      return <StartupPage />;
    case "analysis":
      return <AnalysisPage />;
    case "history":
      return <HistoryPage />;
    case "services":
      return <ServicesPage />;
    case "reports":
      return <ReportsPage />;
    case "settings":
      return <SettingsPage />;
    default:
      return null;
  }
}

export default function App() {
  const [activePage, setActivePage] = useState<PageKey>("overview");

  const sidebarItems = useMemo(
    () => [
      { key: "overview", label: "Overview" },
      { key: "processes", label: "Processes" },
      { key: "startup", label: "Startup" },
      { key: "analysis", label: "Analysis" },
      { key: "history", label: "History" },
      { key: "services", label: "Services" },
      { key: "reports", label: "Reports" },
      { key: "settings", label: "Settings" },
    ],
    [],
  );

  return (
    <div className="app-shell">
      <Sidebar items={sidebarItems} active={activePage} onChange={(next) => setActivePage(next as PageKey)} />
      <main className="content">{renderPage(activePage)}</main>
    </div>
  );
}
