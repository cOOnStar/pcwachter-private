<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Passwort zurücksetzen</title>
  <link rel="stylesheet" href="${url.resourcesPath}/css/theme4.css" />
  <link rel="icon" href="${url.resourcesPath}/img/logo.png" />
</head>
<body>
  <main class="page">
    <section class="card">
      <img class="logo" src="${url.resourcesPath}/img/logo.png" alt="PCWächter" />
      <h1>Passwort zurücksetzen</h1>
      <p class="subtitle">Wir senden dir einen Link zum Zurücksetzen deines Passworts.</p>

      <#if message?has_content>
        <div class="alert ${message.type!"info"}">${message.summary!""}</div>
      </#if>

      <form id="kc-reset-password-form" action="${url.loginAction}" method="post">
        <label for="username">Benutzername oder E-Mail</label>
        <input id="username" name="username" type="text" value="${(auth.attemptedUsername)!""}" autofocus autocomplete="username" />
        <button id="kc-login" type="submit">Link senden</button>
      </form>

      <p class="register"><a class="link" href="${(url.loginUrl)!'/login'}">Zurück zur Anmeldung</a></p>
    </section>
  </main>
</body>
</html>
