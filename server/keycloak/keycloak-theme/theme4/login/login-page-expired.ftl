<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Sitzung abgelaufen</title>
  <link rel="stylesheet" href="${url.resourcesPath}/css/theme4.css" />
  <link rel="icon" href="${url.resourcesPath}/img/logo.png" />
</head>
<body>
  <main class="page">
    <section class="card">
      <img class="logo" src="${url.resourcesPath}/img/logo.png" alt="PCWächter" />
      <h1>Sitzung abgelaufen</h1>
      <p class="subtitle">Deine Anmeldung ist abgelaufen. Bitte starte den Vorgang erneut.</p>
      <p class="register"><a class="link" href="${(url.loginRestartFlowUrl)!((url.loginUrl)!'/login')}">Neu anmelden</a></p>
    </section>
  </main>
</body>
</html>
