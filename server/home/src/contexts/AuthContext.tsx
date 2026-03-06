import { createContext, useContext, useEffect, useState, useRef, ReactNode } from 'react';
import { IS_PREVIEW, createKeycloakInstance, getKeycloak } from '../lib/keycloak';

interface UserProfile {
  id?: string;
  username?: string;
  email?: string;
  firstName?: string;
  lastName?: string;
  emailVerified?: boolean;
  attributes?: {
    license_tier?: string[];
    license_roles?: string[];
  };
}

interface AuthContextType {
  isAuthenticated: boolean;
  isLoading: boolean;
  authError: boolean;
  user: UserProfile | null;
  login: () => void;
  logout: () => void;
  register: () => void;
  updateProfile: () => Promise<void>;
}

// Demo-User fuer Preview-Umgebungen
const PREVIEW_USER: UserProfile = {
  id: 'preview-user-001',
  username: 'vorschau',
  email: 'vorschau@pcwaechter.de',
  firstName: 'Vorschau',
  lastName: 'Benutzer',
  emailVerified: true,
};

const AuthContext = createContext<AuthContextType | undefined>(undefined);

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [isAuthenticated, setIsAuthenticated] = useState(IS_PREVIEW);
  const [isLoading, setIsLoading] = useState(!IS_PREVIEW);
  const [authError, setAuthError] = useState(false);
  const [user, setUser] = useState<UserProfile | null>(IS_PREVIEW ? PREVIEW_USER : null);
  const initCalled = useRef(false);

  useEffect(() => {
    if (IS_PREVIEW) return;
    if (initCalled.current) return;
    initCalled.current = true;

    initKeycloak();
  }, []);

  const initKeycloak = async () => {
    try {
      const kc = await createKeycloakInstance();
      if (!kc) {
        setAuthError(true);
        setIsLoading(false);
        return;
      }

      const keycloakUrl = String(import.meta.env.VITE_KEYCLOAK_URL || '').replace(/\/+$/, '');
      let canUseSilentCheck = false;
      try {
        canUseSilentCheck = !!keycloakUrl && new URL(keycloakUrl).origin === window.location.origin;
      } catch {
        canUseSilentCheck = false;
      }

      const initOptions: Record<string, unknown> = {
        onLoad: 'check-sso',
        pkceMethod: 'S256',
        checkLoginIframe: false,
      };
      if (canUseSilentCheck) {
        initOptions.silentCheckSsoRedirectUri = window.location.origin + '/silent-check-sso.html';
      }

      const authenticated = await kc.init(initOptions);

      setIsAuthenticated(authenticated);

      if (authenticated) {
        await loadUserProfile(kc);
      }

      // Token refresh
      kc.onTokenExpired = () => {
        kc.updateToken(70)
          .then((refreshed: boolean) => {
            if (refreshed) {
              console.log('Token refreshed');
            }
          })
          .catch(() => {
            console.error('Token refresh failed');
            logout();
          });
      };
    } catch (error) {
      console.error('Keycloak initialization failed:', error);
      setIsAuthenticated(false);
      setAuthError(true);
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  };

  const loadUserProfile = async (kc?: any) => {
    const keycloak = kc || getKeycloak();
    if (!keycloak) return;

    try {
      const profile = await keycloak.loadUserProfile();

      setUser({
        id: profile.id,
        username: profile.username,
        email: profile.email,
        firstName: profile.firstName,
        lastName: profile.lastName,
        emailVerified: profile.emailVerified,
        attributes: profile.attributes as { license_tier?: string[]; license_roles?: string[] },
      });
    } catch (error) {
      console.error('Failed to load user profile:', error);
    }
  };

  const login = () => {
    if (IS_PREVIEW) return;
    const kc = getKeycloak();
    if (!kc) return;
    kc.login({
      redirectUri: window.location.origin + '/auth/callback',
    });
  };

  const logout = () => {
    if (IS_PREVIEW) return;
    const kc = getKeycloak();
    if (!kc) return;
    kc.logout({
      redirectUri: window.location.origin + '/',
    });
  };

  const register = () => {
    if (IS_PREVIEW) return;
    const kc = getKeycloak();
    if (!kc) return;
    kc.register({
      redirectUri: window.location.origin + '/auth/callback',
    });
  };

  const updateProfile = async () => {
    if (IS_PREVIEW) return;
    await loadUserProfile();
  };

  const value: AuthContextType = {
    isAuthenticated,
    isLoading,
    authError,
    user,
    login,
    logout,
    register,
    updateProfile,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
