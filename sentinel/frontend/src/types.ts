export interface ProcessInfo {
  pid: number;
  name: string;
  cpu: number;
  memoryMB: number;
  path?: string | null;
  signed: boolean;
  publisher?: string | null;
  risk: string;
  commandLine?: string | null;
  parentPid?: number | null;
  networkKbps: number;
  gpuPercent: number;
  diskKbps: number;
}

export interface ListProcessesResponse {
  processes: ProcessInfo[];
}

export interface ProcessDetailsResponse {
  process?: ProcessInfo | null;
}

export interface KillProcessResponse {
  success: boolean;
  pid: number;
  message: string;
}

export interface StartupItem {
  id: string;
  name: string;
  command: string;
  location: string;
  isEnabled: boolean;
  path?: string | null;
  signed: boolean;
  publisher?: string | null;
  risk: string;
}

export interface StartupItemsResponse {
  items: StartupItem[];
}

export interface StartupToggleResponse {
  success: boolean;
  id: string;
  enabled: boolean;
  message: string;
}

export interface ServiceInfo {
  name: string;
  displayName: string;
  status: string;
  startType: string;
  description?: string | null;
  binaryPath?: string | null;
  signed: boolean;
  publisher?: string | null;
  risk: string;
  requiresAdmin: boolean;
}

export interface ServicesResponse {
  services: ServiceInfo[];
}

export interface ServiceActionResponse {
  success: boolean;
  name: string;
  action: string;
  message: string;
}

export interface AnalyzerFinding {
  title: string;
  category: string;
  severity: string;
  explanation: string;
  evidence: string;
  recommendedActions: string[];
}

export interface SpikeEvent {
  id?: number | null;
  startUtc: string;
  endUtc: string;
  pid?: number | null;
  processName?: string | null;
  metric: string;
  peakValue: number;
  durationSeconds: number;
  context?: string | null;
  possibleLeak: boolean;
}

export interface ChangeEvent {
  id?: number | null;
  detectedUtc: string;
  category: string;
  changeType: string;
  name?: string | null;
  path?: string | null;
  details?: string | null;
  isApproved: boolean;
  isIgnored: boolean;
}

export interface SystemSnapshot {
  machineName: string;
  osVersion: string;
  totalPhysicalMemoryMb: number;
  availableMemoryMb: number;
  processorCount: number;
}

export interface TopOffenders {
  cpu: ProcessInfo[];
  memory: ProcessInfo[];
  disk: ProcessInfo[];
  network: ProcessInfo[];
}

export interface AnalyzeSystemResponse {
  generatedUtc: string;
  appVersion: string;
  systemSnapshot: SystemSnapshot;
  topOffenders: TopOffenders;
  findings: AnalyzerFinding[];
  recentSpikes: SpikeEvent[];
  reportPath: string;
}

export interface SpikeEventsResponse {
  spikeEvents: SpikeEvent[];
  changeEvents: ChangeEvent[];
}

export interface ExportReportResponse {
  exportPath: string;
}
