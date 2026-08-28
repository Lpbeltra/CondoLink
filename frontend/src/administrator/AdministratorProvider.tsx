import { useEffect, useState, type PropsWithChildren } from "react";
import { AdministratorContext } from "./AdministratorContext";
import { getAdministratorContext } from "../managementCompanyRequests/api";
import type { AdministratorContext as Value } from "../managementCompanyRequests/types";
export function AdministratorProvider({ children }: PropsWithChildren) {
  const [value, setValue] = useState<Value | null>(null);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    getAdministratorContext()
      .then(setValue)
      .catch(() => setValue(null))
      .finally(() => setLoading(false));
  }, []);
  return (
    <AdministratorContext.Provider value={{ value, loading }}>
      {children}
    </AdministratorContext.Provider>
  );
}
