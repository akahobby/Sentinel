import { invoke } from "@tauri-apps/api/core";
import type {
  AnalyzeSystemResponse,
  ExportReportResponse,
  KillProcessResponse,
  ListProcessesResponse,
  ProcessDetailsResponse,
  ServiceActionResponse,
  ServicesResponse,
  SpikeEventsResponse,
  StartupItemsResponse,
  StartupToggleResponse,
} from "../types";

function normalizeError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }
  if (typeof error === "string") {
    return error;
  }
  return "Unknown backend error";
}

export async function listProcesses(): Promise<ListProcessesResponse> {
  try {
    return await invoke<ListProcessesResponse>("list_processes");
  } catch (error) {
    throw new Error(normalizeError(error));
  }
}

export async function getProcessDetails(pid: number): Promise<ProcessDetailsResponse> {
  try {
    return await invoke<ProcessDetailsResponse>("get_process_details", { pid });
  } catch (error) {
    throw new Error(normalizeError(error));
  }
}

export async function killProcess(pid: number): Promise<KillProcessResponse> {
  try {
    return await invoke<KillProcessResponse>("kill_process", { pid });
  } catch (error) {
    throw new Error(normalizeError(error));
  }
}

export async function listStartupItems(): Promise<StartupItemsResponse> {
  try {
    return await invoke<StartupItemsResponse>("list_startup_items");
  } catch (error) {
    throw new Error(normalizeError(error));
  }
}

export async function toggleStartupItem(id: string, enabled: boolean): Promise<StartupToggleResponse> {
  try {
    return await invoke<StartupToggleResponse>("toggle_startup_item", { id, enabled });
  } catch (error) {
    throw new Error(normalizeError(error));
  }
}

export async function listServices(): Promise<ServicesResponse> {
  try {
    return await invoke<ServicesResponse>("list_services");
  } catch (error) {
    throw new Error(normalizeError(error));
  }
}

export async function serviceAction(name: string, action: string): Promise<ServiceActionResponse> {
  try {
    return await invoke<ServiceActionResponse>("service_action", { name, action });
  } catch (error) {
    throw new Error(normalizeError(error));
  }
}

export async function analyzeSystem(): Promise<AnalyzeSystemResponse> {
  try {
    return await invoke<AnalyzeSystemResponse>("analyze_system");
  } catch (error) {
    throw new Error(normalizeError(error));
  }
}

export async function getSpikeEvents(daysBack = 7): Promise<SpikeEventsResponse> {
  try {
    return await invoke<SpikeEventsResponse>("get_spike_events", { daysBack });
  } catch (error) {
    throw new Error(normalizeError(error));
  }
}

export async function exportReport(): Promise<ExportReportResponse> {
  try {
    return await invoke<ExportReportResponse>("export_report");
  } catch (error) {
    throw new Error(normalizeError(error));
  }
}
