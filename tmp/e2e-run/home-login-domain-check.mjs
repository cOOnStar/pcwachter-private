import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { chromium } from 'playwright';

const OUTPUT_DIR = join(process.cwd(), 'tmp', 'e2e-home-login-domain');
mkdirSync(OUTPUT_DIR, { recursive: true });

const config = {
  keycloakBaseUrl: process.env.E2E_KC_BASE_URL ?? 'http://localhost:18083',
  realm: process.env.E2E_KC_REALM ?? 'pcwaechter-prod',
  adminUser: process.env.KC_ADMIN_USER ?? 'admin',
  adminPassword: process.env.KC_ADMIN_PASSWORD ?? '',
  testUser: {
    username: process.env.E2E_HOME_USER ?? 'e2e.home.login@pcwaechter.local',
    email: process.env.E2E_HOME_USER ?? 'e2e.home.login@pcwaechter.local',
    firstName: 'E2E',
    lastName: 'Home Login',
    password: process.env.E2E_HOME_PASSWORD ?? 'E2E-Home-Login!2026',
    realmRole: 'pcw_user',
  },
  scenarios: [
    {
      name: 'home-direct',
      startUrl: 'https://home.xn--pcwchter-2za.de',
      expectedClientId: 'home-web',
    },
    {
      name: 'login-domain-direct',
      startUrl: 'https://login.xn--pcwchter-2za.de',
      expectedClientId: 'home-web',
    },
  ],
};

async function requestJson(url, init) {
  const response = await fetch(url, init);
  if (!response.ok) {
    const body = await response.text().catch(() => '');
    throw new Error(`${init?.method ?? 'GET'} ${url} failed with ${response.status}: ${body}`);
  }
  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

async function getAdminToken() {
  const body = new URLSearchParams({
    client_id: 'admin-cli',
    grant_type: 'password',
    username: config.adminUser,
    password: config.adminPassword,
  });
  const response = await fetch(
    `${config.keycloakBaseUrl}/realms/master/protocol/openid-connect/token`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/x-www-form-urlencoded',
      },
      body,
    },
  );
  if (!response.ok) {
    throw new Error(`Keycloak admin token failed with ${response.status}: ${await response.text()}`);
  }
  const payload = await response.json();
  return payload.access_token;
}

