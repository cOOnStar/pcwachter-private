<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <title>theme2 Test</title>
  <style>
    body{font-family:Arial,sans-serif;background:#f5f5f5;color:#222;padding:24px}
    .box{max-width:720px;margin:0 auto;background:#fff;border:1px solid #ddd;padding:16px;border-radius:8px}
    h1{margin-top:0}
    .meta{font-size:14px;color:#555}
  </style>
</head>
<body>
  <div class="box">
    <h1>theme2 ist aktiv</h1>
    <p class="meta">Dies ist ein Test-Theme für Keycloak.</p>
    <p><strong>Realm:</strong> ${realm.name!"-"}</p>
    <p><strong>Client:</strong> ${client.clientId!"-"}</p>
    <p><strong>Login Action:</strong> ${url.loginAction!"-"}</p>
  </div>
</body>
</html>
