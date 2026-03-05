<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Profil aktualisieren</title>
  <link rel="stylesheet" href="${url.resourcesPath}/css/theme4.css" />
  <link rel="icon" href="${url.resourcesPath}/img/logo.png" />
</head>
<body>
  <main class="page">
    <section class="card">
      <img class="logo" src="${url.resourcesPath}/img/logo.png" alt="PCWächter" />
      <h1>Profil aktualisieren</h1>
      <p class="subtitle">Bitte überprüfe deine Profildaten.</p>

      <#if message?has_content>
        <div class="alert ${message.type!"info"}">${message.summary!""}</div>
      </#if>

      <form id="kc-update-profile-form" action="${url.loginAction}" method="post">
        <#if !(realm.registrationEmailAsUsername?? && realm.registrationEmailAsUsername)>
          <label for="username">Benutzername</label>
          <input id="username" name="username" type="text" value="${(user.username)!""}" autocomplete="username" />
        </#if>

        <label for="email">E-Mail</label>
        <input id="email" name="email" type="email" value="${(user.email)!""}" autocomplete="email" />

        <label for="firstName">Vorname</label>
        <input id="firstName" name="firstName" type="text" value="${(user.firstName)!""}" autocomplete="given-name" />

        <label for="lastName">Nachname</label>
        <input id="lastName" name="lastName" type="text" value="${(user.lastName)!""}" autocomplete="family-name" />

        <button id="kc-login" type="submit">Speichern</button>
      </form>
    </section>
  </main>
</body>
</html>
