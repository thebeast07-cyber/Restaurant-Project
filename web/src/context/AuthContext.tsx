import { createContext, useContext, useState, type ReactNode } from "react";
import { login as loginRequest, type LoginResponse } from "../api/auth";

interface AuthUser {
  name: string;
  role: string;
  tenantId: string;
  branchId: string;
}

interface AuthContextValue {
  user: AuthUser | null;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function toUser(response: LoginResponse): AuthUser {
  return {
    name: response.name,
    role: response.role,
    tenantId: response.tenantId,
    branchId: response.branchId,
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const stored = localStorage.getItem("user");
    return stored ? (JSON.parse(stored) as AuthUser) : null;
  });

  async function login(username: string, password: string) {
    const response = await loginRequest(username, password);
    localStorage.setItem("token", response.token);
    localStorage.setItem("user", JSON.stringify(toUser(response)));
    setUser(toUser(response));
  }

  function logout() {
    localStorage.removeItem("token");
    localStorage.removeItem("user");
    setUser(null);
  }

  return <AuthContext.Provider value={{ user, login, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
