import Keycloak from "keycloak-js";
import {
  createContext,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";

const ACCESS_ROLES = new Set(["pcw_admin", "pcw_console", "pcw_support", "owner", "admin"]);
const ADMIN_ROLES = new Set(["pcw_admin", "owner", "admin"]);
const SUPPORT_ROLES = new Set(["pcw_support"]);

interface AuthContextValue {
  ready: boolean;
  authenticated: boolean;
  userName: string;
  userEmail: string;
  roles: string[];
  hasAccess: () => boolean;
  isAdmin: () => boolean;
  isSupport: () => boolean;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue>({
  ready: false,
  authenticated: false,
  userName: "",
  userEmail: "",
  roles: [],
  hasAccess: () => false,
  isAdmin: () => false,
  isSupport: () => false,
  logout: () => {},
});

const keycloakInstance = new Keycloak({
  url: import.meta.env.VITE_KEYCLOAK_URL ?? "https://login.xn--pcwchter-2za.de",
  realm: import.meta.env.VITE_KEYCLOAK_REALM ?? "pcwaechter-prod",
  clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID ?? "console-web",
});

let _kc = keycloakInstance;

export async function getKeycloakToken(): Promise<string> {
  if (_kc.isTokenExpired(30)) {
    await _kc.updateToken(30);
  }
  return _kc.token ?? "";
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [authenticated, setAuthenticated] = useState(false);
  const [userName, setUserName] = useState("");
  const [userEmail, setUserEmail] = useState("");
  const [roles, setRoles] = useState<string[]>([]);
  const initRef = useRef(false);

  useEffect(() => {
    if (initRef.current) return;
    initRef.current = true;

    _kc = keycloakInstance;

    _kc
      .init({
        onLoad: "login-required",
        pkceMethod: "S256",
        checkLoginIframe: false,
      })
      .then((auth) => {
        setAuthenticated(auth);

        if (auth && _kc.tokenParsed) {
          const p = _kc.tokenParsed as Record<string, unknown>;
          setUserName(String(p["name"] ?? p["preferred_username"] ?? ""));
          setUserEmail(String(p["email"] ?? ""));

          const realmRoles = (
            (p["realm_access"] as { roles?: string[] } | undefined)?.roles ?? []
          );
          const resourceRoles: string[] = [];
          const resourceAccess = p["resource_access"] as
            | Record<string, { roles?: string[] }>
            | undefined;
          if (resourceAccess) {
            for (const v of Object.values(resourceAccess)) {
              resourceRoles.push(...(v.roles ?? []));
            }
          }
          setRoles([...new Set([...realmRoles, ...resourceRoles])]);
        }
        setReady(true);
      })
      .catch(() => {
        setReady(true);
      });

    _kc.onTokenExpired = () => {
      _kc.updateToken(60).catch(() => _kc.logout());
    };
  }, []);

  function hasAccess() {
    return roles.some((r) => ACCESS_ROLES.has(r));
  }

  function isAdmin() {
    return roles.some((r) => ADMIN_ROLES.has(r));
  }

  function isSupport() {
    return roles.some((r) => SUPPORT_ROLES.has(r));
  }

  return (
    <AuthContext.Provider
      value={{
        ready,
        authenticated,
        userName,
        userEmail,
        roles,
        hasAccess,
        isAdmin,
        isSupport,
        logout: () => _kc.logout(),
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
