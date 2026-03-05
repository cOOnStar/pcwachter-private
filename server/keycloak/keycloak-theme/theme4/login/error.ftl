<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Fehler</title>
  <link rel="stylesheet" href="${url.resourcesPath}/css/theme4.css" />
  <link rel="icon" href="${url.resourcesPath}/img/logo.png" />
</head>
<body>
  <main class="page">
    <section class="card">
      <img class="logo" src="${url.resourcesPath}/img/logo.png" alt="PCWächter" />
      <h1>Ein Fehler ist aufgetreten</h1>
      <p class="subtitle">Bitte versuche es erneut.</p>

      <#if message?has_content>
        <div class="alert error">${message.summary!"Unbekannter Fehler"}</div>
      <#else>
        <div class="alert error">Unbekannter Fehler</div>
      </#if>

      <p class="register"><a class="link" href="${(url.loginUrl)!"/login"}">Zurück zur Anmeldung</a></p>
    </section>
  </main>
</body>
</html>
