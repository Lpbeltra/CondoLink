import { createContext, useContext } from "react";
import type { AdministratorContext as Value } from "../managementCompanyRequests/types";
export const AdministratorContext = createContext<{
  value: Value | null;
  loading: boolean;
}>({ value: null, loading: true });
export const useAdministrator = () => useContext(AdministratorContext);
