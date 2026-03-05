<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Registrierung</title>
  <link rel="stylesheet" href="${url.resourcesPath}/css/theme4.css" />
  <link rel="icon" href="${url.resourcesPath}/img/logo.png" />
</head>
<body>
  <main class="page">
    <section class="card">
      <img class="logo" src="${url.resourcesPath}/img/logo.png" alt="PCWächter" />
      <h1>Konto erstellen</h1>
      <p class="subtitle">Erstelle dein PCWächter Konto in wenigen Schritten.</p>

      <#if message?has_content>
        <div class="alert ${message.type!"info"}">${message.summary!""}</div>
      </#if>

      <form id="kc-register-form" action="${url.registrationAction}" method="post">
        <#if !(realm.registrationEmailAsUsername?? && realm.registrationEmailAsUsername)>
          <label for="username">Benutzername</label>
          <input id="username" name="username" type="text" value="${(register.formData.username)!""}" autocomplete="username" />
        </#if>

        <label for="email">E-Mail</label>
        <input id="email" name="email" type="email" value="${(register.formData.email)!""}" autocomplete="email" />

        <label for="firstName">Vorname</label>
        <input id="firstName" name="firstName" type="text" value="${(register.formData.firstName)!""}" autocomplete="given-name" />

        <label for="lastName">Nachname</label>
        <input id="lastName" name="lastName" type="text" value="${(register.formData.lastName)!""}" autocomplete="family-name" />

        <label for="password">Passwort</label>
        <input id="password" name="password" type="password" autocomplete="new-password" />

        <label for="password-confirm">Passwort bestätigen</label>
        <input id="password-confirm" name="password-confirm" type="password" autocomplete="new-password" />

        <button id="kc-register" type="submit">Registrieren</button>
      </form>

      <p class="register">Bereits registriert? <a class="link" href="${url.loginUrl}">Zur Anmeldung</a></p>
    </section>
  </main>
</body>
</html>
