<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Passwort ändern</title>
  <link rel="stylesheet" href="${url.resourcesPath}/css/theme4.css" />
  <link rel="icon" href="${url.resourcesPath}/img/logo.png" />
</head>
<body>
  <main class="page">
    <section class="card">
      <img class="logo" src="${url.resourcesPath}/img/logo.png" alt="PCWächter" />
      <h1>Passwort ändern</h1>
      <p class="subtitle">Bitte vergib ein neues Passwort.</p>

      <#if message?has_content>
        <div class="alert ${message.type!"info"}">${message.summary!""}</div>
      </#if>

      <form id="kc-update-password-form" action="${url.loginAction}" method="post">
        <label for="password-new">Neues Passwort</label>
        <input id="password-new" name="password-new" type="password" autocomplete="new-password" />

        <label for="password-confirm">Passwort bestätigen</label>
        <input id="password-confirm" name="password-confirm" type="password" autocomplete="new-password" />

        <button id="kc-login" type="submit">Passwort speichern</button>
      </form>
    </section>
  </main>
</body>
</html>
