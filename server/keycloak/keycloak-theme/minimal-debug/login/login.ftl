<!doctype html>
<html>
<head>
    <meta charset="utf-8">
    <title>Keycloak Debug</title>
    <style>
        body{font-family:monospace;background:#111;color:#ddd;padding:16px}
        .box{border:1px solid #444;padding:10px;margin:10px 0;background:#000}
        .k{color:#7dd3fc}
        .v{color:#a7f3d0}
        h2{color:#fbbf24}
    </style>
</head>
<body>

<h1>🔍 Keycloak Debug Theme</h1>

<div class="box">
    <div><span class="k">templateName</span>: <span class="v">${templateName!""}</span></div>
    <div><span class="k">pageId</span>: <span class="v">${pageId!""}</span></div>
    <div><span class="k">execution</span>: <span class="v">${execution!""}</span></div>
</div>

<h2>URL</h2>
<div class="box">
    <div><span class="k">loginAction</span>: <span class="v">${url.loginAction!""}</span></div>
    <div><span class="k">loginUrl</span>: <span class="v">${url.loginUrl!""}</span></div>
    <div><span class="k">resourcesPath</span>: <span class="v">${url.resourcesPath!""}</span></div>
    <div><span class="k">resourcesCommonPath</span>: <span class="v">${url.resourcesCommonPath!""}</span></div>
    <div><span class="k">registrationUrl</span>: <span class="v">${url.registrationUrl!""}</span></div>
</div>

<h2>Realm</h2>
<div class="box">
    <div><span class="k">name</span>: <span class="v">${realm.name!""}</span></div>
    <div><span class="k">displayName</span>: <span class="v">${realm.displayName!""}</span></div>
    <div><span class="k">registrationAllowed</span>: <span class="v">${realm.registrationAllowed?string}</span></div>
    <div><span class="k">rememberMe</span>: <span class="v">${realm.rememberMe?string}</span></div>

    <#if realm.attributes??>
        <div style="margin-top:8px"><span class="k">realm.attributes</span>:</div>
        <#list realm.attributes?keys as k>
            <div> - <span class="k">${k}</span> = <span class="v">${realm.attributes[k]!""}</span></div>
        </#list>
    </#if>
</div>

<h2>Client</h2>
<div class="box">
    <div><span class="k">clientId</span>: <span class="v">${client.clientId!""}</span></div>
    <div><span class="k">name</span>: <span class="v">${client.name!""}</span></div>
    <div><span class="k">baseUrl</span>: <span class="v">${client.baseUrl!""}</span></div>

    <#if client.attributes??>
        <div style="margin-top:8px"><span class="k">client.attributes</span>:</div>
        <#list client.attributes?keys as k>
            <div> - <span class="k">${k}</span> = <span class="v">${client.attributes[k]!""}</span></div>
        </#list>
    </#if>
</div>

<h2>Login Model</h2>
<div class="box">
    <div><span class="k">login.username</span>: <span class="v">${(login.username)!""}</span></div>
    <div><span class="k">login.rememberMe</span>: <span class="v"><#if (login.rememberMe)??>${login.rememberMe?string}<#else>-</#if></span></div>
</div>

<h2>Messages</h2>
<div class="box">
    <#-- message ist bei dir oft nicht vorhanden; messagesPerField ist besser -->
    <div><span class="k">global message</span>: <span class="v">${(message.summary)!"(none)"}</span></div>

    <#if messagesPerField??>
        <div style="margin-top:8px"><span class="k">messagesPerField</span>:</div>
        <div>username: <span class="v">${messagesPerField.get("username")!"-"}</span></div>
        <div>password: <span class="v">${messagesPerField.get("password")!"-"}</span></div>
    </#if>
</div>

<h2>Data Model Keys</h2>
<div class="box">
    <#list .data_model?keys as key>
        ${key}<br>
    </#list>
</div>

</body>
</html>