async function kcRequest(token, method, path, body) {
  const response = await fetch(`${config.keycloakBaseUrl}${path}`, {
    method,
    headers: {
      authorization: `Bearer ${token}`,
      ...(body ? { 'content-type': 'application/json' } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) {
    const text = await response.text().catch(() => '');
    throw new Error(`${method} ${path} failed with ${response.status}: ${text}`);
  }
  if (response.status === 204) {
    return null;
  }
  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

async function ensureUser(token) {
  const realmPath = `/admin/realms/${config.realm}`;
  const existingUsers = await kcRequest(
    token,
    'GET',
    `${realmPath}/users?exact=true&username=${encodeURIComponent(config.testUser.username)}`,
  );
  let user = existingUsers.find((entry) => entry.username === config.testUser.username);

  if (!user) {
    await kcRequest(token, 'POST', `${realmPath}/users`, {
      username: config.testUser.username,
      email: config.testUser.email,
      firstName: config.testUser.firstName,
      lastName: config.testUser.lastName,
      enabled: true,
      emailVerified: true,
    });
    const createdUsers = await kcRequest(
      token,
      'GET',
      `${realmPath}/users?exact=true&username=${encodeURIComponent(config.testUser.username)}`,
    );
    user = createdUsers.find((entry) => entry.username === config.testUser.username);
  }

  if (!user?.id) {
    throw new Error('Unable to resolve E2E test user after creation.');
  }

  await kcRequest(token, 'PUT', `${realmPath}/users/${user.id}`, {
    ...user,
    username: config.testUser.username,
    email: config.testUser.email,
    firstName: config.testUser.firstName,
    lastName: config.testUser.lastName,
    enabled: true,
    emailVerified: true,
  });

  await kcRequest(token, 'PUT', `${realmPath}/users/${user.id}/reset-password`, {
    type: 'password',
    value: config.testUser.password,
    temporary: false,
  });

  const role = await kcRequest(
    token,
    'GET',
    `${realmPath}/roles/${encodeURIComponent(config.testUser.realmRole)}`,
  );
  const roleMappings = await kcRequest(
    token,
    'GET',
    `${realmPath}/users/${user.id}/role-mappings/realm`,
  );
  if (!roleMappings.some((entry) => entry.name === config.testUser.realmRole)) {
    await kcRequest(token, 'POST', `${realmPath}/users/${user.id}/role-mappings/realm`, [
      {
        id: role.id,
        name: role.name,
      },
    ]);
  }

  return user.id;
}

async function deleteUser(token, userId) {
  await kcRequest(token, 'DELETE', `/admin/realms/${config.realm}/users/${userId}`);
}

function visibleSnippet(text) {
  return text.replace(/\s+/g, ' ').trim().slice(0, 1200);
}

async function runScenario({ name, startUrl, expectedClientId }) {
  const context = await chromium.launchPersistentContext(join(OUTPUT_DIR, `${name}-profile`), {
    channel: 'msedge',
    headless: true,
    ignoreHTTPSErrors: true,
    viewport: { width: 1440, height: 1100 },
  });
  const page = context.pages()[0] ?? (await context.newPage());

  const consoleErrors = [];
  const pageErrors = [];
  const requestFailures = [];
  const apiCalls = [];
  let bootstrapJson = null;
  let releaseJson = null;

  page.on('console', (message) => {
    if (message.type() === 'error') {
      consoleErrors.push(message.text());
    }
  });
  page.on('pageerror', (error) => {
    pageErrors.push(String(error));
  });
  page.on('requestfailed', (request) => {
    requestFailures.push({
      url: request.url(),
      method: request.method(),
      failure: request.failure()?.errorText ?? 'unknown',
    });
  });
  page.on('response', async (response) => {
    const url = response.url();
    if (url.includes('/api/v1/home/bootstrap') || url.includes('/api/v1/home/downloads/latest-release')) {
      apiCalls.push({ url, status: response.status() });
    }
    if (url.includes('/api/v1/home/bootstrap') && response.status() === 200 && !bootstrapJson) {
      bootstrapJson = await response.json().catch(() => null);
    }
    if (url.includes('/api/v1/home/downloads/latest-release') && response.status() === 200 && !releaseJson) {
      releaseJson = await response.json().catch(() => null);
    }
  });

  await page.goto(startUrl, { waitUntil: 'domcontentloaded', timeout: 45000 });
  await page.waitForSelector('input[name="username"], input#username', { timeout: 45000 });
  await page.waitForLoadState('networkidle', { timeout: 45000 });
  await page.waitForTimeout(750);
  const loginUrl = page.url();
  await page.screenshot({ path: join(OUTPUT_DIR, `${name}-01-login.png`), fullPage: true });

  await page.fill('input[name="username"], input#username', config.testUser.username);
  await page.fill('input[name="password"], input#password', config.testUser.password);

  try {
    await page.click('button[type="submit"], input[type="submit"]');
    await page.waitForFunction(
      () => {
        const bodyText = document.body?.innerText ?? '';
        return (
          window.location.href.includes('home.pcwächter.de') ||
          window.location.href.includes('home.xn--pcwchter-2za.de') ||
          bodyText.includes('Kundenportal') ||
          bodyText.includes('Dashboard')
        );
      },
      { timeout: 45000 },
    );
  } catch (error) {
    await page.screenshot({ path: join(OUTPUT_DIR, `${name}-99-error-after-submit.png`), fullPage: true });
    throw error;
  }

  const bootstrapResponse = await page.waitForResponse(
    (response) => response.url().includes('/api/v1/home/bootstrap') && response.status() === 200,
    { timeout: 45000 },
  );
  if (!bootstrapJson) {
    bootstrapJson = await bootstrapResponse.json().catch(() => null);
  }

  if (!releaseJson) {
    const releaseResponse = await page.waitForResponse(
      (response) => response.url().includes('/api/v1/home/downloads/latest-release') && response.status() === 200,
      { timeout: 45000 },
    );
    releaseJson = await releaseResponse.json().catch(() => null);
  }

  await page.waitForLoadState('networkidle', { timeout: 45000 });
  await page.screenshot({ path: join(OUTPUT_DIR, `${name}-02-after-login.png`), fullPage: true });

  const pageText = await page.locator('body').innerText();
  const result = {
    name,
    startUrl,
    loginUrl,
    finalUrl: page.url(),
    title: await page.title(),
    expectedClientId,
    usedHomeClient: loginUrl.includes(`client_id=${expectedClientId}`),
    consoleErrors,
    pageErrors,
    requestFailures,
    apiCalls,
    bootstrapStatus: apiCalls.find((entry) => entry.url.includes('/api/v1/home/bootstrap'))?.status ?? null,
    bootstrapJson,
    releaseStatus: apiCalls.find((entry) => entry.url.includes('/api/v1/home/downloads/latest-release'))?.status ?? null,
    releaseJson,
    visibleSnippet: visibleSnippet(pageText),
  };

  await context.close();
  return result;
}

const report = {
  startedAt: new Date().toISOString(),
  config: {
    realm: config.realm,
    user: config.testUser.username,
  },
  scenarios: [],
};

let token = null;
let userId = null;

try {
  token = await getAdminToken();
  userId = await ensureUser(token);

  for (const scenario of config.scenarios) {
    report.scenarios.push(await runScenario(scenario));
  }
} catch (error) {
  report.error = error instanceof Error ? error.message : String(error);
  throw error;
} finally {
  if (token && userId) {
    try {
      await deleteUser(token, userId);
      report.cleanup = 'deleted_test_user';
    } catch (cleanupError) {
      report.cleanup = `failed: ${cleanupError instanceof Error ? cleanupError.message : String(cleanupError)}`;
    }
  }
  report.finishedAt = new Date().toISOString();
  writeFileSync(join(OUTPUT_DIR, 'report.json'), `${JSON.stringify(report, null, 2)}\n`, 'utf8');
}
