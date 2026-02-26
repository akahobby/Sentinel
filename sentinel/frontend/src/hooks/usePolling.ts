import { useEffect, useRef } from "react";

export function usePolling(action: () => void | Promise<void>, intervalMs: number, enabled = true): void {
  const actionRef = useRef(action);

  useEffect(() => {
    actionRef.current = action;
  }, [action]);

  useEffect(() => {
    if (!enabled || intervalMs <= 0) {
      return;
    }

    const id = window.setInterval(() => {
      void actionRef.current();
    }, intervalMs);

    return () => window.clearInterval(id);
  }, [enabled, intervalMs]);
}
