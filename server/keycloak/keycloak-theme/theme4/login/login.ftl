<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Anmeldung</title>
  <link rel="stylesheet" href="${url.resourcesPath}/css/theme4.css" />
  <link rel="icon" href="${url.resourcesPath}/img/logo.png" />
</head>
<body>
  <main class="page">
    <section class="card">
      <img class="logo" src="${url.resourcesPath}/img/logo.png" alt="PCWächter" />
      <h1>Willkommen zurück</h1>
      <p class="subtitle">Melde dich sicher in dein PCWächter Konto an.</p>

      <#if message?has_content>
        <div class="alert ${message.type!"info"}">${message.summary!""}</div>
      </#if>

      <form id="kc-form-login" onsubmit="login.disabled = true; return true;" action="${url.loginAction}" method="post">
        <#if !(usernameHidden?? && usernameHidden)>
          <label for="username">Benutzername oder E-Mail</label>
          <input id="username" name="username" type="text" value="${(login.username)!""}" autofocus autocomplete="username" />
        </#if>

        <label for="password">Passwort</label>
        <input id="password" name="password" type="password" autocomplete="current-password" />

        <div class="row">
          <#if realm.rememberMe?? && realm.rememberMe>
            <label class="remember" for="rememberMe">
              <input id="rememberMe" name="rememberMe" type="checkbox" <#if login.rememberMe?? && login.rememberMe>checked</#if> />
              Angemeldet bleiben
            </label>
          </#if>

          <#if realm.resetPasswordAllowed?? && realm.resetPasswordAllowed>
            <a class="link" href="${url.loginResetCredentialsUrl}">Passwort vergessen?</a>
          </#if>
        </div>

        <input type="hidden" id="id-hidden-input" name="credentialId" <#if auth.selectedCredential??>value="${auth.selectedCredential}"</#if> />

        <button id="kc-login" name="login" type="submit">Anmelden</button>
      </form>

      <#if social?? && social.providers?? && social.providers?has_content>
        <div class="divider"><span>oder</span></div>
        <div class="social-list">
          <#list social.providers as p>
            <a id="social-${p.alias}" class="social-btn" href="${p.loginUrl}">
              <#if p.providerId == "google">
                <svg viewBox="0 0 48 48" aria-hidden="true" focusable="false">
                  <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"></path>
                  <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"></path>
                  <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"></path>
                  <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"></path>
                </svg>
              </#if>
              <span>Mit ${p.displayName!p.alias} anmelden</span>
            </a>
          </#list>
        </div>
      <#else>
        <div class="divider"><span>oder</span></div>
        <div class="social-list">
          <a id="social-google" class="social-btn" href="${url.loginUrl}<#if url.loginUrl?contains('?')>&<#else>?</#if>kc_idp_hint=google">
            <svg viewBox="0 0 48 48" aria-hidden="true" focusable="false">
              <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"></path>
              <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"></path>
              <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"></path>
              <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"></path>
            </svg>
            <span>Mit Google anmelden</span>
          </a>
        </div>
      </#if>

      <#if realm.registrationAllowed?? && realm.registrationAllowed>
        <p class="register">Noch kein Konto? <a class="link" href="${url.registrationUrl}">Jetzt registrieren</a></p>
      </#if>
    </section>
  </main>
</body>
</html>
