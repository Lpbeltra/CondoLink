import { useEffect, useRef } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { getStoredToken } from "../auth/authStorage";

const baseUrl = (import.meta.env.VITE_API_URL || "/api").replace(/\/$/, "");
const hubUrl = `${baseUrl}/management-company-requests/realtime`;

export function useManagementCompanyRequestRealtime(options: {
  enabled: boolean;
  onMessage?: (payload: unknown) => void;
  onUpdated?: (payload: unknown) => void;
}) {
  const handlers = useRef(options);
  useEffect(() => {
    handlers.current = options;
  }, [options]);
  useEffect(() => {
    if (!options.enabled) return;
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => getStoredToken() ?? "" })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on("management-company-request-event", payload => {
      if (payload?.kind === "message") handlers.current.onMessage?.(payload);
      else if (payload?.kind === "updated") handlers.current.onUpdated?.(payload);
    });
    void connection.start().catch(() => {});
    return () => { void connection.stop(); };
  }, [options.enabled]);
}
