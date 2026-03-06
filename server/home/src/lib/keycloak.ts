// --- Preview-/Design-Modus Erkennung ---
// Laeuft BEVOR irgendetwas von keycloak-js geladen wird.
function isPreviewEnvironment(): boolean {
  if (typeof window === 'undefined') return true;

  // Explizites Opt-in / Opt-out ueber Environment-Variable
  if (import.meta.env.VITE_PREVIEW_MODE === 'true') return true;
  if (import.meta.env.VITE_PREVIEW_MODE === 'false') return false;

  const hostname = window.location.hostname;

  // NUR diese Produktions-Domains verwenden echte Keycloak-Auth.
  // Alles andere (Figma Make, localhost, WebContainer, Sandbox etc.) = Preview.
  const productionHosts = [
    'home.xn--pcwchter-2za.de',
    'home.pcwächter.de',
  ];

  return !productionHosts.some((h) => hostname === h);
}

export const IS_PREVIEW = isPreviewEnvironment();

// Keycloak wird NICHT auf Top-Level importiert.
// In Preview bleibt keycloak = null, keycloak-js wird nie geladen.
// In Produktion wird es dynamisch initialisiert.
let keycloak: any = null;

// Wird von AuthContext aufgerufen, nur in Produktion
export async function createKeycloakInstance(): Promise<any> {
  if (IS_PREVIEW || keycloak) return keycloak;

  try {
    const { default: Keycloak } = await import('keycloak-js');
    const keycloakConfig = {
      url: import.meta.env.VITE_KEYCLOAK_URL || 'https://login.xn--pcwchter-2za.de',
      realm: import.meta.env.VITE_KEYCLOAK_REALM || 'pcwaechter-prod',
      clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID || 'home-web',
    };
    keycloak = new Keycloak(keycloakConfig);
  } catch (e) {
    console.error('Keycloak-Instanz konnte nicht erstellt werden:', e);
  }

  return keycloak;
}

export function getKeycloak(): any {
  return keycloak;
}

export default keycloak;
