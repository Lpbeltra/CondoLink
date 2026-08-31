import { useEffect, useState, type PropsWithChildren } from "react";
import { AdministratorContext } from "./AdministratorContext";
import { getAdministratorContext } from "../managementCompanyRequests/api";
import type { AdministratorContext as Value } from "../managementCompanyRequests/types";
import { useAuth } from "../auth/AuthContext";
export function AdministratorProvider({ children }: PropsWithChildren) {
  const { user } = useAuth();
  const [value, setValue] = useState<Value | null>(null);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    if (!user?.hasAdministratorAccess) { setValue(null); setLoading(false); return; }
    setLoading(true);
    getAdministratorContext()
      .then(setValue)
      .catch(() => setValue(null))
      .finally(() => setLoading(false));
  }, [user]);
  return (
    <AdministratorContext.Provider value={{ value, loading }}>
      {children}
    </AdministratorContext.Provider>
  );
}
